using UnityEngine;

namespace RiseOn.SpriteOutline {
    /// <summary>
    /// Where one capture sits in the outliner's local space: its bottom-left corner, the size of a texel and<br/>
    /// how many texels it spans. The same frame drives the mask projection, the distance field and the quad.
    /// </summary>
    internal readonly struct OutlineFrame {
        // Texels kept clear on every side beyond the padding, so the anti-aliased rim never meets the edge.
        private const int RIM = 2;

        // The capture is flat; the depth range only has to hold wherever the sprites sit along local Z.
        private const float DEPTH = 1e4f;

        public readonly Vector2 Origin;
        public readonly float TexelSize;
        public readonly int Width;
        public readonly int Height;

        /// <summary>Widest ring, in texels, that still fits inside the capture.</summary>
        public readonly float MaxRadius;

        private OutlineFrame(Vector2 origin, float texelSize, int width, int height, float maxRadius) {
            Origin = origin;
            TexelSize = texelSize;
            Width = width;
            Height = height;
            MaxRadius = maxRadius;
        }

        /// <summary>Unity-convention ortho over the capture: SetViewProjectionMatrices converts it for the graphics API.</summary>
        public Matrix4x4 Projection => Matrix4x4.Ortho(Origin.x, Origin.x + Width * TexelSize, Origin.y, Origin.y + Height * TexelSize, -DEPTH, DEPTH);

        /// <summary>
        /// Spreads the budget over the padded bounds with square texels, so the cost follows the budget alone,<br/>
        /// not the group's size or aspect; neither side exceeds maxSize.
        /// </summary>
        public static OutlineFrame Fit(Vector2 min, Vector2 max, float padding, int budget, int maxSize) {
            var size = Vector2.Max(max - min + 2 * padding * Vector2.one, new Vector2(1e-4f, 1e-4f));

            // (size.x * k + 2 RIM) * (size.y * k + 2 RIM) = budget, solved for k, texels per local unit.
            var a = size.x * size.y;
            var b = 2f * RIM * (size.x + size.y);
            var c = 4f * RIM * RIM - budget;
            var texelsPerUnit = (-b + Mathf.Sqrt(b * b - 4 * a * c)) / (2 * a);

            var texel = Mathf.Max(1 / texelsPerUnit, Mathf.Max(size.x, size.y) / (maxSize - 2 * RIM));
            var width = Mathf.Min(Mathf.CeilToInt(size.x / texel) + 2 * RIM, maxSize);
            var height = Mathf.Min(Mathf.CeilToInt(size.y / texel) + 2 * RIM, maxSize);
            var origin = (min + max) / 2 - new Vector2(width, height) * (texel / 2);

            return new OutlineFrame(origin, texel, width, height, padding / texel + RIM - 1);
        }
    }
}