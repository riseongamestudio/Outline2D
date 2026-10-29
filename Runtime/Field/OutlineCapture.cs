using Unity.Collections;
using Unity.Profiling;
using UnityEngine;

namespace RiseOn.Outline2D {
    /// <summary>
    /// One outline's captures, whatever it outlines: fit a frame over the source, draw and read back its mask,<br/>
    /// turn that into a distance field, and the shadow's blur levels when asked, on worker threads and upload it. One<br/>
    /// capture runs at a time; new targets drop the one in flight, new settings wait for it to land.
    /// </summary>
    internal sealed class OutlineCapture {
        // Below this a capture holds too few texels to outline anything.
        private const int MIN_BUDGET = 1024;

        // Above this, squared distances stop being exact in float inside the distance transform.
        private const int MAX_SIZE = 2048;

        private static readonly ProfilerMarker captureMarker = new("Outline2D.Capture");

        private OutlineMask mask;
        private OutlineField field;
        private OutlineFrame pending;
        private Stage stage;
        private int version;
        private int budget = MIN_BUDGET;
        private bool outdated;

        private enum Stage {
            None
          , Capture
          , Readback
          , Transform
          , Shown
        }

        /// <summary>The frame of the field on the texture, once one has landed.</summary>
        public OutlineFrame Frame { get; private set; }

        /// <summary>The field: distance in R, the shadow's blur levels in G, B and A when allocated for a shadow; a new<br/>
        /// texture only after <see cref="Allocate"/> returns true.</summary>
        public Texture2D Texture => field?.Texture;

        /// <summary>A capture should run now: new targets, or new settings once the last capture has landed.</summary>
        public bool IsDue => stage is Stage.Capture || (stage is Stage.Shown && outdated);

        /// <summary>
        /// Sizes the buffers for the largest frame the budget allows, so selecting never allocates; only a new<br/>
        /// budget does, and then this returns true because the texture is a new one. Either side is capped at twice<br/>
        /// the square root of the budget: up to a 4:1 aspect the whole budget is used, beyond it the capture only<br/>
        /// gets cheaper. A shadow needs a field with room for its blur levels, so turning it on or off allocates too.
        /// </summary>
        public bool Allocate(int pixelBudget, bool shadow) {
            budget = Mathf.Max(pixelBudget, MIN_BUDGET);

            var size = Mathf.Min(Mathf.CeilToInt(2 * Mathf.Sqrt(budget)), MAX_SIZE);

            if (mask != null && mask.Size == size && field.HasShadow == shadow) return false;

            field?.Release();
            mask?.Release();

            mask = new OutlineMask(size);
            field = new OutlineField(size, shadow);

            return true;
        }

        /// <summary>Builds the GPU programs of the mask and of these outline materials now, not at the first capture.</summary>
        public void Warm(params Material[] outlines) => mask.Warm(outlines);

        /// <summary>New targets: whatever is in flight no longer counts, and a capture is due at once if there is any.</summary>
        public void Restart(bool any) {
            ++version;
            outdated = false;
            stage = any ? Stage.Capture : Stage.None;
        }

        /// <summary>A setting the capture depends on changed: capture again once the capture in flight has landed.</summary>
        public void MarkOutdated() {
            if (stage is not Stage.None) outdated = true;
        }

        /// <summary>
        /// Captures the source in the owner's local space, its bounds padded by a local distance, and bakes up to three<br/>
        /// shadow blur levels: the silhouette grown by a local distance, then blurred to a softness (the distance it fades<br/>
        /// out over), also local. The buffers must be allocated, for a shadow when there are levels.
        /// </summary>
        public void Run(IMaskSource source, Component owner, float padding, float cutoff, Vector3 shadowSoftness, Vector3 shadowGrowth, int shadowLevels) {
            using var _ = captureMarker.Auto();

            var worldToLocal = owner.transform.worldToLocalMatrix;

            outdated = false;

            // A collapsed scale, or nothing left to draw: no outline until the next restart.
            if (!float.IsFinite(padding) || !source.TryGetBounds(worldToLocal, out var min, out var max)) {
                stage = Stage.None;
                return;
            }

            var frame = OutlineFrame.Fit(min, max, padding, budget, mask.Size);
            var captured = version;

            // A level fades out over about three standard deviations of its Gaussian; the blur works in field texels.
            var sigmas = field.HasShadow ? shadowSoftness / (3 * frame.TexelSize) : Vector3.zero;
            var growths = field.HasShadow ? shadowGrowth / frame.TexelSize : Vector3.zero;
            var levels = field.HasShadow ? Mathf.Clamp(shadowLevels, 0, 3) : 0;

            // Set first: without asynchronous readback the data comes back before Capture returns.
            stage = Stage.Readback;
            mask.Capture(source, worldToLocal, frame, cutoff, (data, rowBytes, pixelBytes) => OnMask(owner, captured, frame, sigmas, growths, levels, data, rowBytes, pixelBytes));
        }

        /// <summary>Uploads the field once its transform is done; true once per capture, when the new outline can show.</summary>
        public bool TryUpload() {
            if (stage is not Stage.Transform || !field.TryUpload()) return false;

            Frame = pending;
            stage = Stage.Shown;

            return true;
        }

        /// <summary>
        /// The quad that shows <see cref="Frame"/>, in the owner's local space, and its UVs. The corners sit on<br/>
        /// texel centres, so bilinear sampling never reaches the texels outside the frame.
        /// </summary>
        public void GetQuad(out Rect position, out Rect uv) {
            var frame = Frame;
            var size = field.Size;

            position = Rect.MinMaxRect(
                frame.Origin.x + .5f * frame.TexelSize
              , frame.Origin.y + .5f * frame.TexelSize
              , frame.Origin.x + (frame.Width - .5f) * frame.TexelSize
              , frame.Origin.y + (frame.Height - .5f) * frame.TexelSize);

            uv = Rect.MinMaxRect(.5f / size, .5f / size, (frame.Width - .5f) / size, (frame.Height - .5f) / size);
        }

        public void Release() {
            field?.Release();
            mask?.Release();
            field = null;
            mask = null;
        }

        private void OnMask(Component owner, int captured, OutlineFrame frame, Vector3 sigmas, Vector3 growths, int levels, NativeArray<byte> data, int rowBytes, int pixelBytes) {
            // Newer targets, or the owner is gone: this capture no longer matters.
            if (captured != version || stage is not Stage.Readback || field == null) return;

            if (!data.IsCreated) {
                Debug.LogWarning($"{owner.GetType().Name} {owner.name}: silhouette readback failed, outline skipped.", owner);
                stage = Stage.None;
                return;
            }

            pending = frame;
            field.Schedule(data, rowBytes, pixelBytes, frame, sigmas, growths, levels);
            stage = Stage.Transform;
        }
    }
}