using System.Collections.Generic;
using RiseOn.Utils;
using UnityEngine;

namespace RiseOn.Outline2D {
    /// <summary>
    /// An outline around the merged silhouette of a group of sprites, sorted and masked like a sprite, over a soft<br/>
    /// shadow of silhouette and outline together. The width, the shadow's distance and its softness all follow one<br/>
    /// orthographic range.
    /// </summary>
    public interface IOutlineSprite : IOutline {
        /// <summary>Sorting layer the outline draws in, by ID as a renderer's.</summary>
        int SortingLayerID { get; set; }

        /// <summary>Order within the sorting layer, as a renderer's.</summary>
        int SortingOrder { get; set; }

        /// <summary>How the outline interacts with SpriteMasks, as a sprite's.</summary>
        SpriteMaskInteraction MaskInteraction { get; set; }

        /// <summary>
        /// Orthographic sizes at which every range below takes its .x and its .y; interpolated between, held outside.<br/>
        /// The shadow's softness is baked at three zooms of it, so changing it takes a new capture.
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

        /// <summary>
        /// The way the shadow falls along the world axes, the screen's when the camera is not rotated, whatever the<br/>
        /// group's rotation or flip. Applies at once.
        /// </summary>
        DirectionExt ShadowDirection { get; set; }

        /// <summary>
        /// World distance the shadow falls at the two ends of the orthographic range. Shows at once; the larger end<br/>
        /// is part of the capture's padding while the shadow shows, so changing it also takes a new capture.
        /// </summary>
        Vector2 ShadowDistanceRange { get; set; }

        /// <summary>
        /// World distance the shadow's edge fades out over at the two ends of the orthographic range. Baked into the<br/>
        /// capture, so it takes a new one.
        /// </summary>
        Vector2 ShadowSoftnessRange { get; set; }

        /// <summary>
        /// Outline these renderers from now on; null or empty hides the outline. The current outline hides at once<br/>
        /// and the new one shows a few frames later. The sequence is read here and not kept.
        /// </summary>
        void SetTargets(IEnumerable<SpriteRenderer> targets);
    }
}