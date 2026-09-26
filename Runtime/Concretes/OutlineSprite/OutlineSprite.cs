using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Outline2D {
    /// <summary>
    /// Outlines the merged silhouette of a group of sprites. The SpriteRenderer that draws it is spawned hidden under<br/>
    /// this transform when the component wakes; colour, sorting and mask interaction are set here and handed to it.<br/>
    /// The silhouette is captured in this transform's local space in the LateUpdate after <see cref="SetTargets"/>,<br/>
    /// which runs after constraints have moved it onto the group; the distance transform then runs on worker threads<br/>
    /// and the outline shows once it lands.
    /// </summary>
    [HideMonoScript]
    public class OutlineSprite : Outline, IOutlineSprite {
        internal const string OUTLINE_SHADER = "RiseOn/Outline2D/OutlineSprite";

        private static readonly int orthoRangeId = Shader.PropertyToID("_OrthoRange");
        private static readonly int widthRangeId = Shader.PropertyToID("_WidthRange");
        private static readonly int texelSizeId = Shader.PropertyToID("_TexelSize");
        private static readonly int maxRadiusId = Shader.PropertyToID("_MaxRadius");
        private static readonly int fieldSizeId = Shader.PropertyToID("_FieldSize");

        // Orthographic sizes at which the width is widthRange.x and .y; interpolated between, held outside.
        [SerializeField, FoldoutGroup("Visual")]
        protected Vector2 orthoRange = new(3, 10);

        // World width at the two ends of orthoRange; the larger one is also the padding each capture reserves.
        [SerializeField, FoldoutGroup("Visual")]
        protected Vector2 widthRange = new(.05f, .2f);

        [SerializeField, PlainFoldoutGroup("Visual/Advanced"), ValueDropdown(nameof(SortingLayers))]
        protected int sortingLayerID;

        // Renderers keep the order in 16 bits and wrap anything outside; Unity's own inspector clamps, so this one does too.
        [SerializeField, PlainFoldoutGroup("Visual/Advanced"), MinValue(short.MinValue), MaxValue(short.MaxValue)]
        protected int sortingOrder;

        [SerializeField, PlainFoldoutGroup("Visual/Advanced")]
        protected SpriteMaskInteraction maskInteraction;

        private readonly SpriteSource source = new();

        private SpriteRenderer spriteRenderer;
        private Sprite sprite;
        private bool shown;

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

        private protected override string ShaderName => OUTLINE_SHADER;

        // The zoom range only picks a width in the shader; the larger end is what a capture must make room for.
        private protected override float CaptureWidth => Mathf.Max(widthRange.x, widthRange.y);

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

        public virtual void SetTargets(IEnumerable<SpriteRenderer> targets) {
            source.Set(targets);
            capture.Restart(source.Count > 0);

            // The old outline belongs to the old group; the new one shows once its field lands.
            Hide();
        }

        protected virtual void LateUpdate() {
            // A capture in flight is never restarted: an inspector change waits for it to land, then takes the latest values.
            if (capture.IsDue) Capture(this);
            else if (capture.TryUpload()) Show(this);

            // LateUpdate runs after this frame's constraints, so the transform already sits on the group. A new field
            // texture leaves the shown sprite pointing at the old one, so it hides until the next lands.
            static void Capture(OutlineSprite context) {
                var worldPerLocal = context.transform.localToWorldMatrix.GetColumn(0).magnitude;

                if (context.RunCapture(context.source, context.CaptureWidth / worldPerLocal)) context.Hide();
            }

            // A sprite over the frame's texels from the first centre to the last: its corners sit on texel centres, so
            // bilinear sampling never reaches the texels outside this frame. A rect that starts half a texel in costs a
            // few microseconds, where OverrideGeometry for the same corners took about 190 (measured in the Editor).
            // One pixel per texel, and the pivot on this transform's origin so that the sprite's local space is this
            // transform's; pivot and pixels per unit only exist at creation, so each capture makes its own.
            static void Show(OutlineSprite context) {
                var frame = context.capture.Frame;
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

                context.material.SetFloat(fieldSizeId, context.capture.Texture.width);
                context.material.SetFloat(texelSizeId, frame.TexelSize);
                context.material.SetFloat(maxRadiusId, frame.MaxRadius);

                context.shown = true;
                context.spriteRenderer.enabled = true;
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

            material.SetVector(orthoRangeId, orthoRange);
            material.SetVector(widthRangeId, widthRange);

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