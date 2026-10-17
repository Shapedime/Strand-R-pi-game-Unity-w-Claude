using System;
using UnityEngine;

namespace BeachVolley.Skills
{
    /// <summary>
    /// Runtime copy of the player's skills, XP and enjoyment. Gameplay reads values from
    /// here (e.g. Footwork drives acceleration, Pass drives the bump window).
    /// XP turns into skill points one at a time, and a training session can raise each
    /// skill by at most <see cref="maxGainPerSession"/>; extra XP is banked, never dumped.
    /// Story mode saves and restores them through Export/Import (see StoryState).
    /// </summary>
    public class PlayerSkills : MonoBehaviour
    {
        public const int MaxValue = 100;
        public static readonly int Count = Enum.GetValues(typeof(SkillType)).Length;

        [SerializeField] SkillProfile startingProfile;

        [Header("Progression")]
        [Tooltip("XP needed for the next point at skill 0.")]
        [SerializeField] float baseXpPerPoint = 6f;
        [Tooltip("Extra XP needed per point already earned, so later points come slower.")]
        [SerializeField] float xpPerPointGrowth = 0.4f;
        [SerializeField] int maxGainPerSession = 2;
        [Tooltip("XP bonus at 100 enjoyment (0.25 = +25%).")]
        [SerializeField] float maxEnjoymentXpBonus = 0.25f;

        readonly int[] values = new int[Count];
        readonly float[] xp = new float[Count];
        readonly int[] sessionGains = new int[Count];
        bool inSession;

        public float Enjoyment { get; private set; }

        /// <summary>Raised whenever a skill gains a point: (skill, new value).</summary>
        public event Action<SkillType, int> SkillRaised;

        public float XpMultiplier => 1f + Enjoyment / 100f * maxEnjoymentXpBonus;

        void Awake()
        {
            for (int i = 0; i < Count; i++)
                values[i] = startingProfile != null ? startingProfile.Get((SkillType)i) : 5;
            Enjoyment = startingProfile != null ? startingProfile.enjoyment : 10f;
        }

        public int Get(SkillType type) => values[(int)type];

        /// <summary>Skill as 0..1.</summary>
        public float Normalized(SkillType type) => values[(int)type] / (float)MaxValue;

        /// <summary>Interpolates a tuning value from its rookie (0) to its master (100) setting.</summary>
        public float Scale(SkillType type, float atZero, float atMax) => Mathf.Lerp(atZero, atMax, Normalized(type));

        public float Xp(SkillType type) => xp[(int)type];
        public float XpToNext(SkillType type) => baseXpPerPoint + xpPerPointGrowth * values[(int)type];

        /// <summary>Adds XP (scaled by enjoyment) and converts it to points. Returns XP actually added.</summary>
        public float AddXp(SkillType type, float amount)
        {
            int i = (int)type;
            if (amount <= 0f || values[i] >= MaxValue) return 0f;

            float gained = amount * XpMultiplier;
            xp[i] += gained;
            while (values[i] < MaxValue && xp[i] >= XpToNext(type))
            {
                if (inSession && sessionGains[i] >= maxGainPerSession)
                {
                    // Bank it just short of the next point; it lands next session.
                    xp[i] = XpToNext(type) - 0.01f;
                    break;
                }
                xp[i] -= XpToNext(type);
                values[i]++;
                if (inSession) sessionGains[i]++;
                SkillRaised?.Invoke(type, values[i]);
            }
            return gained;
        }

        /// <summary>Returns how much enjoyment actually changed after clamping.</summary>
        public float AddEnjoyment(float amount)
        {
            float before = Enjoyment;
            Enjoyment = Mathf.Clamp(Enjoyment + amount, 0f, 100f);
            return Enjoyment - before;
        }

        /// <summary>Copies values, XP and enjoyment out (for the story save).</summary>
        public void Export(out int[] valuesOut, out float[] xpOut, out float enjoyment)
        {
            valuesOut = (int[])values.Clone();
            xpOut = (float[])xp.Clone();
            enjoyment = Enjoyment;
        }

        /// <summary>Restores saved values; arrays of the wrong length are ignored.</summary>
        public void Import(int[] valuesIn, float[] xpIn, float enjoyment)
        {
            if (valuesIn != null && valuesIn.Length == Count) Array.Copy(valuesIn, values, Count);
            if (xpIn != null && xpIn.Length == Count) Array.Copy(xpIn, xp, Count);
            if (enjoyment >= 0f) Enjoyment = Mathf.Clamp(enjoyment, 0f, 100f);
        }

        public void BeginSession()
        {
            inSession = true;
            Array.Clear(sessionGains, 0, Count);
        }

        /// <summary>Ends the session and returns points gained per skill (indexed by SkillType).</summary>
        public int[] EndSession()
        {
            inSession = false;
            return (int[])sessionGains.Clone();
        }
    }
}
