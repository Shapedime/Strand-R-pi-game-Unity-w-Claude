using System;
using System.Collections;
using BeachVolley.Ball;
using BeachVolley.Court;
using BeachVolley.Player;
using BeachVolley.Skills;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BeachVolley.Story
{
    public enum DrillKind
    {
        // Chapter 1
        Passing, Setting, Attacking,
        // Chapter 2, Day 1: control the ball
        PassLR, PassLow, PassPressure,
        // Day 2: build the attack
        SetControl, ThreeTouch, SpikeTiming,
        // Day 3: keep it alive
        ServeTarget, ServeReceive, Pepper, Pancake,
        // Day 4: read the game
        BlockSignal, BlockOrDefend, BlockDig,
    }

    /// <summary>Why a rep went wrong, so the coach can say something that fits.</summary>
    public enum DrillMiss { None, NoTouch, Early, Late, Wild, Net, Out, WrongZone, Blocked, StayedOnNet, TooFar }

    /// <summary>One finished rep, for barks and grading.</summary>
    public struct DrillRep
    {
        public DrillKind kind;
        public int index, successes, streak;
        public bool ok, pancake;
        public float quality;
        public DrillMiss miss;
    }

    /// <summary>A finished drill: how it went, for the coach's verdict and the day's points.</summary>
    public struct DrillResult
    {
        public DrillKind kind;
        public int reps, good, bestStreak, pancakes;
        public float Ratio => reps > 0 ? good / (float)reps : 0f;
    }

    /// <summary>
    /// Story drills with the real gameplay: the feeders (Steve, Dave, club players) toss, hit,
    /// set and serve with the real ball, and each kind has its own success rule. A drill is a fixed
    /// number of reps; misses never block the story, they only change what people say.
    /// The director listens to <see cref="RepEnded"/> for live (non-blocking) barks.
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
        [Tooltip("Optional rings on the sand for zone drills (line / cross), and the 'off the net' line.")]
        [SerializeField] Transform zoneLine, zoneCross, offNetLine;

        [Header("Spots (world, player side is −z)")]
        [SerializeField] Vector3 passSpot = new Vector3(0f, 0f, -5f);
        [SerializeField] Vector3 setSpot = new Vector3(0f, 0f, -2.6f);
        [SerializeField] Vector3 attackStart = new Vector3(-1.6f, 0f, -4.6f);
        [SerializeField] Vector3 attackPoint = new Vector3(-1.5f, 0f, -1.1f);
        [SerializeField] Vector3 serveSpot = new Vector3(0.5f, 0f, -8.7f);
        [SerializeField] Vector3 defendSpot = new Vector3(0f, 0f, -5.6f);
        [SerializeField] Vector3 blockSpot = new Vector3(0.6f, 0f, -0.75f);
        [SerializeField] float repTimeout = 7f;
        [SerializeField] float betweenReps = 1.3f;

        /// <summary>Line and cross landing spots on the player's half for the block drills (attacker hits from +x).</summary>
        public static readonly Vector3 LineSpot = new Vector3(2.7f, 0f, -5.2f);
        public static readonly Vector3 CrossSpot = new Vector3(-2.1f, 0f, -4.8f);
        const float ZoneRadius = 1.3f;
        /// <summary>Behind this z (more negative) counts as "off the net" in Block or Defend.</summary>
        public const float OffNetZ = -3f;

        PlayerServe serve;
        DrillKind kind;
        bool touched, graded, landed, crossed, pancaked, served, blocked;
        float quality;
        HitType gradedType;
        Vector3 landPoint;

        /// <summary>Raised after every rep (the director turns it into barks).</summary>
        public event Action<DrillRep> RepEnded;

        /// <summary>Raised between the signal and the attack in the block drills: 1 = Dave takes line, 2 = cross.</summary>
        public event Action<int> SignalShown;

        /// <summary>Second and third feeders for drills that use more people (left/right tossers, servers, the attacker).</summary>
        public Transform FeederA { get; set; }
        public Transform FeederB { get; set; }

        /// <summary>Day 2 staging: the next three-touch rep gets a set that's far too tight to the net.</summary>
        public bool StageTightSet { get; set; }

        /// <summary>Best streak a rival (Dave) set in the pressure drill, shown as the score to beat.</summary>
        public int ScoreToBeat { get; set; }

        /// <summary>Longest pepper chain in the last pepper drill.</summary>
        public int LongestChain { get; private set; }

        public Vector3 SpotFor(DrillKind k) => k switch
        {
            DrillKind.Setting or DrillKind.SetControl => setSpot,
            DrillKind.Attacking or DrillKind.SpikeTiming => attackStart,
            DrillKind.ThreeTouch => new Vector3(-0.8f, 0f, -5f),
            DrillKind.ServeTarget => serveSpot,
            DrillKind.BlockSignal or DrillKind.BlockDig => defendSpot,
            DrillKind.BlockOrDefend => blockSpot,
            DrillKind.Pepper => new Vector3(0f, 0f, -4.5f),
            _ => passSpot,
        };

        void Awake()
        {
            if (contact != null) serve = contact.GetComponent<PlayerServe>();
            ShowZones(false);
        }

        void OnEnable()
        {
            if (contact != null) { contact.Played += OnPlayed; contact.HitGraded += OnGraded; contact.Pancaked += OnPancake; }
            if (ball != null) ball.Landed += OnLanded;
            if (serve != null) serve.Hit += OnServed;
        }

        void OnDisable()
        {
            if (contact != null) { contact.Played -= OnPlayed; contact.HitGraded -= OnGraded; contact.Pancaked -= OnPancake; }
            if (ball != null) ball.Landed -= OnLanded;
            if (serve != null) serve.Hit -= OnServed;
        }

        void OnPlayed(HitType type, float height)
        {
            touched = true;
            if (type == HitType.Block) blocked = true;
        }

        void OnGraded(HitType type, float q)
        {
            if (graded) return;
            graded = true;
            gradedType = type;
            quality = q;
        }

        void OnPancake(float q) => pancaked = true;
        void OnServed(float side) => served = true;

        void OnLanded(Vector3 p)
        {
            if (landed) return;
            landed = true;
            landPoint = p;
        }

        void ResetRep()
        {
            touched = graded = landed = crossed = pancaked = served = blocked = false;
            quality = 0f;
            gradedType = HitType.None;
        }

        /// <summary>Puts the ball at rest by the player's spot and shows the marker there.</summary>
        public void Prepare(DrillKind k)
        {
            kind = k;
            Vector3 spot = SpotFor(k);
            if (marker != null)
            {
                marker.gameObject.SetActive(true);
                marker.position = spot;
            }
            ball.Place(spot + new Vector3(0.9f, VolleyBall.Radius + 0.01f, 0.4f));
            ShowZones(k == DrillKind.BlockSignal || k == DrillKind.BlockDig || k == DrillKind.BlockOrDefend);
        }

        public void HideMarker()
        {
            if (marker != null) marker.gameObject.SetActive(false);
            ShowZones(false);
        }

        void ShowZones(bool show)
        {
            if (zoneLine != null) { zoneLine.gameObject.SetActive(show && kind != DrillKind.BlockOrDefend); zoneLine.position = LineSpot; }
            if (zoneCross != null) { zoneCross.gameObject.SetActive(show && kind != DrillKind.BlockOrDefend); zoneCross.position = CrossSpot; }
            if (offNetLine != null) offNetLine.gameObject.SetActive(show && kind == DrillKind.BlockOrDefend);
        }

        /// <summary>Chapter 1 form: reports only the number of good reps.</summary>
        public IEnumerator Run(DrillKind k, int reps, Action<int> result, Func<IEnumerator> onStruggle = null, bool untilSuccess = false) =>
            Run(k, reps, r => result?.Invoke(r.good), onStruggle, untilSuccess);

        /// <summary>
        /// Runs <paramref name="reps"/> reps (or, with untilSuccess, until one succeeds) and reports
        /// the result. <paramref name="onStruggle"/> plays once after three misses in a row.
        /// </summary>
        public IEnumerator Run(DrillKind k, int reps, Action<DrillResult> result, Func<IEnumerator> onStruggle = null, bool untilSuccess = false)
        {
            kind = k;
            Prepare(k);
            int successes = 0, streak = 0, best = 0, pancakes = 0, misses = 0;
            bool struggled = false;
            contact.AssistBoost = 1f;
            LongestChain = 0;

            for (int rep = 0; untilSuccess ? successes == 0 : rep < reps; rep++)
            {
                ui.SetObjective(Objective(k, rep, reps, successes, streak, untilSuccess));
                yield return new WaitForSeconds(k == DrillKind.PassPressure && rep > 0 ? 0.7f : betweenReps);
                ResetRep();

                yield return RunRep(k, rep);
                bool ok = repOk;
                if (ok) { successes++; streak++; best = Mathf.Max(best, streak); misses = 0; }
                else { streak = 0; misses++; }
                if (pancaked && touched) pancakes++;
                skills.AddFatigue(k == DrillKind.Pepper ? 0.035f : 0.02f);

                ui.Toast(ok ? Praise(k) : Miss(k, repMiss), ok ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.7f, 0.45f));
                RepEnded?.Invoke(new DrillRep
                {
                    kind = k, index = rep, ok = ok, quality = quality, successes = successes, streak = streak,
                    miss = ok ? DrillMiss.None : repMiss, pancake = pancaked && touched,
                });

                if (untilSuccess && !ok)
                    contact.AssistBoost = Mathf.Min(3f, contact.AssistBoost + 0.5f);

                if (!struggled && misses >= 3 && onStruggle != null)
                {
                    struggled = true;
                    yield return new WaitForSeconds(0.6f);
                    yield return onStruggle();
                }
            }

            contact.AssistBoost = 1f;
            int shownReps = untilSuccess ? 1 : reps;
            ui.SetObjective(Objective(k, shownReps, reps, successes, streak, untilSuccess));
            yield return new WaitForSeconds(1f);
            ui.SetObjective(null);
            result?.Invoke(new DrillResult { kind = k, reps = shownReps, good = successes, bestStreak = best, pancakes = pancakes });
        }

        bool repOk;
        DrillMiss repMiss;

        IEnumerator RunRep(DrillKind k, int rep)
        {
            repOk = false;
            repMiss = DrillMiss.NoTouch;
            switch (k)
            {
                case DrillKind.ThreeTouch: yield return ThreeTouchRep(rep); break;
                case DrillKind.ServeTarget: yield return ServeRep(); break;
                case DrillKind.Pepper: yield return PepperRep(); break;
                case DrillKind.BlockSignal:
                case DrillKind.BlockDig: yield return SignalRep(rep, k == DrillKind.BlockDig); break;
                case DrillKind.BlockOrDefend: yield return BlockOrDefendRep(rep); break;
                default:
                    Feed(k, rep);
                    yield return WatchBall(repTimeout);
                    repOk = Success(k);
                    repMiss = MissFor(k);
                    break;
            }
        }

        /// <summary>Waits until the ball lands (or the timeout), tracking whether it crossed the net after a touch.</summary>
        IEnumerator WatchBall(float timeout)
        {
            float start = Time.time;
            float prevZ = ball.transform.position.z;
            while (!landed && Time.time - start < timeout)
            {
                float z = ball.transform.position.z;
                // Over the net = crossing z = 0 above the tape after the player played it.
                if (touched && prevZ < 0f && z >= 0f && ball.transform.position.y > CourtDimensions.NetHeight) crossed = true;
                prevZ = z;
                yield return null;
            }
        }

        bool Success(DrillKind k)
        {
            switch (k)
            {
                case DrillKind.Attacking:
                case DrillKind.SpikeTiming:
                    return crossed;
                case DrillKind.Pancake:
                    return touched && graded && (gradedType == HitType.Dive || quality >= 0.5f);
                case DrillKind.PassLow:
                case DrillKind.ServeReceive:
                    return touched && graded && quality >= 0.45f;
                default:
                    return touched && graded && quality >= 0.5f;
            }
        }

        DrillMiss MissFor(DrillKind k)
        {
            if (!touched) return DrillMiss.NoTouch;
            if (k == DrillKind.Attacking || k == DrillKind.SpikeTiming) return crossed ? DrillMiss.None : DrillMiss.Net;
            float e = contact.LastTimingError;
            return quality < 0.5f && Mathf.Abs(e) > 0.001f ? (e < 0f ? DrillMiss.Early : DrillMiss.Late) : DrillMiss.Wild;
        }

        bool LandedIn => landed && landPoint.z > 0f && Mathf.Abs(landPoint.x) <= CourtDimensions.HalfWidth && landPoint.z <= CourtDimensions.SideLength;

        static bool InFarCourt(Vector3 p) => p.z > 0f && Mathf.Abs(p.x) <= CourtDimensions.HalfWidth + 0.05f && p.z <= CourtDimensions.SideLength + 0.05f;

        string Praise(DrillKind k)
        {
            if (pancaked && touched) return "Pancake save!";
            switch (k)
            {
                case DrillKind.Attacking:
                case DrillKind.SpikeTiming: return LandedIn ? "Over — and in!" : "Over the net!";
                case DrillKind.ServeTarget: return "In!";
                case DrillKind.ThreeTouch: return LandedIn ? "Three touches — point!" : "Three touches!";
                case DrillKind.Pepper: return $"Chain of {chain}!";
                case DrillKind.BlockSignal:
                case DrillKind.BlockDig: return "Read it — dug!";
                case DrillKind.BlockOrDefend: return blocked ? "Stuff block!" : "Right read!";
                default: return quality >= 0.9f ? "Perfect touch!" : "Good!";
            }
        }

        static string Miss(DrillKind k, DrillMiss m) => m switch
        {
            DrillMiss.NoTouch => k == DrillKind.ServeTarget ? "No serve" : "Missed it",
            DrillMiss.Net => "Into the net…",
            DrillMiss.Out => "Out",
            DrillMiss.WrongZone => "Wrong spot",
            DrillMiss.StayedOnNet => "Caught on the net",
            DrillMiss.TooFar => "Pass too far",
            DrillMiss.Early => "Early",
            DrillMiss.Late => "Late",
            _ => k == DrillKind.Setting || k == DrillKind.SetControl ? "Wild set" : "Off target",
        };

        string Objective(DrillKind k, int rep, int reps, int ok, int streak, bool untilSuccess)
        {
            string hint = k switch
            {
                DrillKind.Passing or DrillKind.PassLR or DrillKind.PassPressure => "LMB (pad West): bump when the ball drops to chest height",
                DrillKind.PassLow => "Get low and bump early  ·  E dives if it's out of reach",
                DrillKind.ServeReceive => "Read the serve, move, bump (LMB)",
                DrillKind.Setting or DrillKind.SetControl => "RMB (pad North): set when the ball is above your head",
                DrillKind.ThreeTouch => "Bump Steve's ball (LMB), then run in, jump and spike Dave's set",
                DrillKind.ServeTarget => "C to toss, then LMB power serve or F short serve",
                DrillKind.Pepper => "Send it back over: F roll shot (or bump, then roll)",
                DrillKind.Pancake => "E dives: press it late, as the ball is about to hit the sand",
                DrillKind.BlockSignal or DrillKind.BlockDig => "Read Dave's signal, cover the open side, bump the attack",
                DrillKind.BlockOrDefend => "Tight set: Q to block.  Bad set: back off the net and dig",
                _ => "Run in, Space to jump, LMB in the air to spike (early is fine)",
            };
            string title = k switch
            {
                DrillKind.Passing => "Passing", DrillKind.Setting => "Setting", DrillKind.Attacking => "Attacking",
                DrillKind.PassLR => "Left and right", DrillKind.PassLow => "Low balls", DrillKind.PassPressure => "Under pressure",
                DrillKind.SetControl => "Setting control", DrillKind.ThreeTouch => "Three-touch attack", DrillKind.SpikeTiming => "Spike timing",
                DrillKind.ServeTarget => "Serving", DrillKind.ServeReceive => "Serve receive", DrillKind.Pepper => "Pepper",
                DrillKind.Pancake => "Pancake", DrillKind.BlockSignal => "Blocking signals", DrillKind.BlockOrDefend => "Block or defend?",
                _ => "Block and dig",
            };
            string count = untilSuccess ? "one more" : $"{Mathf.Min(rep + 1, reps)}/{reps}   ·   good: {ok}";
            if (k == DrillKind.PassPressure) count += $"   ·   streak {streak}" + (ScoreToBeat > 0 ? $"  (Dave: {ScoreToBeat})" : "");
            if (k == DrillKind.Pepper) count = $"{Mathf.Min(rep + 1, reps)}/{reps}   ·   longest chain: {LongestChain}";
            return $"{title}  {count}\n<size=16>{hint}</size>";
        }

        // ---------- Feeds ----------

        static void Touch(Transform who, HitType type)
        {
            if (who != null && who.TryGetComponent(out StoryActor actor)) actor.Volley(type);
        }

        Transform Player => skills.transform;

        void Launch(Vector3 origin, Vector3 target, float apex)
        {
            ball.Place(origin);
            ball.Launch(ball.VelocityToReach(origin, target, apex));
        }

        static Vector3 Hands(Transform who, float height, float forward = 0.4f) =>
            who.position + who.forward * forward + Vector3.up * height;

        void Feed(DrillKind k, int rep)
        {
            Transform p = Player;
            switch (k)
            {
                case DrillKind.Passing:
                {
                    // Steve's underhand toss to the player's waist.
                    Vector2 scatter = Random.insideUnitCircle * skills.Scale(SkillType.Bump, 1.2f, 0.3f);
                    Launch(Hands(steve, 1.5f, 0.45f), p.position + p.forward * 0.5f + new Vector3(scatter.x, 1.15f, scatter.y), 3.4f);
                    break;
                }
                case DrillKind.Setting:
                {
                    // Dave pops a pass up over the player's head.
                    Touch(dave, HitType.Bump);
                    Launch(Hands(dave, 0.9f), p.position + p.forward * 0.3f + Vector3.up * 2.2f, 4.4f);
                    break;
                }
                case DrillKind.PassLR:
                {
                    // Two tossers by the net take turns; the ball goes to alternating sides of the court.
                    Transform who = (rep % 2 == 0 ? FeederA : FeederB) ?? steve;
                    float side = rep % 2 == 0 ? -1f : 1f;
                    var target = new Vector3(side * Random.Range(1.4f, 2.3f), 1.15f, -5f + Random.Range(-0.8f, 0.6f));
                    Touch(who, HitType.Bump);
                    Launch(Hands(who, 1.4f), target, Random.Range(3.4f, 4.2f));
                    break;
                }
                case DrillKind.PassLow:
                {
                    // Steve drives a low, quicker ball at the player: harder to read, less time.
                    Touch(steve, HitType.Spike);
                    Vector3 off = new Vector3(Random.Range(-1.4f, 1.4f), 0f, Random.Range(-0.6f, 0.8f));
                    Launch(Hands(steve, 2.1f), p.position + off + Vector3.up * 0.95f, 2.7f);
                    break;
                }
                case DrillKind.PassPressure:
                {
                    // Anyone, anywhere, any pace.
                    Transform[] feeders = { steve, FeederA ?? steve, FeederB ?? steve };
                    Transform who = feeders[Random.Range(0, feeders.Length)];
                    var target = new Vector3(Random.Range(-2.6f, 2.6f), 1.15f, Random.Range(-6.4f, -3.8f));
                    Touch(who, HitType.Bump);
                    Launch(Hands(who, 1.4f), target, Random.Range(2.6f, 4.3f));
                    break;
                }
                case DrillKind.SetControl:
                {
                    // Steve tosses from the side: alternating a high, easy ball and a quicker, flatter one.
                    bool high = rep % 2 == 0;
                    Touch(steve, HitType.Bump);
                    Vector3 off = new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.5f, 0.4f));
                    Launch(Hands(steve, 1.2f), p.position + off + Vector3.up * 2.2f, high ? 5f : 3.6f);
                    break;
                }
                case DrillKind.SpikeTiming:
                {
                    // Dave sets from the net, mixing a high ball and a faster one, left or right.
                    float x = rep % 3 == 2 ? 1.5f : -1.5f;
                    if (marker != null) marker.position = new Vector3(x - 0.1f, 0f, attackStart.z);
                    Touch(dave, HitType.Set);
                    Launch(dave.position + Vector3.up * 2.1f, new Vector3(x, 2.7f, attackPoint.z), rep % 2 == 0 ? 4.9f : 4.2f);
                    break;
                }
                case DrillKind.ServeReceive:
                {
                    // A float serve from the far baseline toward the player.
                    Transform who = FeederA ?? steve;
                    Touch(who, HitType.Spike);
                    Vector3 off = new Vector3(Random.Range(-1.6f, 1.6f), 0f, Random.Range(-1f, 1f));
                    Launch(Hands(who, 2.4f, 0.5f), p.position + off + Vector3.up * 1.15f, Random.Range(3.9f, 4.6f));
                    break;
                }
                case DrillKind.Pancake:
                {
                    // Dropped just out of reach, low and short: walking there won't make it.
                    Transform who = FeederA ?? steve;
                    Vector3 dir = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * p.forward;
                    Vector3 land = p.position + dir * Random.Range(2.1f, 2.7f);
                    Touch(who, HitType.Bump);
                    Launch(Hands(who, 1.3f), land + Vector3.up * 0.12f, Random.Range(1.9f, 2.4f));
                    break;
                }
                default:
                {
                    // Dave sets high to the attack point by the net.
                    Touch(dave, HitType.Set);
                    Launch(dave.position + Vector3.up * 2.1f, attackPoint + Vector3.up * 2.7f, 4.8f);
                    break;
                }
            }
        }

        // ---------- Multi-touch reps ----------

        /// <summary>Steve's free ball → the player passes → Dave sets → the player attacks.</summary>
        IEnumerator ThreeTouchRep(int rep)
        {
            Transform p = Player;
            Touch(steve, HitType.Bump);
            Vector3 off = new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.5f, 0.5f));
            Launch(Hands(steve, 1.4f), p.position + off + Vector3.up * 1.15f, 3.8f);

            // Touch one: the pass.
            float start = Time.time;
            while (!touched && !landed && Time.time - start < repTimeout) yield return null;
            if (!touched) { repMiss = DrillMiss.NoTouch; yield return WatchBall(3f); yield break; }

            // Dave reads the pass and gets under it.
            Vector3 landing = ball.PredictLanding();
            Vector3 davePos = dave.position;
            float reach = Vector3.Distance(new Vector3(landing.x, 0f, landing.z), new Vector3(davePos.x, 0f, davePos.z));
            if (reach > 3.2f || landing.z > 0f)
            {
                repMiss = DrillMiss.TooFar;
                yield return WatchBall(3f);
                yield break;
            }
            var daveActor = dave.GetComponent<StoryActor>();
            if (daveActor != null) daveActor.WalkTo(landing);
            float t0 = Time.time;
            // Set when the ball comes down to his hands (or it's about to hit the sand).
            while (!landed && Time.time - t0 < 3f && !(ball.Body.linearVelocity.y < 0f && ball.transform.position.y < 2.3f))
                yield return null;
            if (landed) { repMiss = DrillMiss.TooFar; yield break; }
            if (daveActor != null) daveActor.Stop();
            Touch(dave, HitType.Set);
            float x = p.position.x <= 0f ? -1.5f : 1.5f;
            float z = StageTightSet ? -0.25f : attackPoint.z;
            StageTightSet = false;
            touched = graded = false;
            Vector3 from = ball.transform.position;
            ball.Launch(ball.VelocityToReach(from, new Vector3(x, 2.7f, z), 4.7f));

            // Touch three: the attack.
            yield return WatchBall(repTimeout);
            repOk = crossed;
            repMiss = !touched ? DrillMiss.NoTouch : crossed ? DrillMiss.None : DrillMiss.Net;
        }

        /// <summary>The player serves; it has to land in the far court.</summary>
        IEnumerator ServeRep()
        {
            ball.Place(Player.position + Player.forward * 0.6f + new Vector3(0f, VolleyBall.Radius + 0.01f, 0f));
            float start = Time.time;
            while (!served && Time.time - start < 12f) yield return null;
            if (!served) { repMiss = DrillMiss.NoTouch; yield break; }
            landed = false;
            touched = true;
            float prevZ = ball.transform.position.z;
            bool overNet = false;
            float t0 = Time.time;
            while (!landed && Time.time - t0 < 6f)
            {
                float z = ball.transform.position.z;
                if (prevZ < 0f && z >= 0f && ball.transform.position.y > CourtDimensions.NetHeight) overNet = true;
                prevZ = z;
                yield return null;
            }
            // A receiver near the landing spot reacts.
            foreach (var r in new[] { FeederA, FeederB })
                if (r != null && Vector3.Distance(r.position, landPoint) < 2f) Touch(r, HitType.Bump);
            repOk = overNet && InFarCourt(landPoint);
            repMiss = repOk ? DrillMiss.None : overNet ? DrillMiss.Out : DrillMiss.Net;
        }

        int chain;

        /// <summary>
        /// Keep it alive with Dave across the net: he sends a soft ball, the player gets it back over
        /// (roll shot, or bump then roll), Dave chases it down and sends it again. One rep = one chain.
        /// </summary>
        IEnumerator PepperRep()
        {
            chain = 0;
            var daveActor = dave.GetComponent<StoryActor>();
            Vector3 home = dave.position;
            bool alive = true;
            while (alive)
            {
                // Dave's ball over to the player.
                Touch(dave, chain % 2 == 0 ? HitType.Set : HitType.Bump);
                Vector3 off = new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-0.8f, 0.8f));
                Vector3 to = Player.position + off;
                to.z = Mathf.Clamp(to.z, -7f, -2.5f);
                // The first ball starts in his hands; after that he plays it wherever he caught up with it.
                Vector3 from = chain == 0 ? Hands(dave, 1.6f) : ball.transform.position;
                if (chain == 0) ball.Place(from);
                ball.Launch(ball.ArcOverNet(from, to + Vector3.up * 1.15f, 3.9f, CourtDimensions.NetHeight + 0.5f));
                ResetRep();

                // The player has to get it back over before it lands.
                float t0 = Time.time;
                float prevZ = ball.transform.position.z;
                while (!landed && !crossed && Time.time - t0 < 8f)
                {
                    float z = ball.transform.position.z;
                    if (touched && prevZ < 0f && z >= 0f && ball.transform.position.y > CourtDimensions.NetHeight) crossed = true;
                    prevZ = z;
                    yield return null;
                }
                if (!crossed) { alive = false; break; }

                // Dave chases it; if he gets there it counts and the chain goes on.
                Vector3 landing = ball.PredictLanding();
                if (!InFarCourt(landing) || Vector3.Distance(new Vector3(landing.x, 0f, landing.z), new Vector3(dave.position.x, 0f, dave.position.z)) > 3.6f)
                {
                    alive = false;
                    yield return WatchBall(4f);
                    break;
                }
                chain++;
                LongestChain = Mathf.Max(LongestChain, chain);
                ui.SetObjective(Objective(DrillKind.Pepper, 0, 1, 0, 0, false).Replace("1/1   ·   ", $"chain {chain}   ·   "));
                if (daveActor != null) daveActor.WalkTo(landing);
                while (!(ball.Body.linearVelocity.y < 0f && ball.transform.position.y < 1.1f) && !landed) yield return null;
                if (daveActor != null) { daveActor.Stop(); daveActor.WalkTo(home); }
                if (chain >= 8) break;
            }
            repOk = chain >= 3;
            repMiss = repOk ? DrillMiss.None : touched ? DrillMiss.Wild : DrillMiss.NoTouch;
            if (daveActor != null) daveActor.WalkTo(home);
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>
        /// Dave at the net shows a signal (1 = he blocks line, 2 = cross); the player has to cover the
        /// other shot. The attacker hits where the block isn't. Harder: faster, less time to move,
        /// and Dave turns to set the dig.
        /// </summary>
        IEnumerator SignalRep(int rep, bool harder)
        {
            Transform attacker = FeederA ?? steve;
            Transform setter = FeederB;
            int fingers = Random.value < 0.5f ? 1 : 2;
            if (rep == 0) fingers = 1;
            Vector3 open = fingers == 1 ? CrossSpot : LineSpot;
            Vector3 shut = fingers == 1 ? LineSpot : CrossSpot;

            ui.ShowSignal(fingers, harder ? null : fingers == 1 ? "Dave: I've got the line" : "Dave: I've got the cross");
            SignalShown?.Invoke(fingers);
            yield return new WaitForSeconds(harder ? 0.9f : 1.6f);
            ui.ShowSignal(0, null);

            // Their set, then the swing.
            Vector3 hitPoint = attacker.position + new Vector3(-0.3f, 2.85f, -0.5f);
            if (setter != null) Touch(setter, HitType.Set);
            ball.Place((setter != null ? setter.position : attacker.position + new Vector3(-2f, 0f, 1f)) + Vector3.up * 2.1f);
            ball.Launch(ball.VelocityToReach(ball.transform.position, hitPoint, 4.6f));
            yield return SwingAt(attacker, HitType.Spike, hitPoint.y);
            Touch(dave, HitType.Block);

            // Where's the player when the ball is hit?
            Vector3 pp = Player.position;
            bool covering = Flat(pp - open) <= ZoneRadius;
            bool wrong = Flat(pp - shut) <= ZoneRadius;
            Vector3 target = open + new Vector3(Random.Range(-0.4f, 0.4f), 1.05f, Random.Range(-0.4f, 0.4f));
            if (covering) target = Vector3.Lerp(target, pp + Vector3.up * 1.05f, 0.6f);
            Vector3 from = ball.transform.position;
            float pace = harder ? 10.5f : 8f;
            ball.Launch(ball.DriveOverNet(from, target, CourtDimensions.NetHeight + 0.45f, pace, 5f));
            yield return WatchBall(4f);

            repOk = covering && touched && graded && quality >= 0.35f;
            repMiss = repOk ? DrillMiss.None : !covering ? DrillMiss.WrongZone : !touched ? DrillMiss.NoTouch : DrillMiss.Wild;
            if (wrong && !covering) repMiss = DrillMiss.WrongZone;

            if (repOk && harder)
            {
                // Dave turns and sets the dig, the way it would go on in a rally.
                Touch(dave, HitType.Set);
            }
        }

        /// <summary>
        /// The player is the blocker. A tight set means a real attack: block it (Q). A set far off the
        /// net means a soft shot is coming: back off past the line and dig it.
        /// </summary>
        IEnumerator BlockOrDefendRep(int rep)
        {
            Transform attacker = FeederA ?? steve;
            Transform setter = FeederB;
            bool tight = rep == 0 || (rep != 1 && Random.value < 0.5f);
            Vector3 hitPoint = tight
                ? new Vector3(Player.position.x + Random.Range(-0.3f, 0.3f), 2.95f, 0.55f)
                : new Vector3(Random.Range(-1.5f, 1.5f), 2.3f, 3.6f);

            if (setter != null) Touch(setter, HitType.Set);
            Vector3 setFrom = (setter != null ? setter.position : new Vector3(-1f, 0f, 2f)) + Vector3.up * 2.1f;
            ball.Place(setFrom);
            ball.Launch(ball.VelocityToReach(setFrom, hitPoint, tight ? 4.9f : 3.6f));
            var attackerActor = attacker.GetComponent<StoryActor>();
            if (attackerActor != null) attackerActor.WalkTo(new Vector3(hitPoint.x, 0f, hitPoint.z + 0.4f));

            // A sharp player reads the set early.
            if (skills.Get(SkillType.CourtAwareness) >= 3)
                ui.Toast(tight ? "Read: tight set — block!" : "Read: off the net — back off!", new Color(0.7f, 0.85f, 1f), 1.2f);

            if (attackerActor != null) attackerActor.Stop();
            yield return SwingAt(attacker, tight ? HitType.Spike : HitType.Roll, hitPoint.y);
            bool offNet = Player.position.z <= OffNetZ;
            ResetRep();

            Vector3 from = ball.transform.position;
            if (tight)
            {
                // Hard, straight at the block's side of the court.
                var target = new Vector3(Mathf.Clamp(from.x + Random.Range(-0.5f, 0.5f), -3.5f, 3.5f), VolleyBall.Radius, -4.5f);
                ball.Launch(ball.DriveOverNet(from, target, CourtDimensions.NetHeight + 0.2f, 11f, 6f));
            }
            else
            {
                // A controlled shot deep into the court — where a blocker who stayed home leaves a hole.
                var target = new Vector3(Random.Range(-2f, 2f), 1.1f, Random.Range(-5.4f, -4.2f));
                if (offNet) target = Vector3.Lerp(target, Player.position + Vector3.up * 1.1f, 0.55f);
                ball.Launch(ball.ArcOverNet(from, target, 3.8f, CourtDimensions.NetHeight + 0.4f));
            }
            yield return WatchBall(4f);

            if (tight)
            {
                repOk = blocked;
                repMiss = repOk ? DrillMiss.None : touched ? DrillMiss.Wild : DrillMiss.NoTouch;
            }
            else
            {
                repOk = offNet && touched && graded && quality >= 0.35f;
                repMiss = repOk ? DrillMiss.None : !offNet ? DrillMiss.StayedOnNet : !touched ? DrillMiss.NoTouch : DrillMiss.Wild;
            }
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        /// <summary>
        /// An opponent's attack: the swing starts a beat before contact (so a blocker can read it),
        /// and this returns when the ball has come down to the hitting height.
        /// </summary>
        IEnumerator SwingAt(Transform attacker, HitType type, float hitHeight)
        {
            float t0 = Time.time;
            bool swung = false;
            while (Time.time - t0 < 3.5f)
            {
                bool falling = ball.Body.linearVelocity.y < 0f;
                if (!swung && ball.PredictTimeToHeight(hitHeight, 3f, out float until) && until <= 0.35f)
                {
                    Touch(attacker, type);
                    swung = true;
                }
                if (falling && ball.transform.position.y < hitHeight + 0.1f) break;
                yield return null;
            }
            if (!swung) Touch(attacker, type);
        }
    }
}
