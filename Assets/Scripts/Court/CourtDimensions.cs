namespace BeachVolley.Court
{
    /// <summary>
    /// Official beach-volleyball court measurements in meters (1 Unity unit = 1 m).
    /// The court's long axis runs along Z, the net sits on Z = 0 and spans X.
    /// </summary>
    public static class CourtDimensions
    {
        /// <summary>Depth of one half, baseline to center line.</summary>
        public const float SideLength = 8f;

        /// <summary>Court width, sideline to sideline.</summary>
        public const float Width = 8f;

        /// <summary>Total play-area length (two halves).</summary>
        public const float TotalLength = SideLength * 2f;

        /// <summary>Height of the top of the net tape above the sand.</summary>
        public const float NetHeight = 2.43f;

        /// <summary>Vertical size of the net mesh, top tape to bottom tape.</summary>
        public const float NetDepth = 1f;

        /// <summary>Line (tape) width; lines are part of the court and sit inside its footprint.</summary>
        public const float LineWidth = 0.06f;

        /// <summary>How far each post stands outside its sideline.</summary>
        public const float PostOffset = 0.8f;

        public const float PostHeight = 2.6f;

        public const float HalfWidth = Width * 0.5f;
        public const float HalfLength = TotalLength * 0.5f;
    }
}
