using System;
using UnityEngine;

namespace BeachVolley.Skills
{
    /// <summary>
    /// Runtime copy of the player's skills: ten volleyball skills and five gym skills, each a level
    /// from 1 to 10, the development points and gym points waiting to be spent, and today's
    /// fatigue. Gameplay reads values from here (Bump drives the bump window, Leg Power the jump
    /// height, Strength the spike pace...). Training never raises a skill by itself: it awards
    /// points, and the player spends them on the development screen (see <see cref="BeginAllocation"/>).
    /// Court training pays development points; only the gym pays gym points.
    /// Story mode saves and restores everything through Export/Import (see StoryState).
    /// </summary>
    public class PlayerSkills : MonoBehaviour
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 10;
        public static readonly int Count = Enum.GetValues(typeof(SkillType)).Length;
        public static readonly int GymCount = Enum.GetValues(typeof(GymSkill)).Length;

        [SerializeField] SkillProfile startingProfile;

        [Header("Fatigue (Stamina)")]
        [Tooltip("Fatigue multiplier on every bit of effort at Stamina 1 .. Stamina 10.")]
        [SerializeField] Vector2 fatigueRate = new Vector2(1f, 0.4f);
        [Tooltip("At full fatigue, timing windows and top speed shrink by this share.")]
        [SerializeField, Range(0f, 0.5f)] float maxFatiguePenalty = 0.15f;

        readonly int[] levels = new int[Count];
        readonly int[] gym = new int[GymCount];
        int[] allocationStart, gymAllocationStart;
        int allocationStartPoints, allocationStartGymPoints;
        float bankedCredit;

        /// <summary>Development points earned and not yet spent.</summary>
        public int DevPoints { get; private set; }

        /// <summary>Gym points earned and not yet spent (gym skills only).</summary>
        public int GymPoints { get; private set; }

        /// <summary>The gym has been introduced in the story; until then its panel is locked.</summary>
        public bool GymUnlocked { get; set; }

        /// <summary>0 = fresh, 1 = exhausted. Builds up through a day's training; reset each day.</summary>
        public float Fatigue { get; private set; }

        /// <summary>Kept for the old coach session and the save format; it no longer affects progression.</summary>
        public float Enjoyment { get; private set; }

        /// <summary>Raised when a skill goes up a level on the development screen: (skill, new level).</summary>
        public event Action<SkillType, int> SkillRaised;

        /// <summary>Raised when development points are awarded: (amount, reason).</summary>
        public event Action<int, string> PointsAwarded;

        void Awake()
        {
            for (int i = 0; i < Count; i++)
                levels[i] = startingProfile != null ? startingProfile.Get((SkillType)i) : MinLevel;
            for (int i = 0; i < GymCount; i++) gym[i] = MinLevel;
            Enjoyment = startingProfile != null ? startingProfile.enjoyment : 10f;
        }

        public int Get(SkillType type) => levels[(int)type];

        /// <summary>Skill as 0..1: level 1 = 0 (a true beginner), level 10 = 1.</summary>
        public float Normalized(SkillType type) => (levels[(int)type] - MinLevel) / (float)(MaxLevel - MinLevel);

        /// <summary>Interpolates a tuning value from its level-1 setting to its level-10 setting.</summary>
        public float Scale(SkillType type, float atMin, float atMax) => Mathf.Lerp(atMin, atMax, Normalized(type));

        public int Get(GymSkill type) => gym[(int)type];

        public float Normalized(GymSkill type) => (gym[(int)type] - MinLevel) / (float)(MaxLevel - MinLevel);

        /// <summary>Interpolates a tuning value from its level-1 setting to its level-10 setting (gym skill).</summary>
        public float Scale(GymSkill type, float atMin, float atMax) => Mathf.Lerp(atMin, atMax, Normalized(type));

        /// <summary>1 when fresh, down to 1 − maxFatiguePenalty when exhausted. Timing windows and top speed use it.</summary>
        public float FatigueFactor => 1f - Fatigue * maxFatiguePenalty;

        // ---------- Development points ----------

        public void AwardPoints(int amount, string reason = null)
        {
            if (amount <= 0) return;
            DevPoints += amount;
            PointsAwarded?.Invoke(amount, reason);
        }

        /// <summary>
        /// Small bits of credit (e.g. per rep) that turn into a development point once they add up
        /// to 1. Returns how many whole points this credit completed.
        /// </summary>
        public int AddCredit(float credit, string reason = null)
        {
            if (credit <= 0f) return 0;
            bankedCredit += credit;
            int whole = Mathf.FloorToInt(bankedCredit + 1e-4f);
            if (whole <= 0) return 0;
            bankedCredit -= whole;
            AwardPoints(whole, reason);
            return whole;
        }

        /// <summary>Points to go from the current level to the next: 1 up to level 4, 2 up to 7, 3 up to 10.</summary>
        public int CostToRaise(SkillType type) => CostOfLevel(Get(type) + 1);

        public static int CostOfLevel(int level) => level <= 4 ? 1 : level <= 7 ? 2 : 3;

        public bool CanRaise(SkillType type) => Get(type) < MaxLevel && DevPoints >= CostToRaise(type);

        public void AwardGymPoints(int amount, string reason = null)
        {
            if (amount <= 0) return;
            GymPoints += amount;
            PointsAwarded?.Invoke(amount, reason ?? "Gym");
        }

