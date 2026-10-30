using System.Collections.Generic;
using RiseOn.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Outline2D {
    /// <summary>
    /// An outline around the merged silhouette of a group of UI Graphics, sorted by hierarchy and clipped like a Graphic,<br/>
    /// over a soft shadow, moved one way or spread all around. Lengths are in units of the root canvas.
    /// </summary>
    public interface IOutlineGraphic : IOutline {
        /// <summary>Whether Masks and RectMask2D above the outline clip it, as a Graphic's maskable.</summary>
        bool Maskable { get; set; }

        /// <summary>
        /// Width of the outline. Shows at once; it is the capture's padding, so changing it also takes a new<br/>
        /// capture.
        /// </summary>
        float Width { get; set; }

        /// <summary>Whether the shadow shows. Turning it on or off takes a new capture.</summary>
        bool ShadowEnabled { get; set; }

        /// <summary>Colour of the shadow; its alpha is the shadow's opacity. Applies at once.</summary>
        Color ShadowColor { get; set; }

        /// <summary>Moved one way or spread all around. Changing it takes a new capture.</summary>
        OutlineShadowType ShadowType { get; set; }

        /// <summary>
        /// The way a directional shadow falls along the root canvas's axes, the screen's when the canvas is not<br/>
        /// rotated, whatever the outline's rotation. Applies at once.
        /// </summary>
        DirectionExt ShadowDirection { get; set; }

        /// <summary>
        /// Distance a directional shadow falls. Shows at once; it is part of the capture's padding while the shadow<br/>
        /// shows, so changing it also takes a new capture.
        /// </summary>
        float ShadowDistance { get; set; }

        /// <summary>
        /// Distance a directional shadow's edge fades out over. Baked into the capture, so it takes a new one.
        /// </summary>
        float ShadowSoftness { get; set; }

        /// <summary>
        /// Distance a spread shadow reaches past the outline. Shows at once; it is part of the capture's padding while<br/>
        /// the shadow shows, so changing it also takes a new capture.
        /// </summary>
        float ShadowSpread { get; set; }

        /// <summary>
        /// Outline these Graphics from now on; null or empty hides the outline. The current outline hides at once<br/>
        /// and the new one shows in the same frame or a few later, as <see cref="IOutline.Performance"/> says. The<br/>
        /// sequence is read here and not kept.
        /// </summary>
        void SetTargets(IEnumerable<Graphic> targets);
    }
}