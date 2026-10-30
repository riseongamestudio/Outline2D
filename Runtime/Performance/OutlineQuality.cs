namespace RiseOn.Outline2D {
    /// <summary>How an outline builds its distance field from the captured silhouette.</summary>
    public enum OutlineQuality {
        /// <summary>Exact Euclidean distances on the CPU, from the silhouette read back from the GPU.</summary>
        Exact
      ,
        /// <summary>
        /// Approximate distances by jump flooding on the GPU, nothing read back, the shadow's softness taken from the<br/>
        /// distance instead of a blur. Needs half-float render targets and texture copies; without them it builds<br/>
        /// as <see cref="Exact"/>.
        /// </summary>
        Fast
    }
}