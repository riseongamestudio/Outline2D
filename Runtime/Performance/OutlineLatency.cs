namespace RiseOn.Outline2D {
    /// <summary>How a capture's work is spread over frames.</summary>
    public enum OutlineLatency {
        /// <summary>
        /// Spread out so no frame pays it all, and the outline shows a few frames after the capture starts:<br/>
        /// <see cref="OutlineQuality.Exact"/> reads back asynchronously and builds on worker threads,<br/>
        /// <see cref="OutlineQuality.Fast"/> runs its GPU passes over three frames.
        /// </summary>
        Async
      ,
        /// <summary>
        /// All in the frame the capture starts, so the outline shows in that frame: <see cref="OutlineQuality.Exact"/><br/>
        /// reads back synchronously and waits for its jobs, which holds the main thread.
        /// </summary>
        Immediate
    }
}