        public int CostToRaise(GymSkill type) => CostOfLevel(Get(type) + 1);

        public bool CanRaise(GymSkill type) => Get(type) < MaxLevel && GymPoints >= CostToRaise(type);

        public bool TryRaise(GymSkill type)
        {
            if (!CanRaise(type)) return false;
            GymPoints -= CostToRaise(type);
            gym[(int)type]++;
            return true;
        }

        public bool TryLower(GymSkill type)
        {
            int i = (int)type;
            if (gymAllocationStart == null || gym[i] <= gymAllocationStart[i]) return false;
            GymPoints += CostOfLevel(gym[i]);
            gym[i]--;
            return true;
        }

        public int PendingGain(GymSkill type) => gymAllocationStart == null ? 0 : gym[(int)type] - gymAllocationStart[(int)type];

        /// <summary>Starts a round on the development screen: raises can be undone until Confirm/Cancel.</summary>
        public void BeginAllocation()
        {
            allocationStart = (int[])levels.Clone();
            gymAllocationStart = (int[])gym.Clone();
            allocationStartPoints = DevPoints;
            allocationStartGymPoints = GymPoints;
        }

        public bool InAllocation => allocationStart != null;

        public bool TryRaise(SkillType type)
        {
            if (!CanRaise(type)) return false;
            DevPoints -= CostToRaise(type);
            levels[(int)type]++;
            SkillRaised?.Invoke(type, levels[(int)type]);
            return true;
        }

        /// <summary>Undoes a raise made in this allocation round (never below where the round began).</summary>
        public bool TryLower(SkillType type)
        {
            int i = (int)type;
            if (allocationStart == null || levels[i] <= allocationStart[i]) return false;
            DevPoints += CostOfLevel(levels[i]);
            levels[i]--;
            return true;
        }

        /// <summary>Levels gained on this skill in the current allocation round.</summary>
        public int PendingGain(SkillType type) => allocationStart == null ? 0 : levels[(int)type] - allocationStart[(int)type];

        public void ConfirmAllocation() => allocationStart = gymAllocationStart = null;

        public void CancelAllocation()
        {
            if (allocationStart == null) return;
            Array.Copy(allocationStart, levels, Count);
            Array.Copy(gymAllocationStart, gym, GymCount);
            DevPoints = allocationStartPoints;
            GymPoints = allocationStartGymPoints;
            allocationStart = gymAllocationStart = null;
        }

        // ---------- Fatigue ----------

        /// <summary>Adds effort (0.02 ≈ one drill rep, 0.03 ≈ one rally); Stamina and Endurance slow how fast it builds.</summary>
        public void AddFatigue(float effort) =>
            Fatigue = Mathf.Clamp01(Fatigue + effort * Scale(SkillType.Stamina, fatigueRate.x, fatigueRate.y)
                * Scale(GymSkill.Endurance, 1f, 0.6f));

        public void Rest(float amount) => Fatigue = Mathf.Clamp01(Fatigue - amount);

        public void ResetFatigue() => Fatigue = 0f;

        /// <summary>Restores fatigue carried over from earlier days (story Chapter 3 onwards).</summary>
        public void SetFatigue(float value) => Fatigue = Mathf.Clamp01(value);

        // ---------- Old coach-session bridge ----------

        /// <summary>The switched-off coach session still reports XP; it becomes credit (6 XP ≈ 1 point).</summary>
        public float AddXp(SkillType type, float amount)
        {
            AddCredit(amount / 6f, type.ToString());
            return amount;
        }

        public float XpMultiplier => 1f;

        public float AddEnjoyment(float amount)
        {
            float before = Enjoyment;
            Enjoyment = Mathf.Clamp(Enjoyment + amount, 0f, 100f);
            return Enjoyment - before;
        }

        public void BeginSession() => ResetFatigue();

        /// <summary>Kept for the coach session's summary: levels never change during a session now.</summary>
        public int[] EndSession() => new int[Count];

        // ---------- Save ----------

        public void Export(out int[] levelsOut, out int points, out float credit)
        {
            levelsOut = (int[])levels.Clone();
            points = DevPoints;
            credit = bankedCredit;
        }

        public void ExportGym(out int[] gymOut, out int points, out bool unlocked)
        {
            gymOut = (int[])gym.Clone();
            points = GymPoints;
            unlocked = GymUnlocked;
        }

        public void ImportGym(int[] gymIn, int points, bool unlocked)
        {
            if (gymIn != null && gymIn.Length == GymCount)
                for (int i = 0; i < GymCount; i++) gym[i] = Mathf.Clamp(gymIn[i], MinLevel, MaxLevel);
            GymPoints = Mathf.Max(0, points);
            GymUnlocked = unlocked;
            gymAllocationStart = null;
        }

        /// <summary>Restores saved levels (arrays of the wrong length are ignored) and points.</summary>
        public void Import(int[] levelsIn, int points, float credit)
        {
            if (levelsIn != null && levelsIn.Length == Count)
                for (int i = 0; i < Count; i++) levels[i] = Mathf.Clamp(levelsIn[i], MinLevel, MaxLevel);
            DevPoints = Mathf.Max(0, points);
            bankedCredit = Mathf.Clamp01(credit);
            allocationStart = null;
        }
    }
}
