using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace RiseOn.Outline2D {
    /// <summary>
    /// The CPU half: turns a read-back mask into an exact Euclidean distance field on worker threads and uploads<br/>
    /// it as a half-float texture, in texels from the nearest silhouette texel. Sized for a shadow, it then also bakes<br/>
    /// up to three Gaussian blurs of the silhouette, each grown first by its own distance read off that field, into G, B<br/>
    /// and A. Sized once like the mask; each frame fills only its bottom-left corner.
    /// </summary>
    internal sealed class OutlineField {
        // Squared distance of a row that holds no silhouette texel at all.
        private const int NONE = int.MaxValue;

        // Farther than any texel can be along a row, so a side without silhouette needs no special case.
        private const int FAR = 1 << 16;

        // Written down a column without any silhouette texel: farther than any distance inside the largest frame.
        private const float BEYOND = 4096;

        // Most blur levels a field holds, one in each of G, B and A.
        private const int MAX_LEVELS = 3;

        private static readonly ProfilerMarker uploadMarker = new("Outline2D.Upload");

        private readonly Texture2D texture;
        private readonly int channels;
        private readonly int blurSize;
        private NativeArray<byte> mask;
        private NativeArray<int> rows;
        private NativeArray<ushort> field;
        private NativeArray<float> blur;
        private JobHandle handle;
        private bool running;

        public Texture2D Texture => texture;
        public GraphicsFormat Format => texture.graphicsFormat;
        public int Size { get; }

        /// <summary>The texture holds the shadow's blur levels next to the distance.</summary>
        public bool HasShadow => channels > 1;

        public OutlineField(int size, bool shadow) {
            Size = size;
            channels = shadow ? 4 : 1;

            // The shadow is blurred at half resolution: a soft edge has nothing finer than two texels to lose, and the
            // blur costs a quarter. One plane per level, sized for the largest frame like everything else here.
            blurSize = (size + 1) / 2;

            texture = new Texture2D(size, size, shadow ? GraphicsFormat.R16G16B16A16_SFloat : GraphicsFormat.R16_SFloat, TextureCreationFlags.None) {
                name = "OutlineField"
              , filterMode = FilterMode.Bilinear
              , wrapMode = TextureWrapMode.Clamp
              , hideFlags = HideFlags.HideAndDontSave
            };

            rows = new NativeArray<int>(size * size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            field = new NativeArray<ushort>(size * size * channels, Allocator.Persistent);

            // A texture never uploaded holds whatever the GPU had there, and an owner may bind it a frame before the first
            // capture lands; all zeros is distance 0, inside the silhouette, where nothing is drawn.
            texture.SetPixelData(field, 0);
            texture.Apply(false, false);

            if (shadow) blur = new NativeArray<float>(MAX_LEVELS * blurSize * blurSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        /// <summary>
        /// Starts the transform of a freshly read mask and, when the field holds a shadow, the blur of up to three levels,<br/>
        /// each the silhouette grown by a distance then blurred at a standard deviation, both in field texels; a previous<br/>
        /// run still going is finished first.
        /// </summary>
        public void Schedule(NativeArray<byte> data, int rowBytes, int pixelBytes, in OutlineFrame frame, Vector3 sigmas, Vector3 growths, int levels) {
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

            var columnPass = new ColumnJob {
                rows = rows
              , field = field
              , width = frame.Width
              , height = frame.Height
              , fieldWidth = Size
              , channels = channels
            }.Schedule(frame.Width, 8, rowPass);

            handle = levels > 0 && HasShadow ? ScheduleBlur(this, frame, sigmas, growths, levels, columnPass) : columnPass;

            running = true;
            JobHandle.ScheduleBatchedJobs();

            // The blur grows the silhouette by reading the distance in R, so it runs once the distance transform is done.
            static JobHandle ScheduleBlur(OutlineField context, in OutlineFrame frame, Vector3 sigmas, Vector3 growths, int levels, JobHandle columnPass) {
                var width = (frame.Width + 1) / 2;
                var height = (frame.Height + 1) / 2;

                // Half resolution halves every standard deviation.
                var radii = new int3x3(BoxRadii(sigmas.x / 2), BoxRadii(sigmas.y / 2), BoxRadii(sigmas.z / 2));

                var blurRows = new BlurRowJob {
                    field = context.field
                  , blur = context.blur
                  , width = width
                  , frameWidth = frame.Width
                  , frameHeight = frame.Height
                  , fieldWidth = context.Size
                  , channels = context.channels
                  , blurSize = context.blurSize
                  , levels = levels
                  , radii = radii
                  , growths = growths
                }.Schedule(height, 4, columnPass);

                var blurColumns = new BlurColumnJob {
                    blur = context.blur
                  , height = height
                  , blurSize = context.blurSize
                  , levels = levels
                  , radii = radii
                }.Schedule(width, 4, blurRows);

                return new BlurWriteJob {
                    blur = context.blur
                  , field = context.field
                  , width = width
                  , height = height
                  , frameWidth = frame.Width
                  , fieldWidth = context.Size
                  , blurSize = context.blurSize
                  , levels = levels
                }.Schedule(frame.Height, 8, blurColumns);
            }
        }

        // Three box passes whose variances add up to sigma², the widths picked as in "Fast almost-Gaussian filtering"
        // (Kutskir): each pass is a running sum, so a level costs the same however soft it is. As radii.
        private static int3 BoxRadii(float sigma) {
            const int PASSES = 3;

            if (sigma < .5f) return int3.zero;

            var ideal = math.sqrt(12 * sigma * sigma / PASSES + 1);
            var lower = (int)math.floor(ideal);

            if (lower % 2 == 0) --lower;

            var upper = lower + 2;
            var below = (int)math.round((12 * sigma * sigma - PASSES * lower * lower - 4 * PASSES * lower - 3 * PASSES) / (-4f * lower - 4));

            return new int3(0 < below ? lower : upper, 1 < below ? lower : upper, 2 < below ? lower : upper) / 2;
        }

        /// <summary>Waits for the scheduled jobs on this thread, so <see cref="TryUpload"/> succeeds at once.</summary>
        public void Complete() {
            handle.Complete();
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
            if (blur.IsCreated) blur.Dispose();
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
            public int channels;

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

                var beyond = (ushort)math.f32tof16(BEYOND);

                if (k < 0) {
                    for (var q = 0; q < height; ++q) field[(q * fieldWidth + x) * channels] = beyond;
                } else {
                    for (int q = 0, j = 0; q < height; ++q) {
                        while (z[j + 1] < q) ++j;

                        var p = v[j];
                        var d = math.sqrt((q - p) * (q - p) + rows[p * width + x]);

                        // Unclamped: the ring's outer ramp spans half a screen pixel, many texels when zoomed out, and must fade
                        // out before the quad's edge instead of tinting the whole quad.
                        field[(q * fieldWidth + x) * channels] = (ushort)math.f32tof16(d);
                    }
                }

                v.Dispose();
                z.Dispose();
            }
        }

        // Each half resolution row, for each level: the silhouette grown by the level's distance, as read off the field,
        // averaged over 2x2 blocks, then blurred along the row. The grown edge ramps over one texel.
        [BurstCompile]
        private struct BlurRowJob : IJobParallelFor {
            [ReadOnly] public NativeArray<ushort> field;
            [NativeDisableParallelForRestriction] public NativeArray<float> blur;
            public int width;
            public int frameWidth;
            public int frameHeight;
            public int fieldWidth;
            public int channels;
            public int blurSize;
            public int levels;
            public int3x3 radii;
            public float3 growths;

            public void Execute(int y) {
                var lines = new NativeArray<float>(MAX_LEVELS * width, Allocator.Temp);
                var spare = new NativeArray<float>(width, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                // The silhouette's own edge is half a texel out, so grown by g its edge is at g + 0.5; one more for the
                // ramp's far end.
                var edges = growths + 1;

                // Each distance is read once and counted towards every level.
                for (var x = 0; x < width; ++x) {
                    var sums = float3.zero;

                    for (var dy = 0; dy < 2; ++dy) {
                        var fy = 2 * y + dy;

                        if (fy >= frameHeight) continue;

                        for (var dx = 0; dx < 2; ++dx) {
                            var fx = 2 * x + dx;

                            if (fx >= frameWidth) continue;

                            sums += math.saturate(edges - math.f16tof32(field[(fy * fieldWidth + fx) * channels]));
                        }
                    }

                    for (var level = 0; level < levels; ++level) lines[level * width + x] = sums[level] * .25f;
                }

                for (var level = 0; level < levels; ++level) {
                    var line = lines.GetSubArray(level * width, width);

                    Blur(line, spare, width, radii[level]);
                    NativeArray<float>.Copy(line, 0, blur, (level * blurSize + y) * blurSize, width);
                }

                lines.Dispose();
                spare.Dispose();
            }
        }

        // Each half resolution column, every level blurred down it in place.
        [BurstCompile]
        private struct BlurColumnJob : IJobParallelFor {
            [NativeDisableParallelForRestriction] public NativeArray<float> blur;
            public int height;
            public int blurSize;
            public int levels;
            public int3x3 radii;

            public void Execute(int x) {
                var line = new NativeArray<float>(height, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                var spare = new NativeArray<float>(height, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                for (var level = 0; level < levels; ++level) {
                    var plane = level * blurSize * blurSize;

                    for (var y = 0; y < height; ++y) line[y] = blur[plane + y * blurSize + x];

                    Blur(line, spare, height, radii[level]);

                    for (var y = 0; y < height; ++y) blur[plane + y * blurSize + x] = line[y];
                }

                line.Dispose();
                spare.Dispose();
            }
        }

        // Each full resolution row of the field: every level sampled bilinearly from its half resolution plane into G, B
        // or A; channels past the levels are cleared.
        [BurstCompile]
        private struct BlurWriteJob : IJobParallelFor {
            [ReadOnly] public NativeArray<float> blur;
            [NativeDisableParallelForRestriction] public NativeArray<ushort> field;
            public int width;
            public int height;
            public int frameWidth;
            public int fieldWidth;
            public int blurSize;
            public int levels;

            public void Execute(int y) {
                // A full texel's centre in half resolution texels, clamped so the edge texels hold.
                var sy = math.clamp((y + .5f) / 2 - .5f, 0, height - 1);
                var y0 = (int)sy;
                var y1 = math.min(y0 + 1, height - 1);
                var ty = sy - y0;

                for (var x = 0; x < frameWidth; ++x) {
                    var sx = math.clamp((x + .5f) / 2 - .5f, 0, width - 1);
                    var x0 = (int)sx;
                    var x1 = math.min(x0 + 1, width - 1);
                    var tx = sx - x0;
                    var texel = (y * fieldWidth + x) * 4;

                    for (var level = 0; level < MAX_LEVELS; ++level) {
                        var value = 0f;

                        if (level < levels) {
                            var plane = level * blurSize * blurSize;
                            var bottom = math.lerp(blur[plane + y0 * blurSize + x0], blur[plane + y0 * blurSize + x1], tx);
                            var top = math.lerp(blur[plane + y1 * blurSize + x0], blur[plane + y1 * blurSize + x1], tx);

                            value = math.lerp(bottom, top, ty);
                        }

                        field[texel + 1 + level] = (ushort)math.f32tof16(value);
                    }
                }
            }
        }

        // Three box passes along one line, the result left in line; outside the line counts as no silhouette.
        private static void Blur(NativeArray<float> line, NativeArray<float> spare, int length, int3 radii) {
            Box(line, spare, length, radii.x);
            Box(spare, line, length, radii.y);
            Box(line, spare, length, radii.z);
            NativeArray<float>.Copy(spare, line, length);
        }

        // A running sum over 2r + 1 texels: the window gains one texel and loses one per step.
        private static void Box(NativeArray<float> src, NativeArray<float> dst, int length, int radius) {
            if (radius <= 0) {
                NativeArray<float>.Copy(src, dst, length);
                return;
            }

            var sum = 0f;

            for (var i = 0; i <= radius && i < length; ++i) sum += src[i];

            var norm = 1f / (2 * radius + 1);

            for (var i = 0; i < length; ++i) {
                dst[i] = sum * norm;

                var enter = i + radius + 1;
                var leave = i - radius;

                if (enter < length) sum += src[enter];
                if (leave >= 0) sum -= src[leave];
            }
        }
    }
}