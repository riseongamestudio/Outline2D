using System.Collections.Generic;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Outline2D {
    /// <summary>
    /// Outlines the merged silhouette of a group of Graphics. The Graphic that draws it is spawned hidden under this<br/>
    /// transform when the component wakes, first among its children, so it draws right after this object and takes<br/>
    /// CanvasGroup alpha, Mask and RectMask2D like any Graphic; colour and maskable are set here and handed to it. The<br/>
    /// silhouette is captured in this transform's local space at the end of the canvas update after<br/>
    /// <see cref="SetTargets"/>, once layout has run and every Graphic has rebuilt its mesh, and again as often as the<br/>
    /// performance profile says; the outline shows once the field is built.
    /// </summary>
    [HideMonoScript]
    public class OutlineGraphic : Outline, IOutlineGraphic {
        internal const string OUTLINE_SHADER = "RiseOn/Outline2D/OutlineGraphic";

        private const string CLIP_RECT = "UNITY_UI_CLIP_RECT";

        // The frame's width and height share one float of uv1 as width + height * this; both stay under it, and the sum
        // under 2^24, where a float still holds every integer.
        internal const float FRAME_PACK = 4096;

        // In units of the root canvas, whatever scale sits between it and this transform. Also the padding each capture
        // reserves, to which the shadow's reach adds while it shows; the shadow is also grown by it.
        [SerializeField, PlainFoldoutGroup("Visual/Outline"), MinValue(0)]
        protected float width = 8;

        // A soft shadow of silhouette and outline together, as if the group were a sticker lifted off the canvas, or a glow
        // spread all around; the checkbox in the Shadow foldout's header.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow", Toggle = nameof(shadowEnabled))]
        protected bool shadowEnabled;

        // Moved one way, or spread all around; the settings of the other kind are hidden.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow"), LabelText("Type")]
        protected OutlineShadowType shadowType;

        // Its alpha is scaled by the outline colour's, so fading the outline fades the shadow too.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow"), LabelText("Color")]
        protected Color shadowColor = new(0, 0, 0, .5f);

        // The way the shadow falls along the root canvas's axes, the screen's when the canvas is not rotated, whatever this
        // object's rotation.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow"), LabelText("Direction"), ShowIf(nameof(IsDirectional))]
        protected DirectionExt shadowDirection = new(Direction.Down);

        // Distance the shadow falls, in units of the root canvas.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow"), LabelText("Distance"), MinValue(0), ShowIf(nameof(IsDirectional))]
        protected float shadowDistance = 8;

        // Distance the shadow's edge fades out over, in units of the root canvas. Baked at capture as one blur level, so
        // changing it takes a new capture.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow"), LabelText("Softness"), MinValue(0), ShowIf(nameof(IsDirectional))]
        protected float shadowSoftness = 10;

        // Distance a spread shadow reaches past the outline, in units of the root canvas; part of the padding, so changing
        // it takes a new capture.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow"), LabelText("Spread"), MinValue(0), ShowIf(nameof(IsSpread))]
        protected float shadowSpread = 16;

        // Masks and RectMask2D above this object clip the outline like the Graphics they clip.
        [SerializeField, PlainFoldoutGroup("Visual/Advanced")]
        protected bool maskable = true;

        private readonly GraphicSource source = new();

        private OutlineCanvasGraphic graphic;
        private bool visible;
        private bool quadBuilt;
        private float shownRadius;
        private Vector3 shownShadow;

        public bool Maskable {
            get => maskable;
            set {
                maskable = value;
                ApplyRendering();
            }
        }

        // Turning the shadow on or off takes a new capture, into a field with the blur or without it.
        public bool ShadowEnabled {
            get => shadowEnabled;
            set {
                shadowEnabled = value;
                ApplySettings();
            }
        }

        public Color ShadowColor {
            get => shadowColor;
            set {
                shadowColor = value;
                ApplySettings();
            }
        }

        public DirectionExt ShadowDirection {
            get => shadowDirection;
            set {
                shadowDirection = value;
                ApplySettings();
            }
        }

        public float Width {
            get => width;
            set {
                width = value;
                ApplySettings();
            }
        }

        // Switching between the kinds takes a new capture: only a directional shadow bakes a blur level.
        public OutlineShadowType ShadowType {
            get => shadowType;
            set {
                shadowType = value;
                ApplySettings();
            }
        }

        public float ShadowSpread {
            get => shadowSpread;
            set {
                shadowSpread = value;
                ApplySettings();
            }
        }

        public float ShadowDistance {
            get => shadowDistance;
            set {
                shadowDistance = value;
                ApplySettings();
            }
        }

        // A new softness is baked by the next capture, which is due once this one has landed.
        public float ShadowSoftness {
            get => shadowSoftness;
            set {
                shadowSoftness = value;
                ApplySettings();
            }
        }

        internal Texture FieldTexture => capture.Texture;
        internal Material OutlineMaterial => material;

        private protected override string ShaderName => OUTLINE_SHADER;
        private bool HasShadow => shadowEnabled;
        private bool IsDirectional => shadowType is OutlineShadowType.Directional;
        private bool IsSpread => shadowType is OutlineShadowType.Spread;

        // The frame pads every side alike, by the ring's width or, while the shadow shows, its reach: a directional
        // one's distance and softness past the outline it is grown by, or a spread one's reach.
        private protected override float CaptureWidth =>
            !HasShadow ? width : IsSpread ? width + shadowSpread : width + shadowDistance + shadowSoftness;

        private protected override OutlineShadowType? ShadowVariant => HasShadow ? shadowType : null;

        // A canvas does not zoom, so one blur level, the silhouette grown by the width and blurred to the softness, is all
        // a directional shadow needs.
        private protected override int BlurLevels => HasShadow && IsDirectional ? 1 : 0;
        private protected override Vector3 BlurSoftness => BlurLevels > 0 ? Vector3.one * shadowSoftness : Vector3.zero;
        private protected override Vector3 BlurGrowth => BlurLevels > 0 ? Vector3.one * width : Vector3.zero;

        // Under a RectMask2D the canvas draws the clip variant, which has a GPU program of its own to build.
        private protected override void Warm() {
            base.Warm();

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

        public virtual void SetTargets(IEnumerable<Graphic> targets) {
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
            // A quad still built while nothing should show, after a capture took a new texture, is emptied too.
            if (landed || capture.IsDue || visible != quadBuilt || visible && (Radius() != shownRadius || Shadow() != shownShadow)) {
                graphic.SetVerticesDirty();
            }
        }

        // The last step of the canvas update: layout has run and every Graphic rebuilt this frame has handed its
        // CanvasRenderer the mesh the capture draws.
        internal void OnCanvasUpdated() {
            if (!capture.IsDue) return;

            // Marking the material dirty is refused inside the canvas update, so a new texture goes straight to the renderer.
            // A new texture has nothing in it until this capture lands, so the outline hides until then.
            if (RunCapture(source, CanvasToLocal())) {
                graphic.canvasRenderer.SetTexture(capture.Texture);
                visible = false;
            }

            // An immediate capture has landed already. The canvas refuses new dirt this late, but has not batched yet, so
            // the quad is rebuilt straight away and shows in this frame.
            if (!capture.TryUpload()) return;

            visible = true;
            graphic.RebuildGeometry();
        }

        // One quad over the field. The ring's radius and the field's size ride in uv0.zw, the shadow's offset, softness
        // and the frame's size in uv1, its colour in uv2 (in the colour space the shader works in), so the material stays the
        // same.
        internal void PopulateMesh(VertexHelper vh) {
            quadBuilt = visible;

            if (!visible) return;

            capture.GetQuad(out var position, out var uv);

            shownRadius = Radius();
            shownShadow = Shadow();

            // The canvas strips uv1 and uv2 unless its root asks for them, as TextMesh Pro asks for the channels it uses.
            if (HasShadow && graphic.canvas != null) {
                var root = graphic.canvas.rootCanvas;
                const AdditionalCanvasShaderChannels CHANNELS = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;

                if ((root.additionalShaderChannels & CHANNELS) != CHANNELS) root.additionalShaderChannels |= CHANNELS;
            }

            var frame = capture.Frame;
            var vertex = new UIVertex {
                color = graphic.color
              , uv1 = new Vector4(shownShadow.x, shownShadow.y, shownShadow.z, frame.Width + frame.Height * FRAME_PACK)
              , uv2 = (Vector4)(QualitySettings.activeColorSpace == ColorSpace.Linear ? shadowColor.linear : shadowColor)
            };

            var size = capture.Texture.width;

            Add(vh, ref vertex, new Vector3(position.xMin, position.yMin), new Vector4(uv.xMin, uv.yMin, shownRadius, size));
            Add(vh, ref vertex, new Vector3(position.xMin, position.yMax), new Vector4(uv.xMin, uv.yMax, shownRadius, size));
            Add(vh, ref vertex, new Vector3(position.xMax, position.yMax), new Vector4(uv.xMax, uv.yMax, shownRadius, size));
            Add(vh, ref vertex, new Vector3(position.xMax, position.yMin), new Vector4(uv.xMax, uv.yMin, shownRadius, size));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);

            static void Add(VertexHelper vh, ref UIVertex vertex, Vector3 position, Vector4 uv0) {
                vertex.position = position;
                vertex.uv0 = uv0;
                vh.AddVert(vertex);
            }
        }

        // A zero rect on this transform's origin, so that the child's local space is this one's.
        private protected override void Spawn() {
            var rect = (RectTransform)SpawnChild(typeof(RectTransform)).transform;
            var pivot = transform is RectTransform parent ? parent.pivot : new Vector2(.5f, .5f);

            rect.SetAsFirstSibling();
            rect.anchorMin = rect.anchorMax = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            graphic = rect.gameObject.AddComponent<OutlineCanvasGraphic>();
            graphic.raycastTarget = false;
            graphic.Bind(this);
        }

        // What lives on the spawned Graphic; the colour tints the white ring the shader draws, as a Graphic's colour does.
        private protected override void ApplyRendering() {
            if (graphic == null) return;

            graphic.color = color;

            // The shadow rides in the vertices, so a change to it is a rebuild; only turning it on or off takes a new
            // material, for the Graphic to hand on (and a Mask above to copy anew).
            graphic.SetVerticesDirty();

            if (ApplyShadowKeywords()) graphic.SetMaterialDirty();

            if (graphic.maskable == maskable) return;

            // Graphic.maskable only redoes the stencil; a RectMask2D above picks the change up at the Graphic's next
            // enable or reparent (measured), so its clipping is redone here.
            graphic.maskable = maskable;
            graphic.RecalculateClipping();
        }

        // A directional shadow's offset in field texels, its distance along its direction on the root canvas taken into
        // this transform's space so it falls the same way whatever the rotation, and its softness in field texels; a
        // spread one's reach past the ring in z, capped so it never runs off the capture; zero while the shadow is off.
        private Vector3 Shadow() {
            if (!HasShadow || graphic.canvas == null) return Vector3.zero;

            var texel = capture.Frame.TexelSize;

            if (IsSpread) return new Vector3(0, 0, Mathf.Min(shadowSpread * CanvasToLocal() / texel, Mathf.Max(capture.Frame.MaxRadius - Radius(), 0)));

            var root = graphic.canvas.rootCanvas.transform;
            var offset = (Vector2)transform.InverseTransformVector(root.TransformVector(shadowDirection.ToVector2() * shadowDistance));

            return new Vector3(offset.x / texel, offset.y / texel, shadowSoftness * CanvasToLocal() / texel);
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