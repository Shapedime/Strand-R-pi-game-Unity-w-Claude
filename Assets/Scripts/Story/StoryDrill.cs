using System;
using System.Collections;
using BeachVolley.Ball;
using BeachVolley.Court;
using BeachVolley.Player;
using BeachVolley.Skills;
using UnityEngine;

namespace BeachVolley.Story
{
    public enum DrillKind { Passing, Setting, Attacking }

    /// <summary>
    /// The first-training drills with the real gameplay: Steve tosses for passing, Dave feeds for
    /// setting and sets for attacking. A drill is a fixed number of reps; misses never block the
    /// story, they only change what the coach says and how much XP lands.
    /// </summary>
    public class StoryDrill : MonoBehaviour
    {
        [SerializeField] VolleyBall ball;
        [SerializeField] BallContact contact;
        [SerializeField] PlayerSkills skills;
        [SerializeField] Transform steve;
        [SerializeField] Transform dave;
        [SerializeField] Transform marker;
        [SerializeField] StoryUI ui;

        [Header("Spots (world, player side is −z)")]
        [SerializeField] Vector3 passSpot = new Vector3(0f, 0f, -5f);
        [SerializeField] Vector3 setSpot = new Vector3(0f, 0f, -2.6f);
        [SerializeField] Vector3 attackStart = new Vector3(-1.6f, 0f, -4.6f);
        [SerializeField] Vector3 attackPoint = new Vector3(-1.5f, 0f, -1.1f);
        [SerializeField] float repTimeout = 7f;
        [SerializeField] float betweenReps = 1.3f;

        DrillKind kind;
        bool touched, graded, landed, crossed;
        float quality;
        Vector3 landPoint;

        public Vector3 SpotFor(DrillKind k) => k == DrillKind.Passing ? passSpot : k == DrillKind.Setting ? setSpot : attackStart;

        void OnEnable()
        {
            if (contact != null) { contact.Played += OnPlayed; contact.HitGraded += OnGraded; }
            if (ball != null) ball.Landed += OnLanded;
        }

        void OnDisable()
        {
            if (contact != null) { contact.Played -= OnPlayed; contact.HitGraded -= OnGraded; }
            if (ball != null) ball.Landed -= OnLanded;
        }

        void OnPlayed(HitType type, float height) => touched = true;

        void OnGraded(HitType type, float q)
        {
            if (graded) return;
            graded = true;
            quality = q;
        }

        void OnLanded(Vector3 p)
        {
            if (landed) return;
            landed = true;
            landPoint = p;
        }

        /// <summary>Puts the ball at rest by the player's spot and shows the marker there.</summary>
        public void Prepare(DrillKind k)
        {
            Vector3 spot = SpotFor(k);
            if (marker != null)
            {
                marker.gameObject.SetActive(true);
                marker.position = spot;
            }
            ball.Place(spot + new Vector3(0.9f, VolleyBall.Radius + 0.01f, 0.4f));
        }

        public void HideMarker() { if (marker != null) marker.gameObject.SetActive(false); }

        /// <summary>
        /// Runs <paramref name="reps"/> reps (or, with untilSuccess, until one succeeds) and reports
        /// the number of successes. <paramref name="onStruggle"/> plays once after three misses in a row.
        /// </summary>
        public IEnumerator Run(DrillKind k, int reps, Action<int> result, Func<IEnumerator> onStruggle = null, bool untilSuccess = false)
        {
            kind = k;
            Prepare(k);
            int successes = 0, streak = 0;
            bool struggled = false;
            contact.AssistBoost = 1f;

            for (int rep = 0; untilSuccess ? successes == 0 : rep < reps; rep++)
            {
                ui.SetObjective(Objective(k, rep, reps, successes, untilSuccess));
                yield return new WaitForSeconds(betweenReps);
                touched = graded = landed = crossed = false;
                quality = 0f;
                Feed(k);

                float start = Time.time;
                float prevZ = ball.transform.position.z;
                while (!landed && Time.time - start < repTimeout)
                {
                    float z = ball.transform.position.z;
                    // Over the net = crossing z = 0 above the tape after the player played it.
                    if (touched && prevZ < 0f && z >= 0f && ball.transform.position.y > CourtDimensions.NetHeight) crossed = true;
                    prevZ = z;
                    yield return null;
                }

                bool ok = Success(k);
                if (ok) { successes++; streak = 0; }
                else streak++;
                ui.Toast(ok ? Praise(k) : Miss(k), ok ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.7f, 0.45f));
                skills.AddXp(SkillFor(k), ok ? 3f : 1f);
                if (k == DrillKind.Attacking) skills.AddXp(SkillType.Footwork, 0.5f);

                if (untilSuccess && !ok)
                    contact.AssistBoost = Mathf.Min(3f, contact.AssistBoost + 0.5f);

                if (!struggled && streak >= 3 && onStruggle != null)
                {
                    struggled = true;
                    yield return new WaitForSeconds(0.6f);
                    yield return onStruggle();
                }
            }

