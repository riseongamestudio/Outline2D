using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;

namespace RiseOn.SpriteOutline {
    /// <summary>
    /// Outlines the merged silhouette of a group of sprites, drawn by the MeshRenderer on its own GameObject<br/>
    /// so sorting layer and order are set there. The silhouette is captured in this transform's local space in<br/>
    /// the LateUpdate after <see cref="SetTargets"/>, which runs after constraints have moved it onto the group;<br/>
    /// the distance transform then runs on worker threads and the outline shows once it lands.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SpriteOutline : MonoBehaviour, ISpriteOutline {
        internal const string OUTLINE_SHADER = "RiseOn/SpriteOutline/Outline";

        // Above this, squared distances stop being exact in float inside the distance transform.
        private const int MAX_SIZE = 2048;

        private static readonly int fieldTexId = Shader.PropertyToID("_FieldTex");
        private static readonly int colorId = Shader.PropertyToID("_Color");
        private static readonly int orthoRangeId = Shader.PropertyToID("_OrthoRange");
        private static readonly int widthRangeId = Shader.PropertyToID("_WidthRange");
        private static readonly int texelSizeId = Shader.PropertyToID("_TexelSize");
        private static readonly int maxRadiusId = Shader.PropertyToID("_MaxRadius");

        private static readonly ProfilerMarker captureMarker = new("SpriteOutline.Capture");

        [SerializeField, Required, FoldoutGroup("References")]
        private MeshFilter meshFilter;

        [SerializeField, Required, FoldoutGroup("References")]
        private MeshRenderer meshRenderer;

        [SerializeField, Required, FoldoutGroup("References")]
        private Shader outlineShader;

        // Sprite texels with alpha above this belong to the silhouette; the renderer's own color is ignored.
        [SerializeField, FoldoutGroup("Visual"), PropertyRange(0, 1)]
        protected float alphaCutoff = .5f;

        [SerializeField, FoldoutGroup("Visual")]
        protected Color color = Color.darkOrange;

        // Orthographic sizes at which the width is widthRange.x and .y; interpolated between, held outside.
        [SerializeField, FoldoutGroup("Visual")]
        protected Vector2 orthoRange = new(3, 10);

        // World width at the two ends of orthoRange; the larger one is also the padding each capture reserves.
        [SerializeField, FoldoutGroup("Visual")]
        protected Vector2 widthRange = new(.05f, .2f);

        // Texels of one capture, about width x height whatever the group's size or aspect: the one knob for cost.
        [SerializeField, FoldoutGroup("Optimizations"), PropertyRange(16384, 262144)]
        protected int pixelBudget = 65536;
        private readonly List<SpriteRenderer> targets = new();
        private readonly Vector3[] quadVertices = new Vector3[4];
        private readonly Vector2[] quadUVs = new Vector2[4];

        private OutlineMask mask;
        private OutlineField field;
        private OutlineFrame frame;
        private Material material;
        private Mesh quad;
        private Stage stage;
        private int version;

        // The inputs of the latest capture, and whether the inspector has changed one since.
        private (float cutoff, int budget, float width) lastInputs;
        private bool outdated;

        private int Budget => Mathf.Max(pixelBudget, 1024);

        // What a capture depends on; colour and the zoom range are material properties.
        private (float cutoff, int budget, float width) CaptureInputs => (alphaCutoff, pixelBudget, Mathf.Max(widthRange.x, widthRange.y));

        private enum Stage {
            None
          , Capture
          , Readback
          , Transform
          , Shown
        }

        protected virtual void Awake() {
            material = new Material(outlineShader) { hideFlags = HideFlags.HideAndDontSave };

            quad = new Mesh {
                name = "OutlineQuad"
              , hideFlags = HideFlags.HideAndDontSave
            };

            quad.MarkDynamic();
            quad.SetVertices(quadVertices);
            quad.SetUVs(0, quadUVs);
            quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);

            meshFilter.sharedMesh = quad;
            meshRenderer.sharedMaterial = material;
            meshRenderer.enabled = false;

