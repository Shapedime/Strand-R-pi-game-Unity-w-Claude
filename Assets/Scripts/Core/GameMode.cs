namespace BeachVolley.Core
{
    public enum GameModeKind
    {
        /// <summary>Free practice court with the ball machine; nothing is scored or saved.</summary>
        Practice,
    }

    /// <summary>
    /// What the main menu asked the Beach scene to run. Story, tournaments and online plug in here
    /// later; loading Beach directly in the Editor defaults to practice.
    /// </summary>
    public static class GameMode
    {
        public static GameModeKind Current { get; set; } = GameModeKind.Practice;
    }
}