            contact.AssistBoost = 1f;
            ui.SetObjective(Objective(k, untilSuccess ? 1 : reps, reps, successes, untilSuccess));
            yield return new WaitForSeconds(1f);
            ui.SetObjective(null);
            result?.Invoke(successes);
        }

        bool Success(DrillKind k)
        {
            switch (k)
            {
                case DrillKind.Passing:
                case DrillKind.Setting: return touched && graded && quality >= 0.5f;
                default:
                    if (!crossed) return false;
                    // Over the net counts; landing in is a bonus the toast mentions.
                    return true;
            }
        }

        bool LandedIn => landed && landPoint.z > 0f && Mathf.Abs(landPoint.x) <= CourtDimensions.HalfWidth && landPoint.z <= CourtDimensions.SideLength;

        static SkillType SkillFor(DrillKind k) =>
            k == DrillKind.Passing ? SkillType.Pass : k == DrillKind.Setting ? SkillType.Set : SkillType.Attack;

        string Praise(DrillKind k)
        {
            if (k == DrillKind.Attacking) return LandedIn ? "Over — and in!" : "Over the net!";
            return quality >= 0.9f ? "Perfect touch!" : "Good!";
        }

        string Miss(DrillKind k)
        {
            if (!touched) return "Missed it";
            switch (k)
            {
                case DrillKind.Attacking: return "Into the net…";
                case DrillKind.Setting: return "Wild set";
                default: return "Off target";
            }
        }

        static string Objective(DrillKind k, int rep, int reps, int ok, bool untilSuccess)
        {
            string hint = k switch
            {
                DrillKind.Passing => "LMB (pad West): bump when the ball drops to your waist",
                DrillKind.Setting => "RMB (pad North): set when the ball is above your head",
                _ => "Run in, Space to jump, LMB in the air to spike (early is fine)",
            };
            string title = k switch { DrillKind.Passing => "Passing", DrillKind.Setting => "Setting", _ => "Attacking" };
            string count = untilSuccess ? "one more" : $"{Mathf.Min(rep + 1, reps)}/{reps}   ·   good: {ok}";
            return $"{title}  {count}\n<size=16>{hint}   (click the game first to use the mouse)</size>";
        }

        // ---------- Feeds ----------

        static void Touch(Transform who, HitType type)
        {
            if (who != null && who.TryGetComponent(out StoryActor actor)) actor.Volley(type);
        }

        void Feed(DrillKind k)
        {
            Transform p = skills.transform;
            switch (k)
            {
                case DrillKind.Passing:
                {
                    // Steve's underhand toss to the player's waist (the old coach toss).
                    Vector3 origin = steve.position + steve.forward * 0.45f + Vector3.up * 1.5f;
                    Vector2 scatter = UnityEngine.Random.insideUnitCircle * skills.Scale(SkillType.Pass, 1.2f, 0.3f);
                    Vector3 target = p.position + p.forward * 0.5f + new Vector3(scatter.x, 0.9f, scatter.y);
                    ball.Place(origin);
                    ball.Launch(ball.VelocityToReach(origin, target, 3.4f));
                    break;
                }
                case DrillKind.Setting:
                {
                    // Dave pops a pass up over the player's head.
                    Touch(dave, HitType.Bump);
                    Vector3 origin = dave.position + dave.forward * 0.4f + Vector3.up * 0.9f;
                    Vector3 target = p.position + p.forward * 0.3f + Vector3.up * 2.2f;
                    ball.Place(origin);
                    ball.Launch(ball.VelocityToReach(origin, target, 4.4f));
                    break;
                }
                default:
                {
                    // Dave sets high to the attack point by the net.
                    Touch(dave, HitType.Set);
                    Vector3 origin = dave.position + Vector3.up * 2.1f;
                    Vector3 target = attackPoint + Vector3.up * 2.7f;
                    ball.Place(origin);
                    ball.Launch(ball.VelocityToReach(origin, target, 4.8f));
                    break;
                }
            }
        }
    }
}
