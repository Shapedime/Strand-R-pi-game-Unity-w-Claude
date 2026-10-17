using UnityEngine;

namespace BeachVolley.Skills
{
    /// <summary>
    /// Starting skill values for a player. Treated as read-only data at runtime;
    /// <see cref="PlayerSkills"/> copies it so play never edits the asset.
    /// </summary>
    /// <remarks>
    /// Story: the default profile is the rookie, someone who has never played and just
    /// heard about the city's beginner training. Everything starts near the bottom.
    /// </remarks>
    [CreateAssetMenu(menuName = "Beach Volleyball/Skill Profile", fileName = "SkillProfile")]
    public class SkillProfile : ScriptableObject
    {
        [TextArea] public string note = "Rookie at city beginner training: never played before, shows up anyway.";

        [Range(0, 100)] public int serve = 6;
        [Range(0, 100)] public int pass = 9;
        [Range(0, 100)] public int set = 5;
        [Range(0, 100)] public int attack = 7;
        [Range(0, 100)] public int block = 5;
        [Range(0, 100)] public int defense = 8;
        [Range(0, 100)] public int footwork = 10;
        [Range(0, 100)] public int awareness = 12;

        [Tooltip("How much the player enjoys training (0–100). Rises with practice.")]
        [Range(0f, 100f)] public float enjoyment = 10f;

        public int Get(SkillType type) => type switch
        {
            SkillType.Serve => serve,
            SkillType.Pass => pass,
            SkillType.Set => set,
            SkillType.Attack => attack,
            SkillType.Block => block,
            SkillType.Defense => defense,
            SkillType.Footwork => footwork,
            SkillType.Awareness => awareness,
            _ => 0,
        };
    }
}
