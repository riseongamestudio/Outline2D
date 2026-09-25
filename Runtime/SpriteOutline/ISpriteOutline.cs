using System.Collections.Generic;
using UnityEngine;

namespace RiseOn.SpriteOutline {
    /// <summary>
    /// One outline around the merged silhouette of a group of sprites. The silhouette is captured in the<br/>
    /// outliner's own local space, so the group must stay rigid relative to the outliner until the next call.
    /// </summary>
    public interface ISpriteOutline {
        /// <summary>
        /// Outline these renderers from now on; null or empty hides the outline. The current outline hides at<br/>
        /// once and the new one shows a few frames later. The sequence is read here and not kept.
        /// </summary>
        void SetTargets(IEnumerable<SpriteRenderer> renderers);
    }
}