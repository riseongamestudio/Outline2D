using System.Collections.Generic;
using UnityEngine;

namespace RiseOn.Outline2D {
    /// <summary>An outline around the merged silhouette of a group of sprites, sorted and masked like a sprite.</summary>
    public interface IOutlineSprite : IOutline {
        /// <summary>Sorting layer the outline draws in, by ID as a renderer's.</summary>
        int SortingLayerID { get; set; }

        /// <summary>Order within the sorting layer, as a renderer's.</summary>
        int SortingOrder { get; set; }

        /// <summary>How the outline interacts with SpriteMasks, as a sprite's.</summary>
        SpriteMaskInteraction MaskInteraction { get; set; }

        /// <summary>
        /// Outline these renderers from now on; null or empty hides the outline. The current outline hides at once<br/>
        /// and the new one shows a few frames later. The sequence is read here and not kept.
        /// </summary>
        void SetTargets(IEnumerable<SpriteRenderer> targets);
    }
}