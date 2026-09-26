using UnityEngine;

namespace RiseOn.Outline2D {
    /// <summary>
    /// An outline around the merged silhouette of a group of targets, whatever draws it. The silhouette is captured<br/>
    /// in the outline's own local space, so the group must stay rigid relative to the outline until the next call<br/>
    /// that sets its targets.
    /// </summary>
    public interface IOutline {
        /// <summary>Colour of the outline; its alpha is the outline's opacity. Applies at once, no new capture.</summary>
        Color Color { get; set; }
    }
}