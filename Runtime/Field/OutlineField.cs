using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace RiseOn.SpriteOutline {
    /// <summary>
    /// The CPU half: turns a read-back mask into an exact Euclidean distance field on worker threads and uploads<br/>
    /// it as a half-float texture, in texels from the nearest silhouette texel. Sized once like the mask; each<br/>
    /// frame fills only its bottom-left corner.
    /// </summary>
    internal sealed class OutlineField {
        // Squared distance of a row that holds no silhouette texel at all.
        private const int NONE = int.MaxValue;

        // Farther than any texel can be along a row, so a side without silhouette needs no special case.
        private const int FAR = 1 << 16;

        private static readonly ProfilerMarker uploadMarker = new("SpriteOutline.Upload");

        private readonly Texture2D texture;
        private NativeArray<byte> mask;
        private NativeArray<int> rows;
        private NativeArray<ushort> field;
        private JobHandle handle;
        private bool running;

        public Texture Texture => texture;
        public int Size { get; }

        public OutlineField(int size) {
            Size = size;

            texture = new Texture2D(size, size, GraphicsFormat.R16_SFloat, TextureCreationFlags.None) {
                name = "OutlineField"
              , filterMode = FilterMode.Bilinear
              , wrapMode = TextureWrapMode.Clamp
              , hideFlags = HideFlags.HideAndDontSave
            };

            rows = new NativeArray<int>(size * size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            field = new NativeArray<ushort>(size * size, Allocator.Persistent);
        }

        /// <summary>Starts the transform of a freshly read mask; a previous run still going is finished first.</summary>
        public void Schedule(NativeArray<byte> data, int rowBytes, int pixelBytes, in OutlineFrame frame) {
            handle.Complete();

            // The readback's own array only lives until its callback returns.
            var length = rowBytes * frame.Height;

            if (!mask.IsCreated || mask.Length < length) {
                if (mask.IsCreated) mask.Dispose();

                mask = new NativeArray<byte>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            NativeArray<byte>.Copy(data, mask, length);

            var rowPass = new RowJob {
                mask = mask
              , rows = rows
              , width = frame.Width
              , rowBytes = rowBytes
              , pixelBytes = pixelBytes
            }.Schedule(frame.Height, 8);

            handle = new ColumnJob {
                rows = rows
              , field = field
              , width = frame.Width
              , height = frame.Height
              , fieldWidth = Size
              , maxDistance = frame.MaxRadius + 2
            }.Schedule(frame.Width, 8, rowPass);

            running = true;
            JobHandle.ScheduleBatchedJobs();
        }

        /// <summary>Uploads the field once the transform is done; false while it is still running.</summary>
        public bool TryUpload() {
            if (!running || !handle.IsCompleted) return false;

            using var _ = uploadMarker.Auto();

            handle.Complete();
            running = false;

            texture.SetPixelData(field, 0);
            texture.Apply(false, false);

            return true;
        }

        public void Release() {
            handle.Complete();
            running = false;

            if (mask.IsCreated) mask.Dispose();
            rows.Dispose();
            field.Dispose();

            OutlineMask.SafeDestroy(texture);
        }

        // Squared distance to the nearest silhouette texel within each row: one sweep each way.
        [BurstCompile]
        private struct RowJob : IJobParallelFor {
            [ReadOnly] public NativeArray<byte> mask;
            [NativeDisableParallelForRestriction] public NativeArray<int> rows;
            public int width;
            public int rowBytes;
            public int pixelBytes;

            public void Execute(int y) {
                var src = y * rowBytes;
                var dst = y * width;

                var last = -FAR;

                for (var x = 0; x < width; ++x) {
                    if (mask[src + x * pixelBytes] >= 128) last = x;

                    rows[dst + x] = x - last;
                }

                var next = width + FAR;

                for (var x = width - 1; x >= 0; --x) {
                    if (mask[src + x * pixelBytes] >= 128) next = x;

                    var d = math.min(rows[dst + x], next - x);

                    rows[dst + x] = d >= FAR ? NONE : d * d;
                }
            }
        }

        // Felzenszwalb & Huttenlocher down each column: the lower envelope of the parabolas (q - p)^2 + rows[p]
        // gives the exact Euclidean distance in linear time, however wide the outline is.
        [BurstCompile]
        private struct ColumnJob : IJobParallelFor {
            [ReadOnly] public NativeArray<int> rows;
            [NativeDisableParallelForRestriction] public NativeArray<ushort> field;
            public int width;
            public int height;
            public int fieldWidth;
            public float maxDistance;

            public void Execute(int x) {
                var v = new NativeArray<int>(height, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                var z = new NativeArray<float>(height + 1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                var k = -1;

                for (var q = 0; q < height; ++q) {
                    var fq = rows[q * width + x];

                    if (fq == NONE) continue;

                    if (k < 0) {
                        k = 0;
                        v[0] = q;
                        z[0] = float.NegativeInfinity;
                        z[1] = float.PositiveInfinity;
                        continue;
                    }

                    // Terminates at k = 0 at the latest, since z[0] is minus infinity.
                    float s;

                    while (true) {
                        var p = v[k];

                        s = (fq + q * q - rows[p * width + x] - p * p) / (2f * (q - p));

                        if (s > z[k]) break;

                        --k;
                    }

                    v[++k] = q;
                    z[k] = s;
                    z[k + 1] = float.PositiveInfinity;
                }

                var beyond = (ushort)math.f32tof16(maxDistance);

                if (k < 0) {
                    for (var q = 0; q < height; ++q) field[q * fieldWidth + x] = beyond;
                } else {
                    for (int q = 0, j = 0; q < height; ++q) {
                        while (z[j + 1] < q) ++j;

                        var p = v[j];
                        var d = math.sqrt((float)((q - p) * (q - p) + rows[p * width + x]));

                        field[q * fieldWidth + x] = (ushort)math.f32tof16(math.min(d, maxDistance));
                    }
                }

                v.Dispose();
                z.Dispose();
            }
        }
    }
}