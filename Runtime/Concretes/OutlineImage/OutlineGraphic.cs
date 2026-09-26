using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Outline2D {
    /// <summary>
    /// The Graphic an OutlineImage spawns under itself to draw through the canvas. It keeps no state of its own:<br/>
    /// texture, material, geometry and when to capture are the owner's.
    /// </summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class OutlineGraphic : MaskableGraphic, ILayoutIgnorer {
        private OutlineImage owner;

        // Never a cell of a LayoutGroup on the outline's object.
        public bool ignoreLayout => true;

        public override Texture mainTexture => owner != null && owner.FieldTexture != null ? owner.FieldTexture : s_WhiteTexture;

        public override Material defaultMaterial => owner != null && owner.OutlineMaterial != null ? owner.OutlineMaterial : base.defaultMaterial;

        public void Bind(OutlineImage owner) {
            this.owner = owner;
            SetAllDirty();
        }

        public override void GraphicUpdateComplete() {
            base.GraphicUpdateComplete();

            if (owner != null) owner.OnCanvasUpdated();
        }

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();

            if (owner != null) owner.PopulateMesh(vh);
        }

        // The quad sits around the targets, not on this RectTransform, so only an empty clip rect culls it; a RectMask2D
        // still clips its pixels.
        public override void Cull(Rect clipRect, bool validRect) {
            var cull = !validRect;

            if (canvasRenderer.cull == cull) return;

            canvasRenderer.cull = cull;
            onCullStateChanged.Invoke(cull);
            OnCullingChanged();
        }
    }
}