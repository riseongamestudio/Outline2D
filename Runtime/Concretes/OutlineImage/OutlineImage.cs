using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Outline2D {
    /// <summary>
    /// Outlines the merged silhouette of a group of Images. The Graphic that draws it is spawned hidden under this<br/>
    /// transform when the component wakes, first among its children, so it draws right after this object and takes<br/>
    /// CanvasGroup alpha, Mask and RectMask2D like any Graphic; colour and maskable are set here and handed to it. The<br/>
    /// silhouette is captured in this transform's local space at the end of the canvas update after<br/>
    /// <see cref="SetTargets"/>, once layout has run and every Image has rebuilt its mesh; the distance transform then<br/>
    /// runs on worker threads and the outline shows once it lands.
    /// </summary>
    [HideMonoScript]
    public class OutlineImage : Outline, IOutlineImage {
        internal const string OUTLINE_SHADER = "RiseOn/Outline2D/OutlineImage";

        private const string CLIP_RECT = "UNITY_UI_CLIP_RECT";

        // In units of the root canvas, whatever scale sits between it and this transform; also the padding each capture reserves.
        [SerializeField, FoldoutGroup("Visual"), MinValue(0)]
        protected float width = 8;

        // Masks and RectMask2D above this object clip the outline like the Images they clip.
        [SerializeField, FoldoutGroup("Visual/Advanced")]
        protected bool maskable = true;

        private readonly ImageSource source = new();

        private OutlineGraphic graphic;
        private bool visible;
        private float shownRadius;

        public bool Maskable {
            get => maskable;
            set {
                maskable = value;
                ApplyRendering();
            }
        }

        internal Texture FieldTexture => capture.Texture;
        internal Material OutlineMaterial => material;

        private protected override string ShaderName => OUTLINE_SHADER;
        private protected override float CaptureWidth => width;

        protected override void Awake() {
            base.Awake();

            // Under a RectMask2D the canvas draws the clip variant, which has a GPU program of its own to build.
            var clipped = new Material(material) { hideFlags = HideFlags.HideAndDontSave };

            clipped.EnableKeyword(CLIP_RECT);
            capture.Warm(clipped);
            OutlineMask.SafeDestroy(clipped);
        }

        protected virtual void OnEnable() {
            if (graphic != null) graphic.enabled = true;
        }

        protected virtual void OnDisable() {
            if (graphic != null) graphic.enabled = false;
        }

        protected override void OnDestroy() {
            base.OnDestroy();

            source.Release();
        }

        public virtual void SetTargets(IEnumerable<Image> targets) {
            source.Set(targets);
            capture.Restart(source.Count > 0);

            // The old outline belongs to the old group; the new one shows once its field lands.
            visible = false;

            if (graphic != null) graphic.SetVerticesDirty();
        }

        protected virtual void LateUpdate() {
            if (graphic == null) return;

            var landed = capture.TryUpload();

            if (landed) visible = true;

            // A capture only runs inside a canvas update, so a due one needs the Graphic rebuilt, as do a new field and a
            // ring whose radius has changed since the quad was built (this transform or the canvas has rescaled).
            if (landed || capture.IsDue || visible && Radius() != shownRadius) graphic.SetVerticesDirty();
        }

        // The last step of the canvas update: layout has run and every Graphic rebuilt this frame has handed its
        // CanvasRenderer the mesh the capture draws.
        internal void OnCanvasUpdated() {
            if (!capture.IsDue) return;

            // Marking the material dirty is refused inside the canvas update, so a new texture goes straight to the renderer.
            if (RunCapture(source, width * CanvasToLocal())) graphic.canvasRenderer.SetTexture(capture.Texture);
        }

        // One quad over the field; the ring's radius and the field's size ride in uv0.zw, so the material stays the same.
        internal void PopulateMesh(VertexHelper vh) {
            if (!visible) return;

            capture.GetQuad(out var position, out var uv);

            shownRadius = Radius();

            Color32 tint = graphic.color;
            var size = capture.Texture.width;

            vh.AddVert(new Vector3(position.xMin, position.yMin), tint, new Vector4(uv.xMin, uv.yMin, shownRadius, size));
            vh.AddVert(new Vector3(position.xMin, position.yMax), tint, new Vector4(uv.xMin, uv.yMax, shownRadius, size));
            vh.AddVert(new Vector3(position.xMax, position.yMax), tint, new Vector4(uv.xMax, uv.yMax, shownRadius, size));
            vh.AddVert(new Vector3(position.xMax, position.yMin), tint, new Vector4(uv.xMax, uv.yMin, shownRadius, size));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        // A zero rect on this transform's origin, so that the child's local space is this one's.
        private protected override void Spawn() {
            var rect = (RectTransform)SpawnChild(typeof(RectTransform)).transform;
            var pivot = transform is RectTransform parent ? parent.pivot : new Vector2(.5f, .5f);

            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            graphic = rect.gameObject.AddComponent<OutlineGraphic>();
            graphic.raycastTarget = false;
            graphic.Bind(this);
        }

        // What lives on the spawned Graphic; the colour tints the white ring the shader draws, as a Graphic's colour does.
        private protected override void ApplyRendering() {
            if (graphic == null) return;

            graphic.color = color;

            if (graphic.maskable == maskable) return;

            // Graphic.maskable only redoes the stencil; a RectMask2D above picks the change up at the Graphic's next
            // enable or reparent (measured), so its clipping is redone here.
            graphic.maskable = maskable;
            graphic.RecalculateClipping();
        }

        // Field texels from the silhouette's edge to the outline's outer edge, capped so the ring never runs off the capture.
        private float Radius() {
            var frame = capture.Frame;

            return Mathf.Min(width * CanvasToLocal() / frame.TexelSize, frame.MaxRadius);
        }

        // Local units per unit of the root canvas.
        private float CanvasToLocal() {
            var canvas = graphic.canvas;

            if (canvas == null) return 1;

            var root = canvas.rootCanvas.transform.localToWorldMatrix.GetColumn(0).magnitude;
            var own = transform.localToWorldMatrix.GetColumn(0).magnitude;

            return root / own;
        }
    }
}