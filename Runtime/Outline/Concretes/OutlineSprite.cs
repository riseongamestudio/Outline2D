using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Outline2D {
    /// <summary>
    /// Outlines the merged silhouette of a group of sprites, drawn by the MeshRenderer on its own GameObject<br/>
    /// so sorting layer and order are set there. The silhouette is captured in this transform's local space in<br/>
    /// the LateUpdate after <see cref="SetTargets"/>, which runs after constraints have moved it onto the group;<br/>
    /// the distance transform then runs on worker threads and the outline shows once it lands.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class OutlineSprite : MonoBehaviour, IOutline<SpriteRenderer> {
        internal const string OUTLINE_SHADER = "RiseOn/Outline2D/OutlineSprite";

        private static readonly int fieldTexId = Shader.PropertyToID("_FieldTex");
        private static readonly int colorId = Shader.PropertyToID("_Color");
        private static readonly int orthoRangeId = Shader.PropertyToID("_OrthoRange");
        private static readonly int widthRangeId = Shader.PropertyToID("_WidthRange");
        private static readonly int texelSizeId = Shader.PropertyToID("_TexelSize");
        private static readonly int maxRadiusId = Shader.PropertyToID("_MaxRadius");

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

        // A capture holds about resolution² texels, spread over the group's aspect whatever its size: the one knob for cost.
        [SerializeField, FoldoutGroup("Optimizations"), PropertyRange(64, 1024)]
        protected int resolution = 256;

        private readonly SpriteSource source = new();
        private readonly OutlineCapture capture = new();
        private readonly Vector3[] quadVertices = new Vector3[4];
        private readonly Vector2[] quadUVs = new Vector2[4];

        private Material material;
        private Mesh quad;

        // The inputs of the latest capture.
        private (float cutoff, int resolution, float width) lastInputs;

        // What a capture depends on; colour and the zoom range are material properties.
        private (float cutoff, int resolution, float width) CaptureInputs => (alphaCutoff, resolution, Mathf.Max(widthRange.x, widthRange.y));

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

            capture.Allocate(resolution * resolution);
            material.SetTexture(fieldTexId, capture.Texture);

            // On GLES the first draw with each shader holds the main thread about 6 ms while the render thread builds its
            // program (measured on a PowerVR phone); done here, it lands in loading instead of on the first selection.
            capture.Warm(material);
        }

        protected virtual void OnDestroy() {
            capture.Release();

            if (meshFilter != null) meshFilter.sharedMesh = null;

            OutlineMask.SafeDestroy(material);
            OutlineMask.SafeDestroy(quad);
        }

        public virtual void SetTargets(IEnumerable<SpriteRenderer> targets) {
            source.Set(targets);
            capture.Restart(source.Count > 0);

            // The old outline belongs to the old group; the new one shows once its field lands.
            meshRenderer.enabled = false;
        }

        protected virtual void LateUpdate() {
            // A capture in flight is never restarted: an inspector change waits for it to land, then takes the latest values.
            if (capture.IsDue) Capture(this);
            else if (capture.TryUpload()) Show(this);

            // LateUpdate runs after this frame's constraints, so the transform already sits on the group.
            static void Capture(OutlineSprite context) {
                var worldPerLocal = context.transform.localToWorldMatrix.GetColumn(0).magnitude;
                var padding = Mathf.Max(context.widthRange.x, context.widthRange.y) / worldPerLocal;

                context.lastInputs = context.CaptureInputs;

                if (context.capture.Allocate(context.resolution * context.resolution)) context.material.SetTexture(fieldTexId, context.capture.Texture);

                context.capture.Run(context.source, context, padding, context.alphaCutoff);
            }

            static void Show(OutlineSprite context) {
                var frame = context.capture.Frame;

                context.capture.GetQuad(out var position, out var uv);

                context.quadVertices[0] = new Vector3(position.xMin, position.yMin);
                context.quadVertices[1] = new Vector3(position.xMax, position.yMin);
                context.quadVertices[2] = new Vector3(position.xMax, position.yMax);
                context.quadVertices[3] = new Vector3(position.xMin, position.yMax);
                context.quadUVs[0] = new Vector2(uv.xMin, uv.yMin);
                context.quadUVs[1] = new Vector2(uv.xMax, uv.yMin);
                context.quadUVs[2] = new Vector2(uv.xMax, uv.yMax);
                context.quadUVs[3] = new Vector2(uv.xMin, uv.yMax);

                context.quad.SetVertices(context.quadVertices);
                context.quad.SetUVs(0, context.quadUVs);
                context.quad.RecalculateBounds();

                context.material.SetFloat(texelSizeId, frame.TexelSize);
                context.material.SetFloat(maxRadiusId, frame.MaxRadius);
                context.ApplyVisual();

                context.meshRenderer.enabled = true;
            }
        }

        private void ApplyVisual() {
            material.SetColor(colorId, color);
            material.SetVector(orthoRangeId, orthoRange);
            material.SetVector(widthRangeId, widthRange);
        }

        // Tuning in the inspector while playing: colour and width show at once; cutoff, resolution and the width's
        // padding take a fresh capture, one at a time however fast a slider moves.
        protected virtual void OnValidate() {
            if (material == null) return;

            ApplyVisual();

            if (lastInputs != CaptureInputs) capture.MarkOutdated();
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