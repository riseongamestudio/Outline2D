using UnityEngine;
using UnityEngine.Rendering;

namespace RiseOn.Outline2D {
    /// <summary>What a capture draws: the targets' bounds in the capture's space, then their silhouettes.</summary>
    internal interface IMaskSource {
        /// <summary>Bounds of every drawable target in the space worldToLocal leads to; false when none is drawable.</summary>
        bool TryGetBounds(Matrix4x4 worldToLocal, out Vector2 min, out Vector2 max);

        /// <summary>Records one draw per drawable target with the mask material; view and projection are already set.</summary>
        void Draw(CommandBuffer cmd, Material material);
    }
}