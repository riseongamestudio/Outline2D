using System.Collections.Generic;

namespace RiseOn.Outline2D {
    /// <summary>
    /// One outline around the merged silhouette of a group of targets. The silhouette is captured in the outline's<br/>
    /// own local space, so the group must stay rigid relative to the outline until the next call.
    /// </summary>
    public interface IOutline<TTarget> {
        /// <summary>
        /// Outline these targets from now on; null or empty hides the outline. The current outline hides at once<br/>
        /// and the new one shows a few frames later. The sequence is read here and not kept.
        /// </summary>
        void SetTargets(IEnumerable<TTarget> targets);
    }
}