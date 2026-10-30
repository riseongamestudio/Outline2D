using System.Collections.Generic;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Outline2D {
    /// <summary>
    /// Outlines the merged silhouette of a group of renderers, sprites or meshes. The SpriteRenderer that draws it is<br/>
    /// spawned hidden under this transform when the component wakes; colour, sorting and mask interaction are set here<br/>
    /// and handed to it. The silhouette is captured in this transform's local space in the LateUpdate after<br/>
    /// <see cref="SetTargets"/>, which runs after constraints have moved it onto the group, and again as often as the<br/>
    /// performance profile says; the outline shows once the field is built.
    /// </summary>
    [HideMonoScript]
    public class OutlineRenderer : Outline, IOutlineRenderer {
        internal const string OUTLINE_SHADER = "RiseOn/Outline2D/OutlineRenderer";

        private static readonly int orthoRangeId = Shader.PropertyToID("_OrthoRange");
        private static readonly int widthRangeId = Shader.PropertyToID("_WidthRange");
        private static readonly int texelSizeId = Shader.PropertyToID("_TexelSize");
        private static readonly int maxRadiusId = Shader.PropertyToID("_MaxRadius");
        private static readonly int fieldSizeId = Shader.PropertyToID("_FieldSize");
        private static readonly int frameSizeId = Shader.PropertyToID("_FrameSize");
        private static readonly int shadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int shadowDirectionId = Shader.PropertyToID("_ShadowDirection");
        private static readonly int shadowDistanceRangeId = Shader.PropertyToID("_ShadowDistanceRange");
        private static readonly int shadowSoftnessRangeId = Shader.PropertyToID("_ShadowSoftnessRange");
        private static readonly int shadowLevelsId = Shader.PropertyToID("_ShadowLevels");
        private static readonly int shadowSpreadRangeId = Shader.PropertyToID("_ShadowSpreadRange");

        // Orthographic sizes at which every range below takes its .x and its .y; interpolated between, held outside. One
        // range for the outline and the shadow, so the two keep their proportions at every zoom.
        [SerializeField, FoldoutGroup("Visual"), PropertyOrder(-1)]
        protected Vector2 orthoRange = new(3, 10);

        // World width at the two ends of orthoRange. The larger one is the padding each capture reserves, to which the
        // shadow's reach adds while it shows; the shadow is also grown by the width, so then it takes a new capture.
        [SerializeField, PlainFoldoutGroup("Visual/Outline")]
        protected Vector2 widthRange = new(.05f, .2f);

        // A soft shadow of silhouette and outline together, as if the group were a sticker lifted off the scene; the
        // checkbox in the Shadow foldout's header.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow", Toggle = nameof(shadowEnabled))]
        protected bool shadowEnabled;

        // Moved one way, or spread all around; the settings of the other kind are hidden.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow"), LabelText("Type")]
        protected OutlineShadowType shadowType;

        // Its alpha is scaled by the outline colour's, so fading the outline fades the shadow too.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow"), LabelText("Color")]
        protected Color shadowColor = new(0, 0, 0, .5f);

        // The way the shadow falls along the world axes, the screen's when the camera is not rotated, whatever the group's
        // rotation or flip.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow"), LabelText("Direction"), ShowIf(nameof(IsDirectional))]
        protected DirectionExt shadowDirection = new(Direction.Down);

        // World distance the shadow falls at the two ends of orthoRange.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow"), LabelText("Distance Range"), ShowIf(nameof(IsDirectional))]
        protected Vector2 shadowDistanceRange = new(.12f, .48f);

        // World distance the shadow's edge fades out over at the two ends of orthoRange. Baked at capture as three blur
        // levels, at the two ends of orthoRange and one zoom between, so changing it takes a new capture.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow"), LabelText("Softness Range"), MinValue(0), ShowIf(nameof(IsDirectional))]
        protected Vector2 shadowSoftnessRange = new(.12f, .48f);

        // World distance a spread shadow reaches past the outline at the two ends of orthoRange; part of the padding, so
        // changing it takes a new capture.
        [SerializeField, PlainFoldoutGroup("Visual/Shadow"), LabelText("Spread Range"), MinValue(0), ShowIf(nameof(IsSpread))]
        protected Vector2 shadowSpreadRange = new(.15f, .6f);

        [SerializeField, PlainFoldoutGroup("Visual/Advanced"), ValueDropdown(nameof(SortingLayers))]
        protected int sortingLayerID;

        // Renderers keep the order in 16 bits and wrap anything outside; Unity's own inspector clamps, so this one does too.
        [SerializeField, PlainFoldoutGroup("Visual/Advanced"), MinValue(short.MinValue), MaxValue(short.MaxValue)]
        protected int sortingOrder;

        [SerializeField, PlainFoldoutGroup("Visual/Advanced")]
        protected SpriteMaskInteraction maskInteraction;

        private readonly RendererSource source = new();

        private SpriteRenderer spriteRenderer;
        private Sprite sprite;
        private OutlineFrame spriteFrame;
        private bool shown;

        // Where between the ends of orthoRange the capture in flight bakes its middle blur level, and in w whether it
        // bakes any, handed to the shader with the field that holds them.
        private Vector4 capturedLevels;

        public int SortingLayerID {
            get => sortingLayerID;
            set {
                sortingLayerID = value;
                ApplyRendering();
            }
        }

        public int SortingOrder {
            get => sortingOrder;
            set {
                sortingOrder = value;
                ApplyRendering();
            }
        }

        public SpriteMaskInteraction MaskInteraction {
            get => maskInteraction;
            set {
                maskInteraction = value;
                ApplyRendering();
            }
        }

        public Vector2 OrthoRange {
            get => orthoRange;
            set {
                orthoRange = value;
                ApplySettings();
            }
        }

        public Vector2 WidthRange {
            get => widthRange;
            set {
                widthRange = value;
                ApplySettings();
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

        // Switching between the kinds takes a new capture: only a directional shadow bakes blur levels.
        public OutlineShadowType ShadowType {
            get => shadowType;
            set {
                shadowType = value;
                ApplySettings();
            }
        }

        public Vector2 ShadowSpreadRange {
            get => shadowSpreadRange;
            set {
                shadowSpreadRange = value;
                ApplySettings();
            }
        }

        public DirectionExt ShadowDirection {
            get => shadowDirection;
            set {
                shadowDirection = value;
                ApplyRendering();
            }
        }

        public Vector2 ShadowDistanceRange {
            get => shadowDistanceRange;
            set {
                shadowDistanceRange = value;
                ApplySettings();
            }
        }

        // A new softness is baked by the next capture, which is due once this one has landed.
        public Vector2 ShadowSoftnessRange {
            get => shadowSoftnessRange;
            set {
                shadowSoftnessRange = value;
                ApplySettings();
            }
        }

        private protected override string ShaderName => OUTLINE_SHADER;

        private bool HasShadow => shadowEnabled;
        private bool IsDirectional => shadowType is OutlineShadowType.Directional;
        private bool IsSpread => shadowType is OutlineShadowType.Spread;

        // The ring's width is picked in the shader for the zoom; the larger end is what a capture must make room for, plus
        // the shadow's farthest reach while it shows: a directional one's distance and softness, past the outline it is
        // grown by, or a spread one's reach. The frame pads every side alike, so a directional shadow's far side costs
        // texels too.
        private protected override float CaptureWidth {
            get {
                var width = Mathf.Max(widthRange.x, widthRange.y);

                if (!HasShadow) return width;
                if (IsSpread) return width + Mathf.Max(shadowSpreadRange.x, shadowSpreadRange.y);

                return width + Mathf.Max(shadowDistanceRange.x, shadowDistanceRange.y) + Mathf.Max(shadowSoftnessRange.x, shadowSoftnessRange.y);
            }
        }

        private protected override OutlineShadowType? ShadowVariant => HasShadow ? shadowType : null;

        private protected override int BlurLevels => HasShadow && IsDirectional ? 3 : 0;

        // The three blur levels are three zooms: the two ends of orthoRange and one between, each grown by the outline's
        // width and blurred to the shadow's softness at that zoom, for the shader to blend by zoom. The one between is
        // where the softness is the geometric mean of the ends, so that neighbouring levels are at most twice apart in
        // softness for a range up to 4:1, which keeps them close where the shader's logit blend is only approximate
        // (corners, thin parts); the sharper end is taken as no less than a sixteenth of the softer for the mean, so an
        // end at 0 still gets a level between.
        private float MiddleZoom {
            get {
                var near = shadowSoftnessRange.x;
                var far = shadowSoftnessRange.y;

                if (Mathf.Approximately(near, far)) return .5f;

                var softest = Mathf.Max(near, far);
                var mean = Mathf.Sqrt(Mathf.Max(Mathf.Min(near, far), softest / 16) * softest);

                return Mathf.Clamp01((mean - near) / (far - near));
            }
        }

        private protected override Vector3 BlurSoftness => BlurLevels > 0
            ? new Vector3(shadowSoftnessRange.x, Mathf.Lerp(shadowSoftnessRange.x, shadowSoftnessRange.y, MiddleZoom), shadowSoftnessRange.y)
            : Vector3.zero;

        private protected override Vector3 BlurGrowth => BlurLevels > 0
            ? new Vector3(widthRange.x, Mathf.Lerp(widthRange.x, widthRange.y, MiddleZoom), widthRange.y)
            : Vector3.zero;

        protected virtual void OnEnable() {
            if (spriteRenderer != null) spriteRenderer.enabled = shown;
        }

        protected virtual void OnDisable() {
            if (spriteRenderer != null) spriteRenderer.enabled = false;
        }

        protected override void OnDestroy() {
            base.OnDestroy();

            OutlineMask.SafeDestroy(sprite);
        }

        public virtual void SetTargets(IEnumerable<Renderer> targets) {
            source.Set(targets);
            capture.Restart(source.Count > 0);

            // The old outline belongs to the old group; the new one shows once its field lands.
            Hide();
        }

        protected virtual void LateUpdate() {
            // A capture in flight is never restarted: an inspector change waits for it to land, then takes the latest values.
            // An immediate one lands right away, so it shows in this frame.
            if (capture.IsDue) Capture(this);
            if (capture.TryUpload()) Show(this);

            // LateUpdate runs after this frame's constraints, so the transform already sits on the group. A new field
            // texture leaves the shown sprite pointing at the old one, so it hides until the next lands.
            static void Capture(OutlineRenderer context) {
                var worldPerLocal = context.transform.localToWorldMatrix.GetColumn(0).magnitude;

                context.capturedLevels = new Vector4(context.MiddleZoom, 0, 0, context.BlurLevels > 0 ? 1 : 0);

                if (context.RunCapture(context.source, 1 / worldPerLocal)) context.Hide();
            }

            // A sprite over the frame's texels from the first centre to the last: its corners sit on texel centres, so
            // bilinear sampling never reaches the texels outside this frame. A rect that starts half a texel in costs a
            // few microseconds, where OverrideGeometry for the same corners took about 190 (measured in the Editor).
            // One pixel per texel, and the pivot on this transform's origin so that the sprite's local space is this
            // transform's; pivot and pixels per unit only exist at creation, so each capture over a new frame makes its own.
            static void Show(OutlineRenderer context) {
                var frame = context.capture.Frame;

                if (context.sprite == null || context.sprite.texture != context.capture.Texture || !frame.Matches(context.spriteFrame)) {
                    CreateSprite(context, frame);
                }

                context.material.SetFloat(fieldSizeId, context.capture.Texture.width);
                context.material.SetFloat(texelSizeId, frame.TexelSize);
                context.material.SetFloat(maxRadiusId, frame.MaxRadius);
                context.material.SetVector(frameSizeId, new Vector2(frame.Width, frame.Height));
                context.material.SetVector(shadowLevelsId, context.capturedLevels);

                context.shown = true;
                context.spriteRenderer.enabled = true;
            }

            static void CreateSprite(OutlineRenderer context, in OutlineFrame frame) {
                var span = new Vector2(frame.Width - 1, frame.Height - 1);

                var sprite = Sprite.Create(
                    context.capture.Texture
                  , new Rect(new Vector2(.5f, .5f), span)
                  , -(frame.Origin / frame.TexelSize + new Vector2(.5f, .5f)) / span
                  , 1 / frame.TexelSize
                  , 0
                  , SpriteMeshType.FullRect
                  , Vector4.zero
                  , false);

                sprite.name = "Outline";
                sprite.hideFlags = HideFlags.HideAndDontSave;

                context.spriteRenderer.sprite = sprite;
                OutlineMask.SafeDestroy(context.sprite);
                context.sprite = sprite;
                context.spriteFrame = frame;
            }
        }

        private protected override void Spawn() {
            spriteRenderer = SpawnChild(typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            spriteRenderer.sharedMaterial = material;
            spriteRenderer.enabled = false;
        }

        // The zoom range goes to the material, the rest to the spawned renderer; the colour tints the white ring the
        // shader draws, as a sprite's colour does.
        private protected override void ApplyRendering() {
            if (spriteRenderer == null) return;

            if (ApplyShadowKeywords()) spriteRenderer.sharedMaterial = material;

            material.SetVector(orthoRangeId, orthoRange);
            material.SetVector(widthRangeId, widthRange);
            material.SetColor(shadowColorId, shadowColor);
            material.SetVector(shadowDirectionId, shadowDirection.ToVector2());
            material.SetVector(shadowDistanceRangeId, shadowDistanceRange);
            material.SetVector(shadowSoftnessRangeId, shadowSoftnessRange);
            material.SetVector(shadowSpreadRangeId, shadowSpreadRange);

            spriteRenderer.color = color;
            spriteRenderer.sortingLayerID = sortingLayerID;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.maskInteraction = maskInteraction;
        }

        private void Hide() {
            shown = false;

            if (spriteRenderer != null) spriteRenderer.enabled = false;
        }

        private static IEnumerable<ValueDropdownItem<int>> SortingLayers() {
            foreach (var layer in SortingLayer.layers) yield return new ValueDropdownItem<int>(layer.name, layer.id);
        }
    }
}