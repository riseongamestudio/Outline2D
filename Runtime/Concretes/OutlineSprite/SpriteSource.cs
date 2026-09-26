using UnityEngine;
using UnityEngine.Rendering;

namespace RiseOn.Outline2D {
    /// <summary>
    /// Sprites drawn through their own renderer: DrawRenderer hands over the sprite's texture and its geometry<br/>
    /// with flipX / flipY already applied.
    /// </summary>
    internal sealed class SpriteSource : MaskSource<SpriteRenderer> {
        protected override bool TryGetLocalBounds(SpriteRenderer target, out Bounds bounds) {
            bounds = target.localBounds;

            return target.sprite != null;
        }

        protected override void DrawTarget(CommandBuffer cmd, Material material, SpriteRenderer target) {
            if (target.sprite != null) cmd.DrawRenderer(target, material, 0, 0);
        }
    }
}