using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace RiseOn.Outline2D {
    /// <summary>
    /// The GPU half of <see cref="OutlineQuality.Fast"/>: turns the mask into the field by jump flooding and copies it<br/>
    /// into the field texture, all on the GPU, so nothing is read back. A build is the seed from the mask, then steps:<br/>
    /// the jumps from the largest step down, one more of a single texel, and the resolve with the copy, which a caller<br/>
    /// records all at once or a few at a time over frames.
    /// </summary>
    internal sealed class OutlineJumpFlood {
        private const int SEED_PASS = 0;
        private const int JUMP_PASS = 1;
        private const int RESOLVE_PASS = 2;

        private static readonly int sourceId = Shader.PropertyToID("_JumpSource");
        private static readonly int frameId = Shader.PropertyToID("_JumpFrame");
        private static readonly int stepId = Shader.PropertyToID("_JumpStep");
        private static readonly int growthsId = Shader.PropertyToID("_JumpGrowths");
        private static readonly int sigmasId = Shader.PropertyToID("_JumpSigmas");
        private static readonly int levelsId = Shader.PropertyToID("_JumpLevels");

        private readonly Material material;
        private readonly RenderTexture seeds;
        private readonly RenderTexture jumped;
        private readonly RenderTexture output;
        private readonly Mesh quad;
        private readonly MaterialPropertyBlock properties = new();
        private readonly int size;

        private static Shader Shader => GraphicsSettings.GetRenderPipelineSettings<Outline2DResources>().JumpFloodShader;

        /// <summary>Whether this device renders the formats a build needs and copies render targets into textures.</summary>
        public static bool IsSupported(GraphicsFormat fieldFormat) {
            return Shader != null && Shader.isSupported
                && SeedFormat() != GraphicsFormat.None
                && SystemInfo.IsFormatSupported(fieldFormat, GraphicsFormatUsage.Render)
                && (SystemInfo.copyTextureSupport & CopyTextureSupport.RTToTexture) != 0;
        }

        // Texel indices up to 2048 are exact in half floats; floats where the device renders them.
        private static GraphicsFormat SeedFormat() {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R32G32_SFloat, GraphicsFormatUsage.Render)) return GraphicsFormat.R32G32_SFloat;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16_SFloat, GraphicsFormatUsage.Render)) return GraphicsFormat.R16G16_SFloat;

            return GraphicsFormat.None;
        }

        public OutlineJumpFlood(int size, GraphicsFormat fieldFormat) {
            this.size = size;

            material = new Material(Shader) { hideFlags = HideFlags.HideAndDontSave };
            seeds = Create("OutlineSeeds", size, SeedFormat());
            jumped = Create("OutlineSeedsJumped", size, SeedFormat());
            output = Create("OutlineFieldFast", size, fieldFormat);

            quad = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            quad.SetVertices(new[] { new Vector3(-1, -1), new Vector3(1, -1), new Vector3(1, 1), new Vector3(-1, 1) });
            quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);

            static RenderTexture Create(string name, int size, GraphicsFormat format) {
                var texture = new RenderTexture(new RenderTextureDescriptor(size, size, format, 0)) {
                    name = name
                  , filterMode = FilterMode.Point
                  , wrapMode = TextureWrapMode.Clamp
                  , hideFlags = HideFlags.HideAndDontSave
                };
                texture.Create();

                return texture;
            }
        }

        /// <summary>How many steps follow the seed for this frame, the resolve included.</summary>
        public static int StepCount(in OutlineFrame frame) => JumpCount(frame) + 1;

        // log2 of the frame's larger side rounded up, and the extra single-texel jump that mends what the halving missed.
        private static int JumpCount(in OutlineFrame frame) => Mathf.CeilToInt(Mathf.Log(Mathf.Max(frame.Width, frame.Height, 2), 2)) + 1;

        /// <summary>Records the seed from the mask, which must hold this frame's silhouette by then.</summary>
        public void Seed(CommandBuffer cmd, OutlineMask mask, in OutlineFrame frame) {
            Draw(cmd, mask.Texture, seeds, SEED_PASS, frame);
        }

        /// <summary>
        /// Records one step after the seed, in order; the last resolves the levels, grown and blurred in field texels, and<br/>
        /// copies the result into field, which from then on holds the new field.
        /// </summary>
        public void Step(CommandBuffer cmd, int step, in OutlineFrame frame, Vector3 sigmas, Vector3 growths, int levels, Texture2D field) {
            var jumps = JumpCount(frame);

            if (step < jumps) {
                // Halving steps from the largest power of two under the frame, then one more of a texel.
                properties.SetFloat(stepId, step < jumps - 1 ? 1 << (jumps - 2 - step) : 1);

                if (step % 2 == 0) Draw(cmd, seeds, jumped, JUMP_PASS, frame);
                else Draw(cmd, jumped, seeds, JUMP_PASS, frame);

                return;
            }

            properties.SetVector(growthsId, growths);
            properties.SetVector(sigmasId, sigmas);
            properties.SetFloat(levelsId, levels);

            Draw(cmd, jumps % 2 == 1 ? jumped : seeds, output, RESOLVE_PASS, frame);

            // The whole texture: no sub-rect, so nothing depends on which way the API stores rows.
            cmd.CopyTexture(output, field);
        }

        private void Draw(CommandBuffer cmd, Texture source, RenderTexture target, int pass, in OutlineFrame frame) {
            properties.SetTexture(sourceId, source);
            properties.SetVector(frameId, new Vector4(frame.Width, frame.Height, 1f / size, size));

            cmd.SetRenderTarget(target);
            cmd.SetViewport(new Rect(0, 0, frame.Width, frame.Height));
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            cmd.DrawMesh(quad, Matrix4x4.identity, material, 0, pass, properties);
        }

        public void Release() {
            seeds.Release();
            jumped.Release();
            output.Release();

            OutlineMask.SafeDestroy(seeds);
            OutlineMask.SafeDestroy(jumped);
            OutlineMask.SafeDestroy(output);
            OutlineMask.SafeDestroy(quad);
            OutlineMask.SafeDestroy(material);
        }
    }
}