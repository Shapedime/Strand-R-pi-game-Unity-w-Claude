namespace BeachVolley.Skills
{
    /// <summary>
    /// The ten volleyball skills a player develops on the court, each a level from 1 to 10. The
    /// order is the save format: append new skills at the end, never reorder.
    /// </summary>
    public enum SkillType
    {
        Serve,
        Bump,
        Set,
        Spike,
        Roll,
        Dive,
        Block,
        CourtAwareness,
        Stamina,
        Athleticism,
    }

    /// <summary>
    /// The body, trained in the gym with gym points (a separate currency): levels 1–10. The order
    /// is the save format.
    /// </summary>
    public enum GymSkill
    {
        Strength,
        LegPower,
        Explosiveness,
        Core,
        Endurance,
    }

    /// <summary>Display names and one-line descriptions for the development screen.</summary>
    public static class SkillInfo
    {
        /// <summary>The order the development screen lists them in.</summary>
        public static readonly SkillType[] DisplayOrder =
        {
            SkillType.CourtAwareness, SkillType.Bump, SkillType.Set, SkillType.Spike, SkillType.Roll,
            SkillType.Dive, SkillType.Serve, SkillType.Block, SkillType.Stamina, SkillType.Athleticism,
        };

        public static readonly GymSkill[] GymOrder =
        {
            GymSkill.Strength, GymSkill.LegPower, GymSkill.Explosiveness, GymSkill.Core, GymSkill.Endurance,
        };

        public static string Name(SkillType type) => type switch
        {
            SkillType.CourtAwareness => "Court Awareness",
            SkillType.Dive => "Dive (Pancake)",
            SkillType.Roll => "Roll Shot",
            _ => type.ToString(),
        };

        public static string Description(SkillType type) => type switch
        {
            SkillType.CourtAwareness => "Reading attacks, anticipating where the ball goes, positioning and decisions. Shows the ball's flight earlier.",
            SkillType.Bump => "Forearm passing and controlling the first touch. A wider, more forgiving bump window.",
            SkillType.Set => "Setting accuracy, height and control. A more forgiving set window.",
            SkillType.Spike => "Attack timing and control. Tighter aim and a wider swing window.",
            SkillType.Roll => "The soft roll shot over the block: how close it lands to where you aim.",
            SkillType.Dive => "Timing and success of last-second dives and pancake saves.",
            SkillType.Serve => "Serve timing, accuracy and consistency.",
            SkillType.Block => "Blocking timing, positioning and how big a wall your hands make at the net.",
            SkillType.Stamina => "Staying sharp through long sessions and matches. Fatigue builds more slowly.",
            SkillType.Athleticism => "Agility, balance and movement efficiency: quicker turns, a touch more speed.",
            _ => "",
        };

        public static string Name(GymSkill type) => type switch
        {
            GymSkill.LegPower => "Leg Power",
            _ => type.ToString(),
        };

        public static string Description(GymSkill type) => type switch
        {
            GymSkill.Strength => "Upper-body power: harder spikes and jump serves.",
            GymSkill.LegPower => "Trained legs jump higher — more time in the air for spikes and blocks.",
            GymSkill.Explosiveness => "The first step: faster acceleration toward the ball.",
            GymSkill.Core => "A strong core: quicker back up after a dive, steadier landings.",
            GymSkill.Endurance => "Physical engine: fatigue builds more slowly in long sessions.",
            _ => "",
        };
    }
}
