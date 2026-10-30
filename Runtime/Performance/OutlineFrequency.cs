namespace RiseOn.Outline2D {
    /// <summary>
    /// When an outline captures its group. Beyond what each one adds, a change of targets or of the settings a capture<br/>
    /// reads always takes one. One capture runs at a time, so an interval shorter than a capture runs them back to back.
    /// </summary>
    public enum OutlineFrequency {
        /// <summary>Only then: for a group that stands still.</summary>
        OnDemand
      ,
        /// <summary>
        /// Also every <see cref="OutlinePerformanceProfile.Frames"/> frames from the start of the last capture, for a group<br/>
        /// that changes shape on its own, a skeleton or an animated sprite; 1 is every frame.
        /// </summary>
        FrameInterval
      ,
        /// <summary>
        /// Also every <see cref="OutlinePerformanceProfile.Seconds"/> seconds of real time from the start of the last<br/>
        /// capture, whatever the frame rate or the time scale.
        /// </summary>
        TimeInterval
    }
}