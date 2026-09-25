using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiseOn.Outline2D {
    /// <summary>
    /// The targets of the latest SetTargets, null ones dropped. Each is read again at capture time, so a target<br/>
    /// changed in between is captured as it is then; the concrete source says what one target draws.
    /// </summary>
    internal abstract class MaskSource<TTarget> : IMaskSource where TTarget : Component {
        private readonly List<TTarget> targets = new();

        public int Count => targets.Count;

        public void Set(IEnumerable<TTarget> values) {
            targets.Clear();

            if (values == null) return;

            foreach (var target in values) {
                if (target != null) targets.Add(target);
            }
        }

        // Every corner of every target's local box, taken into the capture's space.
        public bool TryGetBounds(Matrix4x4 worldToLocal, out Vector2 min, out Vector2 max) {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);

            foreach (var target in targets) {
                if (target == null || !TryGetLocalBounds(target, out var bounds)) continue;

                var toLocal = worldToLocal * target.transform.localToWorldMatrix;

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

        public virtual void Draw(CommandBuffer cmd, Material material) {
            foreach (var target in targets) {
                if (target != null) DrawTarget(cmd, material, target);
            }
        }

        /// <summary>The target's box in its own local space; false when it draws nothing.</summary>
        protected abstract bool TryGetLocalBounds(TTarget target, out Bounds bounds);

        /// <summary>Records the target's silhouette, when it draws anything.</summary>
        protected abstract void DrawTarget(CommandBuffer cmd, Material material, TTarget target);
    }
}