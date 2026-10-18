using UnityEngine;

namespace BeachVolley.Skills
{
    /// <summary>
    /// Starting skill levels (1–10) for a player. Treated as read-only data at runtime;
    /// <see cref="PlayerSkills"/> copies it so play never edits the asset.
    /// </summary>
    /// <remarks>
    /// Story: the default profile is the rookie, someone who has never played and just
    /// heard about the city's beginner training. Everything starts at the bottom.
    /// </remarks>
    [CreateAssetMenu(menuName = "Beach Volleyball/Skill Profile", fileName = "SkillProfile")]
    public class SkillProfile : ScriptableObject
    {
        [TextArea] public string note = "Rookie at city beginner training: never played before, shows up anyway.";

        [Range(1, 10)] public int serve = 1;
        [Range(1, 10)] public int bump = 1;
        [Range(1, 10)] public int set = 1;
        [Range(1, 10)] public int spike = 1;
        [Range(1, 10)] public int roll = 1;
        [Range(1, 10)] public int dive = 1;
        [Range(1, 10)] public int block = 1;
        [Range(1, 10)] public int courtAwareness = 1;
        [Range(1, 10)] public int stamina = 1;
        [Range(1, 10)] public int athleticism = 1;

        [Tooltip("Kept for the old coach session; it no longer affects progression.")]
        [Range(0f, 100f)] public float enjoyment = 10f;

        public int Get(SkillType type) => Mathf.Clamp(type switch
        {
            SkillType.Serve => serve,
            SkillType.Bump => bump,
            SkillType.Set => set,
            SkillType.Spike => spike,
            SkillType.Roll => roll,
            SkillType.Dive => dive,
            SkillType.Block => block,
            SkillType.CourtAwareness => courtAwareness,
            SkillType.Stamina => stamina,
            SkillType.Athleticism => athleticism,
            _ => 1,
        }, PlayerSkills.MinLevel, PlayerSkills.MaxLevel);
    }
}
