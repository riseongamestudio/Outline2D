using System;
using Sirenix.OdinInspector;

namespace RiseOn.Outline2D {
    /// <summary>
    /// What an outline spends on its captures and when. Every combination works; they differ only in what they cost and
    /// how soon a change shows. Serialize one per kind of group and hand it to <see cref="IOutline.Performance"/>.
    /// </summary>
    [Serializable]
    public struct OutlinePerformanceProfile {
        /// <summary>A capture holds about Resolution² texels, spread over the group's aspect whatever its size.</summary>
        [PropertyRange(64, 1024)]
        public int Resolution;

        public OutlineFrequency Frequency;

        /// <summary>Frames between the starts of two captures under <see cref="OutlineFrequency.FrameInterval"/>, at least 1.</summary>
        [ShowIf(nameof(Frequency), OutlineFrequency.FrameInterval), MinValue(1)]
        public int Frames;

        /// <summary>Seconds of real time between the starts of two captures under <see cref="OutlineFrequency.TimeInterval"/>.</summary>
        [ShowIf(nameof(Frequency), OutlineFrequency.TimeInterval), MinValue(0), SuffixLabel("s", true)]
        public float Seconds;

        public OutlineQuality Quality;

        public OutlineLatency Latency;

        /// <summary>256, on demand, exact, asynchronous: the outline of a group that stands still.</summary>
        public static OutlinePerformanceProfile Default => new() { Resolution = 256, Frames = 1, Seconds = .1f };
    }
}