using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Outline2D {
    /// <summary>
    /// What <see cref="OutlineSprite"/> and <see cref="OutlineImage"/> share: the settings every capture reads, the<br/>
    /// outline material, the capture, and the hidden child each spawns when it wakes to draw through. When to capture,<br/>
    /// how to show the field and what the child holds are the subclass's; only the outlines in this package derive<br/>
    /// from here.
    /// </summary>
    public abstract class Outline : MonoBehaviour, IOutline {
        private const string CHILD = "Outline";

        [SerializeField, Required]
        private Shader shader;

        [SerializeField, FoldoutGroup("Visual")]
        protected Color color = Color.darkOrange;

        // Target texels with alpha above this belong to the silhouette; the targets' own colour is ignored.
        [SerializeField, FoldoutGroup("Visual"), PropertyRange(0, 1)]
        protected float alphaCutoff = .5f;

        // A capture holds about resolution² texels, spread over the group's aspect whatever its size: the one knob for cost.
        // Drawn after the groups a subclass declares.
        [SerializeField, FoldoutGroup("Optimizations", order: 1), PropertyRange(64, 1024)]
        protected int resolution = 256;

        private protected readonly OutlineCapture capture = new();

        private protected Material material;

        private GameObject child;

        // The inputs of the latest capture.
        private (float cutoff, int resolution, float width) lastInputs;

        public Color Color {
            get => color;
            set {
                color = value;
                ApplyRendering();
            }
        }

        // Found by name when the component is set up in the Editor.
        private protected abstract string ShaderName { get; }

        // The width setting a capture pads its frame by, in the subclass's own units.
        private protected abstract float CaptureWidth { get; }

        private (float cutoff, int resolution, float width) CaptureInputs => (alphaCutoff, resolution, CaptureWidth);

        protected virtual void Awake() {
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            capture.Allocate(resolution * resolution);

            // On GLES the first draw with each shader holds the main thread about 6 ms while the render thread builds its
            // program (measured on a PowerVR phone); done here, it lands in loading instead of on the first selection.
            capture.Warm(material);

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

        // Runs the due capture with the current settings, its frame padded by a distance in this transform's units. True
        // when it took a new field texture, which whatever shows the last field does not point at.
        private protected bool RunCapture(IMaskSource source, float padding) {
            lastInputs = CaptureInputs;

            var reallocated = capture.Allocate(resolution * resolution);

            capture.Run(source, this, padding, alphaCutoff);

            return reallocated;
        }

        // Tuning in the inspector while playing: what needs no new capture shows at once; cutoff, resolution and the
        // width's padding take a fresh capture, one at a time however fast a slider moves.
        protected virtual void OnValidate() {
            if (material == null) return;

            ApplyRendering();

            if (lastInputs != CaptureInputs) capture.MarkOutdated();
        }

        protected virtual void Reset() {
            SetupEditor();
        }

        // Last, below the groups a subclass declares, as the inspector had it before this base existed.
        [Button, PropertyOrder(2)]
        protected virtual void SetupEditor() {
            if (shader == null) shader = Shader.Find(ShaderName);
        }
    }
}