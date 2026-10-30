using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiseOn.Outline2D {
    /// <summary>
    /// What <see cref="OutlineRenderer"/> and <see cref="OutlineGraphic"/> share: the settings every capture reads, the<br/>
    /// outline material, the capture, and the hidden child each spawns when it wakes to draw through. When to capture,<br/>
    /// how to show the field and what the child holds are the subclass's; only the outlines in this package derive<br/>
    /// from here.
    /// </summary>
    public abstract class Outline : MonoBehaviour, IOutline {
        private const string CHILD = "Outline";

        // Ahead of Visual, whose order is its first members' -1.
        [SerializeField, Required, InlineButton(nameof(UseDefaultShader), "Default"), PropertyOrder(-2)]
        private Shader shader;

        [SerializeField, PlainFoldoutGroup("Visual/Outline")]
        protected Color color = Color.darkOrange;

        // Target texels with alpha above this belong to the silhouette; the targets' own colour is ignored. The outline
        // and the shadow both follow the silhouette, so it heads the Visual group, above both.
        [SerializeField, FoldoutGroup("Visual"), PropertyOrder(-1), PropertyRange(0, 1)]
        protected float alphaCutoff = .5f;

        // What captures cost and when they run. Drawn after the groups a subclass declares.
        [SerializeField, FoldoutGroup("Performance", order: 1), InlineProperty, HideLabel]
        protected OutlinePerformanceProfile performance = OutlinePerformanceProfile.Default;

        // The outline shaders compile each kind of shadow in only under its keyword, so an outline without one draws at
        // the cost of the ring alone.
        private const string DIRECTIONAL_KEYWORD = "_SHADOW";
        private const string SPREAD_KEYWORD = "_SHADOW_SPREAD";

        private protected readonly OutlineCapture capture = new();

        private protected Material material;

        private GameObject child;

        // The inputs of the latest capture.
        private (float cutoff, int resolution, OutlineQuality quality, float width, Vector3 shadowSoftness, Vector3 shadowGrowth, int shadowLevels) lastInputs;

        public Color Color {
            get => color;
            set {
                color = value;
                ApplyRendering();
            }
        }

        // Resolution and quality take a new capture; frequency and latency apply from the next one.
        public OutlinePerformanceProfile Performance {
            get => performance;
            set {
                performance = value;
                ApplySettings();
            }
        }

        // The package's own outline shader, found by name for Reset and the Default button.
        private protected abstract string ShaderName { get; }

        // The width setting a capture pads its frame by, in the subclass's own units.
        private protected abstract float CaptureWidth { get; }

        // The shadow the shader draws, null while it is off.
        private protected abstract OutlineShadowType? ShadowVariant { get; }

        // How many blur levels of the silhouette a capture bakes for a directional shadow, 0 for any other; for each, how
        // far the silhouette is grown first (the outline's width, so the shadow is the whole sticker's) and how soft the
        // blur is, in the subclass's own units. Levels past the count are ignored.
        private protected abstract int BlurLevels { get; }
        private protected abstract Vector3 BlurSoftness { get; }
        private protected abstract Vector3 BlurGrowth { get; }

        private (float cutoff, int resolution, OutlineQuality quality, float width, Vector3 shadowSoftness, Vector3 shadowGrowth, int shadowLevels) CaptureInputs =>
            (alphaCutoff, performance.Resolution, performance.Quality, CaptureWidth, BlurSoftness, BlurGrowth, BlurLevels);

        protected virtual void Awake() {
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            capture.Performance = performance;
            capture.Allocate(performance.Resolution * performance.Resolution, BlurLevels > 0);
            SetShadowKeywords(material, ShadowVariant);

            Warm();

            Spawn();
            ApplyRendering();
        }

        protected virtual void OnDestroy() {
            capture.Release();

            OutlineMask.SafeDestroy(child);
            OutlineMask.SafeDestroy(material);
        }

        // Puts what draws the outline on a child made by SpawnChild.
        private protected abstract void Spawn();

        // Hands the settings that need no new capture to the child and the material. Also called before Awake and after
        // OnDestroy, when there is no child to hand them to.
        private protected abstract void ApplyRendering();

        // The drawing is an implementation detail: a hidden child at this transform's origin, on this object's layer at
        // the time, never saved.
        private protected GameObject SpawnChild(Type component) {
            // Instantiate copies hidden children too (measured), so a clone of a woken outline arrives with a copy of its
            // child, still drawing the original's field; the copy goes before this outline spawns its own.
            foreach (Transform existing in transform) {
                var copy = existing.gameObject;

                if (copy.hideFlags != HideFlags.HideAndDontSave || copy.name != CHILD) continue;

                copy.SetActive(false);
                OutlineMask.SafeDestroy(copy);
            }

            child = new GameObject(CHILD, component) {
                hideFlags = HideFlags.HideAndDontSave
              , layer = gameObject.layer
            };

            child.transform.SetParent(transform, false);

            return child;
        }

        // Runs the due capture with the current settings, scaled from the subclass's units into this transform's by
        // toLocal: the frame padded by the width, the shadow's levels grown and blurred as set. True when it took a new
        // field texture, which whatever shows the last field does not point at.
        private protected bool RunCapture(IMaskSource source, float toLocal) {
            lastInputs = CaptureInputs;

            var reallocated = capture.Allocate(performance.Resolution * performance.Resolution, lastInputs.shadowLevels > 0);

            capture.Run(source, this, lastInputs.width * toLocal, alphaCutoff
              , lastInputs.shadowSoftness * toLocal, lastInputs.shadowGrowth * toLocal, lastInputs.shadowLevels);

            return reallocated;
        }

        // Tuning in the inspector while playing: what needs no new capture shows at once; cutoff, resolution, quality,
        // the padding, turning the shadow on or off, its softness and, while it shows, the width take a fresh capture, one
        // at a time however fast a slider moves.
        protected virtual void OnValidate() {
            if (material == null) return;

            ApplySettings();
        }

        // A setting changed: what needs no new capture shows at once, the rest once the capture in flight has landed.
        private protected void ApplySettings() {
            ApplyRendering();

            capture.Performance = performance;

            if (material != null && lastInputs != CaptureInputs) capture.MarkOutdated();
        }

        // On GLES the first draw with each shader holds the main thread about 6 ms while the render thread builds its
        // program (measured on a PowerVR phone); done in Awake, it lands in loading instead of on the first selection.
        private protected virtual void Warm() => capture.Warm(material);

        // Puts the outline on a material of the variant for its shadow, or for none; true when that took a new material,
        // which whatever draws the outline must be handed. A new one rather than the keywords flipped on this one: a Mask
        // keeps its stencil copy for as long as the material under it is the same object. Turning a shadow on warms its
        // variant there and then, as Awake warms the one it starts with.
        private protected bool ApplyShadowKeywords() {
            var variant = ShadowVariant;

            if (material.IsKeywordEnabled(new LocalKeyword(material.shader, DIRECTIONAL_KEYWORD)) == variant is OutlineShadowType.Directional
             && material.IsKeywordEnabled(new LocalKeyword(material.shader, SPREAD_KEYWORD)) == variant is OutlineShadowType.Spread) {
                return false;
            }

            var next = new Material(material) { hideFlags = HideFlags.HideAndDontSave };

            SetShadowKeywords(next, variant);
            OutlineMask.SafeDestroy(material);
            material = next;

            if (variant != null) Warm();

            return true;
        }

        private static void SetShadowKeywords(Material target, OutlineShadowType? variant) {
            target.SetKeyword(new LocalKeyword(target.shader, DIRECTIONAL_KEYWORD), variant is OutlineShadowType.Directional);
            target.SetKeyword(new LocalKeyword(target.shader, SPREAD_KEYWORD), variant is OutlineShadowType.Spread);
        }

        protected virtual void Reset() {
            UseDefaultShader();
        }

        private void UseDefaultShader() {
            shader = Shader.Find(ShaderName);
        }
    }
}