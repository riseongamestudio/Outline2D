using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RiseOn.SpriteOutline {
    /// <summary>
    /// The GPU half: draws the sprites' silhouettes into the bottom-left corner of a square single-channel<br/>
    /// target, one DrawRenderer each, then reads that corner back. The target is allocated once at the largest<br/>
    /// size a frame can take, so a capture never allocates GPU memory.
    /// </summary>
    internal sealed class OutlineMask {
        private static readonly int cutoffId = Shader.PropertyToID("_Cutoff");

        private readonly RenderTexture texture;
        private readonly Material material;
        private readonly CommandBuffer cmd = new() { name = "SpriteOutline Capture" };
        private readonly int bytesPerPixel;

        // Only for devices without asynchronous readback: a blocking copy that stalls once per capture.
        private Texture2D syncCopy;

        /// <summary>Receives the captured bytes, rows bottom-first; data is not created when the readback failed.</summary>
        public delegate void DataHandler(NativeArray<byte> data, int rowBytes, int pixelBytes);

        public int Size { get; }

        public OutlineMask(int size) {
            Size = size;
            texture = CreateTexture(size, out bytesPerPixel);
            material = new Material(GraphicsSettings.GetRenderPipelineSettings<SpriteOutlineResources>().MaskShader) { hideFlags = HideFlags.HideAndDontSave };

            // R8 wherever the device can render to it.
            static RenderTexture CreateTexture(int size, out int bytesPerPixel) {
                var format = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Render)
                    ? GraphicsFormat.R8_UNorm
                    : GraphicsFormat.R8G8B8A8_UNorm;

                bytesPerPixel = format is GraphicsFormat.R8_UNorm ? 1 : 4;

                var texture = new RenderTexture(new RenderTextureDescriptor(size, size, format, 0)) {
                    name = "OutlineMask"
                  , filterMode = FilterMode.Point
                  , wrapMode = TextureWrapMode.Clamp
                  , hideFlags = HideFlags.HideAndDontSave
                };
                texture.Create();

                return texture;
            }
        }

        /// <summary>Captures the renderers into frame.Width x frame.Height texels and hands the bytes to onData.</summary>
        public void Capture(IReadOnlyList<SpriteRenderer> renderers, Matrix4x4 worldToLocal, in OutlineFrame frame, float cutoff, DataHandler onData) {
            material.SetFloat(cutoffId, cutoff);

            cmd.Clear();
            cmd.SetRenderTarget(texture);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.SetViewport(new Rect(0, 0, frame.Width, frame.Height));
            cmd.SetViewProjectionMatrices(worldToLocal, frame.Projection);

            foreach (var renderer in renderers) {
                if (renderer == null || renderer.sprite == null) continue;

                cmd.DrawRenderer(renderer, material, 0, 0);
            }

            if (!SystemInfo.supportsAsyncGPUReadback) {
                Graphics.ExecuteCommandBuffer(cmd);
                ReadSync(this, frame, onData);
                return;
            }

            var rowBytes = frame.Width * bytesPerPixel;
            var pixelBytes = bytesPerPixel;

            cmd.RequestAsyncReadback(texture, 0, 0, frame.Width, 0, frame.Height, 0, 1, request => onData(request.hasError ? default : request.GetData<byte>(), rowBytes, pixelBytes));
            Graphics.ExecuteCommandBuffer(cmd);

            static void ReadSync(OutlineMask context, in OutlineFrame frame, DataHandler onData) {
                if (context.syncCopy == null) {
                    context.syncCopy = new Texture2D(context.Size, context.Size, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
                }

                var active = RenderTexture.active;

                RenderTexture.active = context.texture;
                context.syncCopy.ReadPixels(new Rect(0, 0, frame.Width, frame.Height), 0, 0, false);
                RenderTexture.active = active;

                onData(context.syncCopy.GetPixelData<byte>(0), context.Size * 4, 4);
            }
        }

        /// <summary>
        /// Draws once with the mask shader and with the outline material and reads a texel back, so the GPU programs<br/>
        /// and the readback path exist before the first capture needs them.
        /// </summary>
        public void Warm(Material outline, Mesh quad) {
            cmd.Clear();
            cmd.SetRenderTarget(texture);
            cmd.DrawMesh(quad, Matrix4x4.identity, material, 0, 0);
            cmd.DrawMesh(quad, Matrix4x4.identity, outline, 0, 0);

            if (SystemInfo.supportsAsyncGPUReadback) cmd.RequestAsyncReadback(texture, 0, 0, 1, 0, 1, 0, 1, _ => { });

            Graphics.ExecuteCommandBuffer(cmd);
        }

        public void Release() {
            cmd.Release();
            texture.Release();

            SafeDestroy(texture);
            SafeDestroy(material);
            SafeDestroy(syncCopy);
        }

        internal static void SafeDestroy(Object target) {
            if (target == null) return;

            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}