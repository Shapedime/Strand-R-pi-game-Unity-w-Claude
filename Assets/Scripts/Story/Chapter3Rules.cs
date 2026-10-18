using BeachVolley.Match;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>How the protagonist feels today. Temporary: it never lowers a skill level.</summary>
    public enum Condition { Fresh, Tired, Exhausted }

    /// <summary>Dave's friendship, as the dialogue sees it.</summary>
    public enum Band { Low, Medium, High }

    /// <summary>How the Day 5 evaluation went.</summary>
    public enum Performance { Poor, Average, Strong }

    /// <summary>
    /// Chapter 3 ("The Price") tuning in one place: what each activity costs in fatigue, what it
    /// pays, how fatigue reads as a condition, how Dave's friendship reads as a band, and how the
    /// final evaluation is graded. Values are first guesses for playtesting.
    /// </summary>
    public static class Chapter3Rules
    {
        // ---------- Fatigue ----------

        public const float TiredAt = 0.35f;
        public const float ExhaustedAt = 0.7f;

        /// <summary>Extra fatigue on top of what the reps themselves add.</summary>
        public const float GymMorning = 0.22f;
        public const float GymConditioning = 0.25f;
        public const float ExtraPractice = 0.15f;

        /// <summary>Recovery (fatigue taken off).</summary>
        public const float Sleep = 0.3f;
        public const float RestDay = 0.3f;
        public const float RestEvening = 0.45f;
        public const float StudyEvening = 0.2f;

        public static Condition ConditionOf(float fatigue) =>
            fatigue >= ExhaustedAt ? Condition.Exhausted : fatigue >= TiredAt ? Condition.Tired : Condition.Fresh;

        public static string Name(Condition c) => c switch
        {
            Condition.Exhausted => "Exhausted",
            Condition.Tired => "Tired",
            _ => "Fresh",
        };

        public static string Effect(Condition c) => c switch
        {
            Condition.Exhausted => "Clearly less consistent movement. Steve may cut the workload.",
            Condition.Tired => "Slightly less forgiving movement and recovery.",
            _ => "Normal physical performance.",
        };

        /// <summary>"→ Tired" style preview of where an activity would leave him.</summary>
        public static string Preview(float now, float change)
        {
            var before = ConditionOf(now);
            var after = ConditionOf(Mathf.Clamp01(now + change));
            return after == before ? Name(after).ToLowerInvariant() : "→ " + Name(after).ToLowerInvariant();
        }

        // ---------- Dave ----------

        /// <summary>
        /// Low / Medium / High from Dave's relationship (Chapters 1–2 give roughly 0–8). Missing
        /// invitations again and again cools the tone by one step; a single miss never does.
        /// </summary>
        public static Band DaveBand()
        {
            int r = StoryState.Relationship("dave");
            var band = r >= 7 ? Band.High : r >= 4 ? Band.Medium : Band.Low;
            if (MissedInvites >= 2 && band > Band.Low) band--;
            return band;
        }

        public static int MissedInvites => int.TryParse(StoryState.Flag("ch3_missed_invites"), out int n) ? n : 0;

        public static void MissInvite() => StoryState.SetFlag("ch3_missed_invites", (MissedInvites + 1).ToString());

        // ---------- Evaluation ----------

        /// <summary>
        /// The Day 5 grade from what actually happened on the court: the drills (40 %) and the
        /// game (60 %: good touches, share of the points, whether he played well). Fatigue only
        /// nudges it; being tired already shows in the play itself.
        /// </summary>
        public static Performance Evaluate(float drillRatio, MatchStats game, float fatigue)
        {
            float touches = game.humanTouches > 0 ? game.humanGoodTouches / (float)game.humanTouches : 0f;
            int total = game.nearPoints + game.farPoints;
            float share = total > 0 ? game.nearPoints / (float)total : 0f;
            float match = touches * 0.5f + share * 0.35f + (game.PlayedWell ? 0.15f : 0f);
            float score = drillRatio * 0.4f + match * 0.6f;
            score -= ConditionOf(fatigue) switch { Condition.Exhausted => 0.1f, Condition.Tired => 0.05f, _ => 0f };
            return score >= 0.65f ? Performance.Strong : score >= 0.4f ? Performance.Average : Performance.Poor;
        }
    }
}
