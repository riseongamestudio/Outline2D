using System.Collections.Generic;
using UnityEngine.UI;

namespace RiseOn.Outline2D {
    /// <summary>
    /// An outline around the merged silhouette of a group of UI Images, sorted by hierarchy and clipped like a Graphic.
    /// </summary>
    public interface IOutlineImage : IOutline {
        /// <summary>Whether Masks and RectMask2D above the outline clip it, as a Graphic's maskable.</summary>
        bool Maskable { get; set; }

        /// <summary>
        /// Outline these Images from now on; null or empty hides the outline. The current outline hides at once<br/>
        /// and the new one shows a few frames later. The sequence is read here and not kept.
        /// </summary>
        void SetTargets(IEnumerable<Image> targets);
    }
}