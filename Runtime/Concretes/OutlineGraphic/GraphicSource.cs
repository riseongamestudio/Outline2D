using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace RiseOn.Outline2D {
    /// <summary>
    /// Graphics drawn with the mesh uGUI last handed their CanvasRenderer, so an Image's Type, Preserve Aspect and Use<br/>
    /// Sprite Mesh, a RawImage's UV Rect or a text's glyphs come out the way the Graphic draws them, on its own texture.
    /// </summary>
    internal sealed class GraphicSource : MaskSource<Graphic> {
        private static readonly int mainTexId = Shader.PropertyToID("_MainTex");

        // Drawn directly, the CanvasRenderer's mesh leaves the mask empty (measured), so each Graphic is drawn from a copy;
        // the copies are kept across captures.
        private readonly List<Mesh> copies = new();
        private readonly CombineInstance[] single = new CombineInstance[1];

        // Created at the first draw: the source is built with its component, where Unity refuses native allocations.
        private MaterialPropertyBlock properties;
        private int drawn;

        public override void Draw(CommandBuffer cmd, Material material) {
            drawn = 0;
            base.Draw(cmd, material);
        }

        public void Release() {
            foreach (var copy in copies) OutlineMask.SafeDestroy(copy);

            copies.Clear();
        }

        protected override bool TryGetLocalBounds(Graphic target, out Bounds bounds) {
            var mesh = GetMesh(target);

            bounds = mesh != null ? mesh.bounds : default;

            return mesh != null;
        }

        protected override void DrawTarget(CommandBuffer cmd, Material material, Graphic target) {
            var mesh = GetMesh(target);

            if (mesh == null) return;

            if (drawn == copies.Count) copies.Add(new Mesh { name = "OutlineGraphicCopy", hideFlags = HideFlags.HideAndDontSave });

            var copy = copies[drawn++];

            single[0].mesh = mesh;
            copy.CombineMeshes(single, true, false);

            // The command buffer copies the block as it records, so one block serves every draw.
            properties ??= new MaterialPropertyBlock();
            properties.SetTexture(mainTexId, target.mainTexture);
            cmd.DrawMesh(copy, target.transform.localToWorldMatrix, material, 0, 0, properties);
        }

        // Null for a Graphic that draws nothing: disabled, or without geometry yet.
        private static Mesh GetMesh(Graphic graphic) {
            var mesh = graphic.isActiveAndEnabled ? graphic.canvasRenderer.GetMesh() : null;

            return mesh != null && mesh.vertexCount > 0 ? mesh : null;
        }
    }
}