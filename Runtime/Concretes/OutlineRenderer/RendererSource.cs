using UnityEngine;
using UnityEngine.Rendering;

namespace RiseOn.Outline2D {
    /// <summary>
    /// Renderers drawn through themselves, so their geometry is theirs, a sprite's flipX / flipY and a mesh renderer's<br/>
    /// latest mesh alike. A sprite hands its texture over itself; any other renderer draws each submesh with the main<br/>
    /// texture of the material it has there, set globally just before, as the mask material's own _MainTex would<br/>
    /// hide a per-draw value.
    /// </summary>
    internal sealed class RendererSource : MaskSource<Renderer> {
        private const int SPRITE_PASS = 0;
        private const int TEXTURE_PASS = 1;

        private static readonly int sourceTexId = Shader.PropertyToID("_Outline2DSourceTex");

        protected override bool TryGetLocalBounds(Renderer target, out Bounds bounds) {
            bounds = target.localBounds;

            return target.enabled && target.gameObject.activeInHierarchy && (target is not SpriteRenderer sprite || sprite.sprite != null);
        }

        protected override void DrawTarget(CommandBuffer cmd, Material material, Renderer target) {
            if (target is SpriteRenderer sprite) {
                if (sprite.sprite != null) cmd.DrawRenderer(target, material, 0, SPRITE_PASS);
                return;
            }

            var materials = target.sharedMaterials;
            for (var i = 0; i < materials.Length; ++i) {
                var texture = materials[i] != null ? materials[i].mainTexture : null;
                if (texture == null) continue;

                cmd.SetGlobalTexture(sourceTexId, texture);
                cmd.DrawRenderer(target, material, i, TEXTURE_PASS);
            }
        }
    }
}