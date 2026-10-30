namespace RiseOn.Outline2D {
    /// <summary>The shape of an outline's shadow.</summary>
    public enum OutlineShadowType {
        /// <summary>
        /// The silhouette with its outline, blurred and moved one way: the group as a sticker lifted off the scene. Its<br/>
        /// blur is baked into each capture.
        /// </summary>
        Directional
      ,
        /// <summary>
        /// Spread evenly all around from the outline's outer edge, strongest there and fading to nothing at its reach: a<br/>
        /// glow. Drawn from the distance alone, so nothing is baked for it.
        /// </summary>
        Spread
    }
}