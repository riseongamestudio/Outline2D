using Unity.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiseOn.Outline2D {
    /// <summary>
    /// One outline's captures, whatever it outlines: fit a frame over the source, draw its mask, and turn that into a<br/>
    /// distance field, with the shadow's blur levels when asked, as <see cref="Performance"/> says: read back and built<br/>
    /// on worker threads, or jump flooded on the GPU. One capture runs at a time; new targets drop the one in flight,<br/>
    /// new settings and the next interval wait for it to land.
    /// </summary>
    internal sealed class OutlineCapture {
        // Below this a capture holds too few texels to outline anything.
        private const int MIN_BUDGET = 1024;

        // Above this, squared distances stop being exact in float inside the distance transform.
        private const int MAX_SIZE = 2048;

        // Frames an asynchronous Fast build spreads its steps over.
        private const int FLOOD_FRAMES = 3;

        private static readonly ProfilerMarker captureMarker = new("Outline2D.Capture");
        private static readonly ProfilerMarker floodMarker = new("Outline2D.Flood");

        private OutlineMask mask;
        private OutlineField field;

        // Made by the first allocation that asks for Fast, never in a field initializer, where Unity refuses native objects
        // for an owner; then kept, as an outline that switches between profiles would otherwise allocate at each switch.
        private OutlineJumpFlood flood;
        private CommandBuffer cmd;
        private bool fast;
        private OutlineFrame pending;
        private Stage stage;
        private int version;
        private int budget = MIN_BUDGET;
        private bool outdated;
        private bool hasTargets;
        private int startFrame;
        private float startTime;

        // The Fast build in flight: its levels, the next step to record, how many it takes, and the frame that recorded
        // the last ones.
        private (Vector3 sigmas, Vector3 growths, int levels) floodLevels;
        private int nextStep;
        private int stepCount;
        private int stepFrame;

        private enum Stage {
            None
          , Capture
          , Readback
          , Transform
          , Flood
          , Shown
        }

        /// <summary>How captures run and when the next is due; Quality takes effect at the next <see cref="Allocate"/>.</summary>
        public OutlinePerformanceProfile Performance { get; set; } = OutlinePerformanceProfile.Default;

        /// <summary>The frame of the field on the texture, once one has landed.</summary>
        public OutlineFrame Frame { get; private set; }

        /// <summary>The field: distance in R, the shadow's blur levels in G, B and A when allocated for a shadow; a new<br/>
        /// texture only after <see cref="Allocate"/> returns true.</summary>
        public Texture2D Texture => field?.Texture;

        /// <summary>
        /// A capture should run now: new targets; or, once the last capture has landed, new settings or the interval<br/>
        /// the frequency sets. Under an interval a group that came out empty is tried again.
        /// </summary>
        public bool IsDue => stage switch {
            Stage.Capture => true
          , Stage.Shown => outdated || IsIntervalDue
          , Stage.None => hasTargets && IsIntervalDue
          , _ => false
        };

        private bool IsIntervalDue => Performance.Frequency switch {
            OutlineFrequency.FrameInterval => Time.frameCount - startFrame >= Mathf.Max(Performance.Frames, 1)
          , OutlineFrequency.TimeInterval => Time.unscaledTime - startTime >= Performance.Seconds
          , _ => false
        };

        /// <summary>
        /// Sizes the buffers for the largest frame the budget allows, so selecting never allocates; only a new<br/>
        /// budget does, and then this returns true because the texture is a new one. Either side is capped at twice<br/>
        /// the square root of the budget: up to a 4:1 aspect the whole budget is used, beyond it the capture only<br/>
        /// gets cheaper. A shadow needs a field with room for its blur levels, so turning it on or off allocates too.<br/>
        /// The GPU targets of <see cref="OutlineQuality.Fast"/> come with the first allocation that asks for it on a device<br/>
        /// that runs it, at wake if the profile starts with it.
        /// </summary>
        public bool Allocate(int pixelBudget, bool shadow) {
            budget = Mathf.Max(pixelBudget, MIN_BUDGET);

            var size = Mathf.Min(Mathf.CeilToInt(2 * Mathf.Sqrt(budget)), MAX_SIZE);
            var reallocated = mask == null || mask.Size != size || field.HasShadow != shadow;

            if (reallocated) {
                ReleaseFlood();
                field?.Release();
                mask?.Release();

                mask = new OutlineMask(size);
                field = new OutlineField(size, shadow);
            }

            fast = Performance.Quality is OutlineQuality.Fast && OutlineJumpFlood.IsSupported(field.Format);

            if (fast && flood == null) {
                flood = new OutlineJumpFlood(size, field.Format);
                cmd = new CommandBuffer { name = "Outline2D Flood" };
            }

            return reallocated;
        }

        /// <summary>Builds the GPU programs of the mask and of these outline materials now, not at the first capture.</summary>
        public void Warm(params Material[] outlines) => mask.Warm(outlines);

        /// <summary>New targets: whatever is in flight no longer counts, and a capture is due at once if there is any.</summary>
        public void Restart(bool any) {
            ++version;
            outdated = false;
            hasTargets = any;
            stage = any ? Stage.Capture : Stage.None;
        }

        /// <summary>A setting the capture depends on changed: capture again once the capture in flight has landed.</summary>
        public void MarkOutdated() {
            if (stage is not Stage.None) outdated = true;
        }

        /// <summary>
        /// Captures the source in the owner's local space, its bounds padded by a local distance, and bakes up to three<br/>
        /// shadow blur levels: the silhouette grown by a local distance, then blurred to a softness (the distance it fades<br/>
        /// out over), also local. The buffers must be allocated, for a shadow when there are levels. Immediate, the field<br/>
        /// is ready for <see cref="TryUpload"/> before this returns.
        /// </summary>
        public void Run(IMaskSource source, Component owner, float padding, float cutoff, Vector3 shadowSoftness, Vector3 shadowGrowth, int shadowLevels) {
            using var _ = captureMarker.Auto();

            var worldToLocal = owner.transform.worldToLocalMatrix;

            outdated = false;
            startFrame = Time.frameCount;
            startTime = Time.unscaledTime;

            // A collapsed scale, or nothing left to draw: no outline until the next restart or interval.
            if (!float.IsFinite(padding) || !source.TryGetBounds(worldToLocal, out var min, out var max)) {
                stage = Stage.None;
                return;
            }

            var frame = OutlineFrame.Fit(min, max, padding, budget, mask.Size);
            var captured = version;
            var immediate = Performance.Latency is OutlineLatency.Immediate;

            // A level fades out over about three standard deviations of its Gaussian; both builds work in field texels.
            var sigmas = field.HasShadow ? shadowSoftness / (3 * frame.TexelSize) : Vector3.zero;
            var growths = field.HasShadow ? shadowGrowth / frame.TexelSize : Vector3.zero;
            var levels = field.HasShadow ? Mathf.Clamp(shadowLevels, 0, 3) : 0;

            if (fast) {
                pending = frame;
                floodLevels = (sigmas, growths, levels);
                nextStep = 0;
                stepCount = OutlineJumpFlood.StepCount(frame);
                stage = Stage.Flood;

                cmd.Clear();
                mask.Record(cmd, source, worldToLocal, frame, cutoff);
                flood.Seed(cmd, mask, frame);
                RecordSteps(immediate ? stepCount : StepsPerFrame());

                return;
            }

            // Set first: without asynchronous readback, or immediate, the data comes back before Capture returns.
            stage = Stage.Readback;
            mask.Capture(source, worldToLocal, frame, cutoff, immediate, (data, rowBytes, pixelBytes) => OnMask(owner, captured, frame, sigmas, growths, levels, data, rowBytes, pixelBytes));

            if (immediate && stage is Stage.Transform) field.Complete();
        }

        /// <summary>
        /// Lands the capture in flight once its field is built, or records the next steps of an asynchronous Fast build,<br/>
        /// at most once a frame; true once per capture, when the new outline can show.
        /// </summary>
        public bool TryUpload() {
            if (stage is Stage.Flood) {
                if (nextStep < stepCount && stepFrame != Time.frameCount) {
                    cmd.Clear();
                    RecordSteps(StepsPerFrame());
                }

                if (nextStep < stepCount) return false;
            } else if (stage is not Stage.Transform || !field.TryUpload()) {
                return false;
            }

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
            ReleaseFlood();
            field?.Release();
            mask?.Release();
            field = null;
            mask = null;
        }

        private int StepsPerFrame() => Mathf.CeilToInt(stepCount / (float)FLOOD_FRAMES);

        // Adds up to count steps of the Fast build in flight to what cmd holds, and runs it all.
        private void RecordSteps(int count) {
            using var _ = floodMarker.Auto();

            for (var end = Mathf.Min(nextStep + count, stepCount); nextStep < end; ++nextStep) {
                flood.Step(cmd, nextStep, pending, floodLevels.sigmas, floodLevels.growths, floodLevels.levels, field.Texture);
            }

            stepFrame = Time.frameCount;
            Graphics.ExecuteCommandBuffer(cmd);
        }

        private void ReleaseFlood() {
            flood?.Release();
            cmd?.Release();
            flood = null;
            cmd = null;
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