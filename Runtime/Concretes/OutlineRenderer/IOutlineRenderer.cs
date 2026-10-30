using System.Collections.Generic;
using RiseOn.Utils;
using UnityEngine;

namespace RiseOn.Outline2D {
    /// <summary>
    /// An outline around the merged silhouette of a group of renderers, sprites or meshes, sorted and masked like a<br/>
    /// sprite, over a soft shadow, moved one way or spread all around. The width and the shadow's lengths all follow one<br/>
    /// orthographic range.
    /// </summary>
    public interface IOutlineRenderer : IOutline {
        /// <summary>Sorting layer the outline draws in, by ID as a renderer's.</summary>
        int SortingLayerID { get; set; }

        /// <summary>Order within the sorting layer, as a renderer's.</summary>
        int SortingOrder { get; set; }

        /// <summary>How the outline interacts with SpriteMasks, as a sprite's.</summary>
        SpriteMaskInteraction MaskInteraction { get; set; }

        /// <summary>
        /// Orthographic sizes at which every range below takes its .x and its .y; interpolated between, held outside.<br/>
        /// Applies at once: the shadow's baked levels are the softness range's ends and a point between, not sizes.
        /// </summary>
        Vector2 OrthoRange { get; set; }

        /// <summary>
        /// World width of the outline at the two ends of the orthographic range. Shows at once; the larger end is<br/>
        /// the capture's padding, so changing it also takes a new capture.
        /// </summary>
        Vector2 WidthRange { get; set; }

        /// <summary>Whether the shadow shows. Turning it on or off takes a new capture.</summary>
        bool ShadowEnabled { get; set; }

        /// <summary>Colour of the shadow; its alpha is the shadow's opacity. Applies at once.</summary>
        Color ShadowColor { get; set; }

        /// <summary>Moved one way or spread all around. Changing it takes a new capture.</summary>
        OutlineShadowType ShadowType { get; set; }

        /// <summary>
        /// The way a directional shadow falls along the world axes, the screen's when the camera is not rotated,<br/>
        /// whatever the group's rotation or flip. Applies at once.
        /// </summary>
        DirectionExt ShadowDirection { get; set; }

        /// <summary>
        /// World distance a directional shadow falls at the two ends of the orthographic range. Shows at once; the<br/>
        /// larger end is part of the capture's padding while the shadow shows, so changing it also takes a new capture.
        /// </summary>
        Vector2 ShadowDistanceRange { get; set; }

        /// <summary>
        /// World distance a directional shadow's edge fades out over at the two ends of the orthographic range. Baked<br/>
        /// into the capture, so it takes a new one.
        /// </summary>
        Vector2 ShadowSoftnessRange { get; set; }

        /// <summary>
        /// World distance a spread shadow reaches past the outline at the two ends of the orthographic range. Shows at<br/>
        /// once; the larger end is part of the capture's padding while the shadow shows, so changing it also takes a<br/>
        /// new capture.
        /// </summary>
        Vector2 ShadowSpreadRange { get; set; }

        /// <summary>
        /// Outline these renderers from now on; null or empty hides the outline. The current outline hides at once<br/>
        /// and the new one shows in the same frame or a few later, as <see cref="IOutline.Performance"/> says. The<br/>
        /// sequence is read here and not kept.
        /// </summary>
        void SetTargets(IEnumerable<Renderer> targets);
    }
}