            // On GLES the first draw with each shader holds the main thread about 6 ms while the render thread builds its
            // program (measured on a PowerVR phone); done here, it lands in loading instead of on the first selection.
            EnsureBuffers();
            mask.Warm(material, quad);
        }

        protected virtual void OnDestroy() {
            field?.Release();
            mask?.Release();
            field = null;
            mask = null;

            if (meshFilter != null) meshFilter.sharedMesh = null;

            OutlineMask.SafeDestroy(material);
            OutlineMask.SafeDestroy(quad);
        }

        public virtual void SetTargets(IEnumerable<SpriteRenderer> renderers) {
            targets.Clear();

            if (renderers != null) {
                foreach (var renderer in renderers) {
                    if (renderer != null) targets.Add(renderer);
                }
            }

            ++version;

            // The old outline belongs to the old group; the new one shows once its field lands.
            meshRenderer.enabled = false;
            stage = targets.Count > 0 ? Stage.Capture : Stage.None;
        }

        protected virtual void LateUpdate() {
            // A capture in flight is never restarted: an inspector change waits for it to show, then takes the latest values.
            if (stage is Stage.Shown && outdated) stage = Stage.Capture;

            if (stage is Stage.Capture) Capture(this);
            else if (stage is Stage.Transform && field.TryUpload()) Show(this);

            // LateUpdate runs after this frame's constraints, so the transform already sits on the group.
            static void Capture(SpriteOutline context) {
                using var _ = captureMarker.Auto();

                var worldToLocal = context.transform.worldToLocalMatrix;
                var worldPerLocal = context.transform.localToWorldMatrix.GetColumn(0).magnitude;

                if (worldPerLocal <= 0 || !TryGetBounds(context.targets, worldToLocal, out var min, out var max)) {
                    context.stage = Stage.None;
                    return;
                }

                context.EnsureBuffers();

                var padding = Mathf.Max(context.widthRange.x, context.widthRange.y) / worldPerLocal;
                var frame = OutlineFrame.Fit(min, max, padding, context.Budget, context.mask.Size);
                var captured = context.version;

                context.lastInputs = context.CaptureInputs;
                context.outdated = false;

                // Set first: without asynchronous readback the data comes back before Capture returns.
                context.stage = Stage.Readback;
                context.mask.Capture(context.targets, worldToLocal, frame, context.alphaCutoff, (data, rowBytes, pixelBytes) => context.OnMask(captured, frame, data, rowBytes, pixelBytes));
            }

            static void Show(SpriteOutline context) {
                var frame = context.frame;
                var size = context.field.Size;
                var min = frame.Origin + new Vector2(.5f, .5f) * frame.TexelSize;
                var max = frame.Origin + new Vector2(frame.Width - .5f, frame.Height - .5f) * frame.TexelSize;
                var uvMin = new Vector2(.5f, .5f) / size;
                var uvMax = new Vector2(frame.Width - .5f, frame.Height - .5f) / size;

                // Corners on texel centres: bilinear sampling never reaches the texels outside this frame.
                context.quadVertices[0] = new Vector3(min.x, min.y);
                context.quadVertices[1] = new Vector3(max.x, min.y);
                context.quadVertices[2] = new Vector3(max.x, max.y);
                context.quadVertices[3] = new Vector3(min.x, max.y);
                context.quadUVs[0] = new Vector2(uvMin.x, uvMin.y);
                context.quadUVs[1] = new Vector2(uvMax.x, uvMin.y);
                context.quadUVs[2] = new Vector2(uvMax.x, uvMax.y);
                context.quadUVs[3] = new Vector2(uvMin.x, uvMax.y);

                context.quad.SetVertices(context.quadVertices);
                context.quad.SetUVs(0, context.quadUVs);
                context.quad.RecalculateBounds();

                context.material.SetFloat(texelSizeId, frame.TexelSize);
                context.material.SetFloat(maxRadiusId, frame.MaxRadius);
                context.ApplyVisual();

                context.meshRenderer.enabled = true;
                context.stage = Stage.Shown;
            }

            // Every corner of every renderer's local box, taken into this transform's space.
            static bool TryGetBounds(List<SpriteRenderer> targets, Matrix4x4 worldToLocal, out Vector2 min, out Vector2 max) {
                min = new Vector2(float.MaxValue, float.MaxValue);
                max = new Vector2(float.MinValue, float.MinValue);

                foreach (var renderer in targets) {
                    if (renderer == null || renderer.sprite == null) continue;

                    var toLocal = worldToLocal * renderer.localToWorldMatrix;
                    var bounds = renderer.localBounds;

                    for (var i = 0; i < 8; ++i) {
                        var corner = new Vector3(
                            (i & 1) == 0 ? bounds.min.x : bounds.max.x
                          , (i & 2) == 0 ? bounds.min.y : bounds.max.y
                          , (i & 4) == 0 ? bounds.min.z : bounds.max.z);

                        Vector2 point = toLocal.MultiplyPoint3x4(corner);

                        min = Vector2.Min(min, point);
                        max = Vector2.Max(max, point);
                    }
                }

                return min.x <= max.x;
            }
        }

        private void OnMask(int captured, OutlineFrame frame, NativeArray<byte> data, int rowBytes, int pixelBytes) {
            // A newer SetTargets, or the outliner is gone: this capture no longer matters.
            if (captured != version || stage is not Stage.Readback || field == null) return;

            if (!data.IsCreated) {
                Debug.LogWarning($"{nameof(SpriteOutline)} {name}: silhouette readback failed, outline skipped.", this);
                stage = Stage.None;
                return;
            }

            this.frame = frame;
            field.Schedule(data, rowBytes, pixelBytes, frame);
            stage = Stage.Transform;
        }

        // Sized for the largest frame the budget allows, so selecting never allocates; only a new budget does. Either side is
        // capped at twice the square root of the budget: up to a 4:1 aspect the whole budget is used, beyond it the capture
        // only gets cheaper.
        private void EnsureBuffers() {
            var size = Mathf.Min(Mathf.CeilToInt(2 * Mathf.Sqrt(Budget)), MAX_SIZE);

            if (mask != null && mask.Size == size) return;

            field?.Release();
            mask?.Release();

            mask = new OutlineMask(size);
            field = new OutlineField(size);

            material.SetTexture(fieldTexId, field.Texture);
        }

        private void ApplyVisual() {
            material.SetColor(colorId, color);
            material.SetVector(orthoRangeId, orthoRange);
            material.SetVector(widthRangeId, widthRange);
        }

        // Tuning in the inspector while playing: colour and width show at once; cutoff, budget and the width's padding take
        // a fresh capture, one at a time however fast a slider moves.
        protected virtual void OnValidate() {
            if (material == null) return;

            ApplyVisual();

            if (stage is not Stage.None && lastInputs != CaptureInputs) outdated = true;
        }

        protected virtual void Reset() {
            SetupEditor();
        }

        [Button]
        protected virtual void SetupEditor() {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (outlineShader == null) outlineShader = Shader.Find(OUTLINE_SHADER);
        }
    }
}