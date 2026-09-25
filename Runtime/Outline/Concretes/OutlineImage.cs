using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Outline2D {
    /// <summary>
    /// Outlines the merged silhouette of a group of Images as a Graphic of its own: it sorts by hierarchy and takes<br/>
    /// its colour from <see cref="Graphic.color"/>, CanvasGroup alpha, Mask and RectMask2D like any Image. The<br/>
    /// silhouette is captured in this transform's local space at the end of the canvas update after<br/>
    /// <see cref="SetTargets"/>, once layout has run and every Image has rebuilt its mesh; the distance transform<br/>
    /// then runs on worker threads and the outline shows once it lands.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class OutlineImage : MaskableGraphic, IOutline<Image> {
        internal const string OUTLINE_SHADER = "RiseOn/Outline2D/OutlineImage";

        private const string CLIP_RECT = "UNITY_UI_CLIP_RECT";

        [SerializeField, Required, FoldoutGroup("References")]
        private Shader outlineShader;

        // Image texels with alpha above this belong to the silhouette; the Image's own colour is ignored.
        [SerializeField, FoldoutGroup("Visual"), PropertyRange(0, 1)]
        protected float alphaCutoff = .5f;

        // In units of the root canvas, whatever scale sits between it and this transform; also the padding each capture reserves.
        [SerializeField, FoldoutGroup("Visual"), MinValue(0)]
        protected float width = 8;

        // A capture holds about resolution² texels, spread over the group's aspect whatever its size: the one knob for cost.
        [SerializeField, FoldoutGroup("Optimizations"), PropertyRange(64, 1024)]
        protected int resolution = 256;

        private readonly ImageSource source = new();
        private readonly OutlineCapture capture = new();

        private Material outlineMaterial;
        private bool visible;
        private float shownRadius;

        // The inputs of the latest capture.
        private (float cutoff, int resolution, float width) lastInputs;

        private (float cutoff, int resolution, float width) CaptureInputs => (alphaCutoff, resolution, width);

        public override Texture mainTexture => capture.Texture != null ? capture.Texture : s_WhiteTexture;

        public override Material defaultMaterial => outlineMaterial != null ? outlineMaterial : base.defaultMaterial;

        protected override void Awake() {
            base.Awake();

            // A Graphic runs in edit mode too; the outline only exists while playing.
            if (!Application.isPlaying) return;

            // The field is hidden in the inspector: an outline never takes input, whatever a runtime AddComponent left there.
            raycastTarget = false;

            outlineMaterial = new Material(outlineShader) { hideFlags = HideFlags.HideAndDontSave };

            capture.Allocate(resolution * resolution);

            // On GLES the first draw with each shader holds the main thread while the render thread builds its program; done
            // here, it lands in loading instead of on the first selection. Under a RectMask2D the canvas draws the clip variant.
            var clipped = new Material(outlineMaterial) { hideFlags = HideFlags.HideAndDontSave };

            clipped.EnableKeyword(CLIP_RECT);
            capture.Warm(outlineMaterial, clipped);
            OutlineMask.SafeDestroy(clipped);
        }

        protected override void OnDestroy() {
            capture.Release();
            source.Release();
            OutlineMask.SafeDestroy(outlineMaterial);

            base.OnDestroy();
        }

        public virtual void SetTargets(IEnumerable<Image> targets) {
            source.Set(targets);
            capture.Restart(source.Count > 0);

            // The old outline belongs to the old group; the new one shows once its field lands.
            visible = false;
            SetVerticesDirty();
        }

        protected virtual void LateUpdate() {
            if (capture.TryUpload()) {
                visible = true;
                SetVerticesDirty();
            } else if (visible && Radius() != shownRadius) {
                // This transform or the canvas has rescaled since the quad was built.
                SetVerticesDirty();
            }
        }

        // The last step of the canvas update: layout has run and every Graphic rebuilt this frame has handed its
        // CanvasRenderer the mesh the capture draws.
        public override void GraphicUpdateComplete() {
            base.GraphicUpdateComplete();

            if (!Application.isPlaying || !capture.IsDue) return;

            lastInputs = CaptureInputs;

            // Marking the material dirty is refused inside the canvas update, so a new texture goes straight to the renderer.
            if (capture.Allocate(resolution * resolution)) canvasRenderer.SetTexture(mainTexture);

            capture.Run(source, this, width * CanvasToLocal(), alphaCutoff);
        }

        // One quad over the field; the ring's radius and the field's size ride in uv0.zw, so the material stays the same.
        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();

            if (!visible) return;

            capture.GetQuad(out var position, out var uv);

            shownRadius = Radius();

            Color32 tint = color;
            var size = capture.Texture.width;

            vh.AddVert(new Vector3(position.xMin, position.yMin), tint, new Vector4(uv.xMin, uv.yMin, shownRadius, size));
            vh.AddVert(new Vector3(position.xMin, position.yMax), tint, new Vector4(uv.xMin, uv.yMax, shownRadius, size));
            vh.AddVert(new Vector3(position.xMax, position.yMax), tint, new Vector4(uv.xMax, uv.yMax, shownRadius, size));
            vh.AddVert(new Vector3(position.xMax, position.yMin), tint, new Vector4(uv.xMax, uv.yMin, shownRadius, size));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
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

        // Field texels from the silhouette's edge to the outline's outer edge, capped so the ring never runs off the capture.
        private float Radius() {
            var frame = capture.Frame;

            return Mathf.Min(width * CanvasToLocal() / frame.TexelSize, frame.MaxRadius);
        }

        // Local units per unit of the root canvas.
        private float CanvasToLocal() {
            if (canvas == null) return 1;

            var root = canvas.rootCanvas.transform.localToWorldMatrix.GetColumn(0).magnitude;
            var own = transform.localToWorldMatrix.GetColumn(0).magnitude;

            return root / own;
        }

#if UNITY_EDITOR
        // Tuning in the inspector while playing: colour and width show at once; cutoff, resolution and the width's
        // padding take a fresh capture, one at a time however fast a slider moves.
        protected override void OnValidate() {
            base.OnValidate();

            if (lastInputs != CaptureInputs) capture.MarkOutdated();
        }

        protected override void Reset() {
            base.Reset();

            raycastTarget = false;
            SetupEditor();
        }
#endif

        [Button]
        protected virtual void SetupEditor() {
            if (outlineShader == null) outlineShader = Shader.Find(OUTLINE_SHADER);
        }
    }
}