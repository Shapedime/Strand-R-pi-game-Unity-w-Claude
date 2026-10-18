using System;
using System.Collections;
using System.Collections.Generic;
using BeachVolley.Ball;
using BeachVolley.Court;
using BeachVolley.Player;
using BeachVolley.Skills;
using BeachVolley.Story;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BeachVolley.Match
{
    public enum PointReason { Kill, Ace, Out, Net, Fault, Block, Gap, Error }

    /// <summary>One scored point, for the director's dialogue.</summary>
    public struct MatchPoint
    {
        public bool nearWon;
        public PointReason reason;
        public bool humanLastTouch;
        public CourtAI lastAi;
        public Vector3 landedAt;
        public int rallyTouches, nearScore, farScore, index;
    }

    /// <summary>What happened in a match, for branching dialogue afterwards.</summary>
    public class MatchStats
    {
        public int humanTouches, humanGoodTouches, humanErrors, humanKills, humanSaves, humanPancakes;
        public int gapBallsLost, longestRally, nearPoints, farPoints;
        public bool NearWon => nearPoints > farPoints;
        /// <summary>Did the protagonist play well (by beginner standards)?</summary>
        public bool PlayedWell => humanTouches > 0 && humanGoodTouches * 2 >= humanTouches && humanErrors <= humanGoodTouches;
    }

    /// <summary>
    /// A real 2v2 beach game against NPCs: the protagonist (near side, −z) with an AI partner
    /// against two AIs. The AIs read the real ball, call it, pass to the setter spot, set the hitter
    /// and attack the open court, serve, block and dive, with errors from their profiles. Rally
    /// scoring to 10 (win by 2, capped at 12). The director can stage moments (a gap ball the
    /// partner leaves, a partner attack into the net) and talk between points.
    /// </summary>
    public class StoryMatch : MonoBehaviour
    {
        [SerializeField] VolleyBall ball;
        [SerializeField] BallContact contact;
        [SerializeField] StoryUI ui;

        [Header("Rules")]
        [SerializeField] int targetScore = 10;
        [SerializeField] int scoreCap = 12;
        [SerializeField] float rallyTimeout = 70f;
        [Tooltip("Logs serves, attacks and stuck rallies to the console (for tuning).")]
        public bool debugLog;

        // ---------- Teams ----------

        class Team
        {
            public string name;
            public float side;          // −1 near, +1 far
            public CourtAI[] ais;       // the near team has one (the partner), the far team two
            public bool human;
            public int score, touches, serverIndex;
            public int Members => ais.Length + (human ? 1 : 0);
        }

        Team near, far;
        PlayerSkills skills;
        PlayerServe serve;
        Transform player;

        Team serving;
        Team lastTouchTeam;
        CourtAI lastAi;             // null = the human touched last
        bool humanTouchedLast;
        int rallyTouches;

        // Who is going for the ball next on each side (null CourtAI + flag = the human).
        CourtAI nextAi;
        bool nextIsHuman;
        Team ballTeam;              // team whose side the ball is on / heading to
        float lastSide;
        bool rallyOver, fault;
        PointReason faultReason;
        Vector3 landedAt;
        bool landed;
        bool humanServed;
        CourtAI aiActor;            // the AI that just touched (can't touch twice in a row)
        CourtAI blocker;            // the far/near AI going up to block this attack
        bool diveTried;
        float speedBeforeTouch;
        float sideChangedAt;        // when the ball last crossed the net (AIs need a moment to read it)
        float digSpeed;             // how hard the ball came over, for the first touch
        bool beaten;                // the defender was too late for this ball: it goes by

        // Staging
        bool stageGap;
        CourtAI hesitating;
        CourtAI netNextAttack;
        Vector3? forcedAttack;

        public MatchStats Stats { get; private set; } = new MatchStats();
        public int NearScore => near?.score ?? 0;
        public int FarScore => far?.score ?? 0;
        public bool Finished { get; private set; }

        /// <summary>The next touch on the protagonist's side is his (the partner is leaving it).</summary>
        public bool BallIsYours => nextIsHuman;

        /// <summary>Called between points (gameplay paused); the director can talk here.</summary>
        public Func<MatchPoint, IEnumerator> BetweenPoints { get; set; }

        /// <summary>Raised the moment a point ends.</summary>
        public event Action<MatchPoint> PointScored;

        /// <summary>The HUD line under the score (the director can add a reminder).</summary>
        public string Hint { get; set; }

        void Awake()
        {
            if (contact != null)
            {
                player = contact.transform;
                skills = contact.GetComponent<PlayerSkills>();
                serve = contact.GetComponent<PlayerServe>();
            }
        }

        void OnEnable()
        {
            if (contact != null) { contact.Played += OnHumanPlayed; contact.HitGraded += OnHumanGraded; contact.Pancaked += OnHumanPancake; }
            if (serve != null) serve.Hit += OnHumanServed;
            if (ball != null) ball.Landed += OnLanded;
        }

        void OnDisable()
        {
            if (contact != null) { contact.Played -= OnHumanPlayed; contact.HitGraded -= OnHumanGraded; contact.Pancaked -= OnHumanPancake; }
            if (serve != null) serve.Hit -= OnHumanServed;
            if (ball != null) ball.Landed -= OnLanded;
        }

        /// <summary>Sets up a game: the protagonist + <paramref name="partner"/> vs <paramref name="farA"/> + <paramref name="farB"/>.</summary>
        public void Setup(string nearName, CourtAI partner, string farName, CourtAI farA, CourtAI farB, float farBias = 1f, float partnerBias = 1f)
        {
            near = new Team { name = nearName, side = -1f, ais = new[] { partner }, human = true };
            far = new Team { name = farName, side = 1f, ais = new[] { farA, farB } };
            partner.Live = partner.profile.Scaled(partnerBias);
            farA.Live = farA.profile.Scaled(farBias);
            farB.Live = farB.profile.Scaled(farBias);
            Stats = new MatchStats();
            Finished = false;
            serving = far;
            stageGap = false;
            hesitating = netNextAttack = null;
            forcedAttack = null;
        }

        // ---------- Staging ----------

        /// <summary>The far team's next attack goes into the gap between the protagonist and the partner, and the partner leaves it.</summary>
        public void StageGapBall() => stageGap = true;

        /// <summary>True while a staged gap ball is still waiting for the far team's next attack.</summary>
        public bool GapPending => stageGap;

        public void CancelGap() => stageGap = false;

        /// <summary>This AI's next attack goes into the net.</summary>
        public void StageNetAttack(CourtAI ai) => netNextAttack = ai;

        /// <summary>The next attack by the far team goes exactly here.</summary>
        public void ForceNextAttack(Vector3 target) => forcedAttack = target;

        // ---------- The game ----------

        public IEnumerator Play()
        {
            foreach (var ai in AllAis) ai.BeginMatch();
            contact.ShowControlsHint = true;
            int index = 0;
            while (!Finished)
            {
                yield return Rally();
                var point = Resolve(index++);
                PointScored?.Invoke(point);
                UpdateBoard();
                skills.AddFatigue(0.03f);
                ui.SetEnergy(skills.Fatigue);
                yield return new WaitForSeconds(1.1f);
                Finished = IsOver();
                if (BetweenPoints != null) yield return BetweenPoints(point);
            }
            Stats.nearPoints = near.score;
            Stats.farPoints = far.score;
            foreach (var ai in AllAis) ai.EndMatch();
            ui.SetObjective(null);
        }

        IEnumerable<CourtAI> AllAis
        {
            get
            {
                foreach (var a in near.ais) yield return a;
                foreach (var a in far.ais) yield return a;
            }
        }

        bool IsOver()
        {
            int a = near.score, b = far.score;
            if (Mathf.Max(a, b) >= scoreCap) return true;
            return Mathf.Max(a, b) >= targetScore && Mathf.Abs(a - b) >= 2;
        }

        void UpdateBoard()
        {
            string line = $"{near.name}   {near.score} : {far.score}   {far.name}";
            string sub = string.IsNullOrEmpty(Hint) ? "First to 10 (win by 2)" : Hint;
            ui.SetObjective(line + $"\n<size=16>{sub}</size>");
        }

        // ---------- One rally ----------

        IEnumerator Rally()
        {
            rallyOver = fault = landed = humanServed = false;
            rallyTouches = 0;
            near.touches = far.touches = 0;
            aiActor = null;
            blocker = null;
            nextAi = null;
            nextIsHuman = false;
            diveTried = false;
            beaten = false;
            UpdateBoard();
            yield return ServePhase();
            if (rallyOver) yield break;

            float start = Time.time;
            float still = 0f;
            while (!landed && !fault && Time.time - start < rallyTimeout)
            {
                // A ball resting on something that isn't sand (a car roof, a post) or gone off the
                // beach is out of play where it is.
                Vector3 bp = ball.transform.position;
                still = !ball.IsDead && ball.Body.linearVelocity.sqrMagnitude < 0.25f && bp.y < 2.5f ? still + Time.deltaTime : 0f;
                if (still > 0.8f || Mathf.Abs(bp.x) > 16f || Mathf.Abs(bp.z) > 18f || bp.y < -1f)
                {
                    landed = true;
                    landedAt = new Vector3(bp.x, 0f, bp.z);
                    if (debugLog) Debug.Log($"[StoryMatch] ball out of play at {bp:F1} (resting or off the beach)");
                    break;
                }
                TrackSide();
                speedBeforeTouch = ball.Body.linearVelocity.magnitude;
                Brain(near);
                Brain(far);
                yield return null;
            }
            if (!landed && !fault)
            {
                Debug.LogWarning($"[StoryMatch] Rally stuck: ball at {ball.transform.position:F2}, velocity {ball.Body.linearVelocity:F2}, dead={ball.IsDead}, touches={rallyTouches}, ballTeam={(ballTeam == near ? "near" : "far")}, next={(nextAi != null ? nextAi.DisplayName : nextIsHuman ? "you" : "-")}");
                // Something got stuck: replay the point, nobody scores.
                fault = true;
                faultReason = PointReason.Error;
                lastTouchTeam = null;
            }
            foreach (var ai in AllAis) ai.Hold();
        }

        /// <summary>Lines everyone up, then the server serves (the protagonist: C, then LMB/F).</summary>
        IEnumerator ServePhase()
        {
            Team recv = serving == near ? far : near;
            // Start from a dead ball, so nothing from the last rally can land during the serve.
            ball.Place(new Vector3(0f, VolleyBall.Radius + 0.01f, serving.side * 9.5f));
            int server = serving.serverIndex % serving.Members;
            serving.serverIndex++;
            bool humanServes = serving.human && server == serving.Members - 1;

            // Receivers spread across the court; the human always takes the left lane.
            Formation(recv, receiving: true, serverSlot: -1);
            Formation(serving, receiving: false, serverSlot: humanServes ? -2 : server);
            lastTouchTeam = serving;
            ballTeam = recv;

            if (humanServes)
            {
                Vector3 spot = new Vector3(-1f, 0f, -8.7f);
                PlacePlayer(spot, Quaternion.identity);
                ball.Place(spot + new Vector3(0.6f, VolleyBall.Radius + 0.01f, 0.5f));
                Hint = "Your serve:  C to toss, then LMB power serve or F short serve";
                UpdateBoard();
                float t0 = Time.time;
                while (!humanServed)
                {
                    if (landed && !humanServed) landed = false; // a dropped toss: just toss again
                    yield return null;
                    if (Time.time - t0 > 40f) break;
                }
                Hint = null;
                UpdateBoard();
                lastTouchTeam = near;
                humanTouchedLast = true;
                lastAi = null;
            }
            else
            {
                var srv = serving.ais[Mathf.Min(server, serving.ais.Length - 1)];
                yield return new WaitForSeconds(1.3f);
                AiServe(srv, serving, recv);
                landed = false;
                lastTouchTeam = serving;
                humanTouchedLast = false;
                lastAi = srv;
                aiActor = srv;
            }
            lastSide = Mathf.Sign(ball.transform.position.z);
            AssignReceiver(recv);
        }

        void Formation(Team t, bool receiving, int serverSlot)
        {
            float s = t.side;
            Vector3 net = Vector3.zero;
            if (t.human)
            {
                var partner = t.ais[0];
                if (receiving)
                {
                    PlacePlayer(new Vector3(-1.8f, 0f, s * 5.2f), Quaternion.identity);
                    partner.Teleport(new Vector3(1.8f, 0f, s * 5.2f), net);
                }
                else if (serverSlot == -2)
                    partner.Teleport(new Vector3(1.6f, 0f, s * 3f), net);
                else
                {
                    PlacePlayer(new Vector3(-1.8f, 0f, s * 3.4f), Quaternion.identity);
                    partner.Teleport(new Vector3(1.2f, 0f, s * 8.8f), net);
                }
                return;
            }
            for (int i = 0; i < t.ais.Length; i++)
            {
                float lane = i == 0 ? -1.8f : 1.8f;
                Vector3 at = receiving ? new Vector3(lane, 0f, s * 5.2f)
                    : i == serverSlot ? new Vector3(lane * 0.6f, 0f, s * 8.8f) : new Vector3(lane, 0f, s * 3.2f);
                t.ais[i].Teleport(at, net);
            }
        }

        void PlacePlayer(Vector3 at, Quaternion facing)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.SetPositionAndRotation(at, facing);
            if (cc != null) cc.enabled = true;
        }

        // ---------- Who plays the ball ----------

        void TrackSide()
        {
            float s = Mathf.Sign(ball.transform.position.z);
            if (Mathf.Abs(ball.transform.position.z) < 0.02f || s == lastSide) return;
            lastSide = s;
            Team now = s < 0f ? near : far;
            now.touches = 0;
            ballTeam = now;
            blocker = null;
            diveTried = false;
            sideChangedAt = Time.time;
            beaten = false;
            digSpeed = ball.Body.linearVelocity.magnitude;
            AssignReceiver(now);
        }

        /// <summary>The ball is coming to <paramref name="t"/>: decide who takes the first touch, and call it.</summary>
        void AssignReceiver(Team t)
        {
            Vector3 land = ball.PredictLanding();
            nextAi = null;
            nextIsHuman = false;
            if (t.human)
            {
                var partner = t.ais[0];
                float dh = Flat(player.position - land), dp = Flat(partner.Ground - land);
                bool humanLane = land.x < 0.2f;
                if (hesitating == partner)
                {
                    // Staged: the partner freezes, as if he thought it was the protagonist's.
                    nextIsHuman = true;
                    return;
                }
                if ((humanLane && dh < dp + 1.2f) || dh + 0.6f < dp)
                {
                    nextIsHuman = true;
                    if (partner.Live.callsEarly) ui.Bark(partner.DisplayName, partner.Live.callYours, 1.4f);
                }
                else
                {
                    nextAi = partner;
                    if (partner.Live.callsEarly) ui.Bark(partner.DisplayName, partner.Live.callMine, 1.2f);
                }
                return;
            }
            // Two AIs: the nearer one goes.
            nextAi = Flat(t.ais[0].Ground - land) <= Flat(t.ais[1].Ground - land) ? t.ais[0] : t.ais[1];
        }

        /// <summary>After a touch by a team member, the other one takes the next.</summary>
        void PassBaton(Team t, CourtAI toucher, bool humanTouched)
        {
            if (t.touches >= 3) { nextAi = null; nextIsHuman = false; return; }
            if (t.human)
            {
                if (humanTouched) { nextAi = t.ais[0]; nextIsHuman = false; }
                else { nextAi = null; nextIsHuman = true; }
                return;
            }
            nextAi = toucher == t.ais[0] ? t.ais[1] : t.ais[0];
            nextIsHuman = false;
        }

        // ---------- AI behaviour ----------

        void Brain(Team t)
        {
            bool ballHere = ballTeam == t && !ball.IsDead;
            foreach (var ai in t.ais)
            {
                ai.Face(ball.transform.position);
                if (ballHere && ai == nextAi && ai != aiActor && ai != hesitating) GoForBall(t, ai);
                else if (ai == hesitating && ballHere) ai.Hold();
                else Position(t, ai, ballHere);
            }
        }

        /// <summary>Where an AI waits when it isn't playing the ball.</summary>
        void Position(Team t, CourtAI ai, bool ballHere)
        {
            float s = t.side;
            if (ballHere)
            {
                // Our ball: the one who just touched gets ready to attack (or cover) in their lane.
                float lane = t.human ? 1.7f : (ai == t.ais[0] ? -1.7f : 1.7f);
                ai.MoveTo(new Vector3(lane, 0f, s * (t.touches >= 1 ? 3.6f : 4.6f)));
                return;
            }
            // Their ball: blockers come to the net when they set; defenders split the court.
            Team other = t == near ? far : near;
            bool theySet = other.touches >= 2 || (other.touches >= 1 && ball.transform.position.y > 3f);
            if (IsBlocker(t, ai) && theySet && Mathf.Abs(ball.transform.position.z) < 3.5f)
            {
                blocker = ai;
                ai.MoveTo(new Vector3(Mathf.Clamp(ball.PredictLanding().x, -3f, 3f), 0f, s * 0.6f));
                return;
            }
            float laneX;
            if (t.human) laneX = player.position.x < 0f ? 1.8f : -1.8f;
            else laneX = ai == t.ais[0] ? -1.8f : 1.8f;
            ai.MoveTo(new Vector3(laneX, 0f, s * 4.6f));
        }

        /// <summary>
        /// Who goes up to block: a player whose profile blocks; on a team of two AIs without one, the
        /// better blocker of the two (a beach team always puts someone at the net against a set).
        /// The protagonist's partner only blocks if his profile says so.
        /// </summary>
        bool IsBlocker(Team t, CourtAI ai)
        {
            if (ai.Live.blocks) return true;
            if (t.human || t.ais.Length < 2) return false;
            var other = ai == t.ais[0] ? t.ais[1] : t.ais[0];
            if (other.Live.blocks) return false;
            return ai.Live.blocking > other.Live.blocking || (ai.Live.blocking == other.Live.blocking && ai == t.ais[1]);
        }

        /// <summary>Spike and block jump heights: the better the player, the higher they get.</summary>
        static float SpikeJump(NpcProfile p) => Mathf.Lerp(0.55f, 1.05f, NpcProfile.N(p.attacking));
        static float BlockJump(NpcProfile p) => Mathf.Lerp(0.45f, 0.85f, NpcProfile.N(p.blocking));

        void GoForBall(Team t, CourtAI ai)
        {
            int touch = t.touches + 1;
            bool spike = touch == 3 && CanSpike(t);
            float h = touch == 1 ? 1.15f : touch == 2 ? 2.1f : spike ? 2.85f : 1.2f;

            Vector3 meet;
            float until;
            if (ball.PredictTimeToHeight(h, 4f, out until) && until > 0f) meet = ball.PredictPositionAt(until);
            else { meet = ball.PredictLanding(); until = 0.05f; }

            if (touch == 1 && beaten) return;
            // Reading the ball takes a moment after it crosses: better defenders start sooner.
            if (touch == 1 && Time.time - sideChangedAt < Mathf.Lerp(0.38f, 0.1f, NpcProfile.N(ai.Live.defense)))
            {
                ai.Hold();
                return;
            }

            // Stand a little behind the ball (facing the net) so it comes down in front.
            Vector3 stand = new Vector3(meet.x, 0f, meet.z + t.side * 0.35f);
            ai.MoveTo(stand);

            if (spike && until < 0.45f && !ai.IsAirborne) ai.Jump(SpikeJump(ai.Live), 0.85f);

            // Contact.
            Vector3 b = ball.transform.position;
            float reach = touch == 1 ? 0.8f + NpcProfile.N(ai.Live.defense) * 0.4f : 0.9f;
            bool inReach = Flat(b - ai.Ground) <= reach;
            bool heightOk = touch == 1 ? b.y <= 1.9f : touch == 2 ? b.y <= 2.6f : b.y <= (spike ? 3.4f : 1.8f);
            bool onOurSide = b.z * t.side > -0.1f;
            if (inReach && heightOk && onOurSide && (ball.Body.linearVelocity.y < 0.5f || b.y < 1.2f))
            {
                // A hard-driven first ball can beat a defender outright or come off the arms wild.
                if (touch == 1 && !diveTried)
                {
                    float hard = Mathf.InverseLerp(8f, 16f, digSpeed);
                    float beatenChance = hard * Mathf.Lerp(0.55f, 0.15f, NpcProfile.N(ai.Live.defense));
                    diveTried = true; // one chance per ball
                    float roll = Random.value;
                    if (roll < beatenChance * 0.5f) { beaten = true; ai.Actor.Volley(HitType.Bump); return; } // too late: it goes by
                    if (roll < beatenChance) { Shank(t, ai); return; }
                }
                AiTouch(t, ai, touch, spike);
                return;
            }

            // Out of reach and almost down: dive for it.
            if (!diveTried && b.y < 0.9f && ball.Body.linearVelocity.y < 0f)
            {
                Vector3 land = ball.PredictLanding();
                float d = Flat(land - ai.Ground);
                if (d > reach && d < reach + 2f && ball.PredictTimeToHeight(0.25f, 1f, out float tl) && tl < 0.32f)
                {
                    diveTried = true;
                    ai.Lunge(land, tl);
                    if (Random.value < Mathf.Lerp(0.15f, 0.6f, NpcProfile.N(ai.Live.defense)))
                        StartCoroutine(DiveDig(t, ai, tl));
                }
            }
        }

        IEnumerator DiveDig(Team t, CourtAI ai, float delay)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delay - 0.05f));
            if (ball.IsDead || ballTeam != t) yield break;
            CountTouch(t, ai);
            var target = new Vector3(0f, VolleyBall.Radius, t.side * 2.5f);
            Vector2 j = Random.insideUnitCircle * 1.6f;
            target += new Vector3(j.x, 0f, j.y);
            ball.Launch(ball.VelocityToReach(ball.transform.position, Clamp(target, t.side), 3f));
        }

        /// <summary>A dig that comes off the arms the wrong way: wide, or straight back over the net.</summary>
        void Shank(Team t, CourtAI ai)
        {
            CountTouch(t, ai);
            if (fault) return;
            ai.Actor.Volley(HitType.Bump);
            Vector3 from = ball.transform.position;
            Vector3 target = Random.value < 0.5f
                ? new Vector3(Mathf.Sign(Random.value - 0.5f) * Random.Range(4.5f, 7f), VolleyBall.Radius, t.side * Random.Range(2f, 8f))
                : new Vector3(Random.Range(-2.5f, 2.5f), VolleyBall.Radius, -t.side * Random.Range(1f, 4f));
            ball.Launch(ball.VelocityToReach(from, target, Mathf.Max(from.y + 0.8f, 2.6f)));
        }

        bool CanSpike(Team t) => ball.PredictTimeToHeight(2.85f, 4f, out float u) && u > 0.25f
            && Mathf.Abs(ball.PredictPositionAt(u).z) < 3.2f;

        void AiTouch(Team t, CourtAI ai, int touch, bool spike)
        {
            CountTouch(t, ai);
            if (fault) return;
            var p = ai.Live;
            Vector3 from = ball.transform.position;
            float incoming = ball.Body.linearVelocity.magnitude;

            if (touch == 1)
            {
                // Pass to the setter spot near the net, in the partner's lane.
                float q = Mathf.Clamp01(0.35f + NpcProfile.N(p.passing) * 0.6f - Mathf.InverseLerp(9f, 16f, incoming) * 0.35f + Random.Range(-0.2f, 0.2f));
                Vector3 setter = SetterSpot(t, ai);
                Vector2 miss = Random.insideUnitCircle * Mathf.Lerp(2.2f, 0.35f, q);
                Vector3 target = Clamp(setter + new Vector3(miss.x, 0f, miss.y), t.side);
                ai.Actor.Volley(HitType.Bump);
                ball.Launch(ball.VelocityToReach(from, target, Mathf.Lerp(3.2f, 4.4f, q)));
            }
            else if (touch == 2)
            {
                // Set the hitter: near the net in their lane, high.
                bool hitterHuman = t.human;
                float x = hitterHuman ? Mathf.Clamp(player.position.x, -2.6f, 2.6f) : (ai == t.ais[0] ? 1.6f : -1.6f);
                var target = new Vector3(x, VolleyBall.Radius, t.side * 1.1f);
                Vector2 miss = Random.insideUnitCircle * Mathf.Lerp(1.4f, 0.25f, NpcProfile.N(p.setting));
                target = Clamp(target + new Vector3(miss.x, 0f, miss.y * 0.6f), t.side);
                ai.Actor.Volley(HitType.Set);
                ball.Launch(ball.VelocityToReach(from, target, 4.7f));
                if (hitterHuman) { Hint = "Partner set it: run in, jump (Space) and spike (LMB)"; UpdateBoard(); StartCoroutine(ClearHint(2.5f)); }
            }
            else Attack(t, ai, spike);
        }

        IEnumerator ClearHint(float after)
        {
            yield return new WaitForSeconds(after);
            Hint = null;
            UpdateBoard();
        }

        Vector3 SetterSpot(Team t, CourtAI passer)
        {
            if (t.human) return new Vector3(Mathf.Clamp(player.position.x * 0.6f, -2f, 2f), 0f, t.side * 1.9f);
            var partner = passer == t.ais[0] ? t.ais[1] : t.ais[0];
            return new Vector3(Mathf.Clamp(partner.Ground.x * 0.5f, -1.6f, 1.6f), 0f, t.side * 1.9f);
        }

        void Attack(Team t, CourtAI ai, bool spike)
        {
            var p = ai.Live;
            Team other = t == near ? far : near;
            Vector3 from = ball.transform.position;
            ai.Actor.Volley(spike ? HitType.Spike : HitType.Roll);

            // Staged: straight into the net.
            if (netNextAttack == ai)
            {
                netNextAttack = null;
                ball.Launch(new Vector3(Random.Range(-0.5f, 0.5f), -1.5f, -t.side * 6f));
                return;
            }

            // The other team's block.
            if (spike && blocker != null && Array.IndexOf(other.ais, blocker) >= 0
                && Flat(blocker.Ground - new Vector3(from.x, 0f, 0f)) < 1.2f && Mathf.Abs(blocker.Ground.z) < 1.4f
                && Random.value < Mathf.Lerp(0.12f, 0.4f, NpcProfile.N(blocker.Live.blocking)))
            {
                blocker.Jump(BlockJump(blocker.Live), 0.7f);
                blocker.Actor.Volley(HitType.Block);
                CountBlock(other, blocker);
                var down = new Vector3(from.x + Random.Range(-0.8f, 0.8f), VolleyBall.Radius, t.side * Random.Range(0.8f, 2f));
                ball.Launch(ball.VelocityToReach(from + new Vector3(0f, 0f, -t.side * 0.4f), down, from.y + 0.1f));
                return;
            }
            if (blocker != null) { blocker.Jump(BlockJump(blocker.Live), 0.7f); blocker.Actor.Volley(HitType.Block); }

            Vector3 target;
            bool staged = (forcedAttack.HasValue || stageGap) && t == far;
            if (staged && stageGap)
            {
                // Right between the protagonist and his partner, who'll leave it.
                Vector3 a = new Vector3(player.position.x, 0f, player.position.z), b = near.ais[0].Ground;
                target = Clamp((a + b) * 0.5f, near.side);
                hesitating = near.ais[0];
                stageGap = false;
            }
            else if (staged) { target = forcedAttack.Value; forcedAttack = null; }
            // Good attackers roll it deep into the corners instead of just over the net.
            else target = OpenSpot(other, spike ? 3.2f : Mathf.Lerp(2f, 5.2f, NpcProfile.N(p.attacking)));

            // Errors: net or long, more often for weaker attackers.
            float faultChance = staged ? 0f : Mathf.Lerp(spike ? 0.24f : 0.12f, spike ? 0.06f : 0.03f, NpcProfile.N(p.attacking));
            if (Random.value < faultChance)
            {
                if (Random.value < 0.5f) { ball.Launch(new Vector3(Random.Range(-0.6f, 0.6f), -1f, -t.side * 7f)); return; }
                target = new Vector3(target.x * 1.4f + Random.Range(-1f, 1f), VolleyBall.Radius, other.side * (CourtDimensions.SideLength + Random.Range(0.6f, 1.6f)));
            }
            else if (!staged)
            {
                Vector2 j = Random.insideUnitCircle * Mathf.Lerp(1.5f, 0.35f, NpcProfile.N(p.attacking));
                target = new Vector3(target.x + j.x, VolleyBall.Radius, target.z + j.y);
            }

            if (spike)
            {
                float pace = Mathf.Lerp(8.5f, 16.5f, NpcProfile.N(p.attacking));
                ball.Launch(Carry(from, ball.DriveOverNet(from, target, CourtDimensions.NetHeight + VolleyBall.Radius + 0.2f, pace, 6f), target));
            }
            else
            {
                target.y = Mathf.Max(target.y, VolleyBall.Radius);
                ball.Launch(Carry(from, ball.ArcOverNet(from, target, Mathf.Max(from.y + 1f, CourtDimensions.NetHeight + 1.3f),
                    CourtDimensions.NetHeight + VolleyBall.Radius + 0.3f), target));
            }
        }

        /// <summary>
        /// The emptiest spot in <paramref name="t"/>'s court at least <paramref name="minDepth"/> off the
        /// net: furthest from both defenders, with a little randomness so it isn't predictable.
        /// </summary>
        Vector3 OpenSpot(Team t, float minDepth)
        {
            var defenders = new List<Vector3>();
            foreach (var a in t.ais) defenders.Add(a.Ground);
            if (t.human) defenders.Add(new Vector3(player.position.x, 0f, player.position.z));
            Vector3 best = new Vector3(0f, VolleyBall.Radius, t.side * 5f);
            float bestScore = -1f;
            for (float x = -3.2f; x <= 3.21f; x += 0.8f)
            for (float z = minDepth; z <= 7.2f; z += 0.8f)
            {
                var c = new Vector3(x, 0f, t.side * z);
                float score = float.MaxValue;
                foreach (var d in defenders) score = Mathf.Min(score, Flat(c - d));
                score += Random.Range(0f, 0.8f);
                if (score > bestScore) { bestScore = score; best = c; }
            }
            best.y = VolleyBall.Radius;
            return best;
        }

        void AiServe(CourtAI srv, Team from, Team to)
        {
            var p = srv.Live;
            Vector3 origin = srv.Ground + new Vector3(0f, 2.6f, -from.side * 0.4f);
            srv.Actor.Volley(HitType.Spike);
            Vector3 target;
            if (to.human && Random.value < p.serveAtWeakest)
                target = new Vector3(player.position.x + Random.Range(-1f, 1f), VolleyBall.Radius, player.position.z + Random.Range(-1f, 1f));
            else
                target = new Vector3(Random.Range(-3f, 3f), VolleyBall.Radius, to.side * Random.Range(3f, 7f));
            target = Clamp(target, to.side);
            if (Random.value < Mathf.Lerp(0.14f, 0.03f, NpcProfile.N(p.serving)))
                target.z = to.side * (CourtDimensions.SideLength + Random.Range(0.5f, 1.5f));
            ball.Place(origin);
            bool jumpServe = p.serving >= 7 && Random.value < 0.5f;
            Vector3 v = jumpServe
                ? ball.DriveOverNet(origin, target, CourtDimensions.NetHeight + VolleyBall.Radius + 0.35f, 12f, 8f)
                : ball.ArcOverNet(origin, target, 4.3f, CourtDimensions.NetHeight + VolleyBall.Radius + 0.3f);
            v = Carry(origin, v, target);
            if (debugLog) Debug.Log($"[StoryMatch] {srv.DisplayName} serves from {origin:F1} to {target:F1}, sim lands {SimLanding(origin, v):F1}{(jumpServe ? " (jump)" : "")}");
            ball.Launch(v);
        }

        /// <summary>
        /// The launch solvers ignore air drag, so long balls fall short. Stretch the horizontal speed
        /// until the simulated flight (same model as the ball) lands at <paramref name="target"/>.
        /// </summary>
        Vector3 Carry(Vector3 origin, Vector3 v, Vector3 target)
        {
            float want = Flat(target - origin);
            if (want < 1f) return v;
            for (int i = 0; i < 3; i++)
            {
                float got = Flat(SimLanding(origin, v) - origin);
                if (got < 0.1f) break;
                float k = Mathf.Clamp(want / got, 0.9f, 1.15f);
                if (Mathf.Abs(k - 1f) < 0.01f) break;
                v = new Vector3(v.x * k, v.y, v.z * k);
            }
            return v;
        }

        Vector3 SimLanding(Vector3 p, Vector3 v)
        {
            const float step = 0.01f;
            float g = ball.GravityMagnitude, drag = ball.AirDrag;
            for (float t = 0f; t < 6f && (p.y > VolleyBall.Radius || v.y > 0f); t += step)
            {
                p += v * step;
                v.y -= g * step;
                v *= 1f / (1f + drag * step);
            }
            return p;
        }

        // ---------- Touch bookkeeping ----------

        void CountTouch(Team t, CourtAI ai)
        {
            t.touches++;
            rallyTouches++;
            lastTouchTeam = t;
            lastAi = ai;
            humanTouchedLast = false;
            aiActor = ai;
            if (hesitating == ai) hesitating = null;
            if (t.touches > 3) { fault = true; faultReason = PointReason.Fault; return; }
            PassBaton(t, ai, false);
        }

        void CountBlock(Team t, CourtAI ai)
        {
            // A block touch: the ball goes straight back, the blocking team's count restarts.
            lastTouchTeam = t;
            lastAi = ai;
            humanTouchedLast = false;
            aiActor = ai;
            rallyTouches++;
        }

        void OnHumanPlayed(HitType type, float height)
        {
            if (near == null || Finished) return;
            near.touches++;
            rallyTouches++;
            lastTouchTeam = near;
            lastAi = null;
            humanTouchedLast = true;
            aiActor = null;
            if (hesitating != null) hesitating = null;
            Stats.humanTouches++;
            if (near.touches == 1 && (type == HitType.Dive || speedBeforeTouch > 9f)) Stats.humanSaves++;
            if (type == HitType.Block) near.touches = 0;
            if (near.touches > 3) { fault = true; faultReason = PointReason.Fault; return; }
            ballTeam = near;
            PassBaton(near, null, true);
            // A spike at the net meets the other team's block (a roll shot goes over it).
            if (type == HitType.Spike && blocker != null && Array.IndexOf(far.ais, blocker) >= 0)
                StartCoroutine(BlockHuman(blocker));
        }

        /// <summary>
        /// The far blocker goes up against the protagonist's spike. If he's in line and high enough
        /// when the ball reaches the net, it's stuffed back down on the protagonist's side.
        /// </summary>
        IEnumerator BlockHuman(CourtAI b)
        {
            b.Jump(BlockJump(b.Live), 0.7f);
            b.Actor.Volley(HitType.Block);
            float t0 = Time.time;
            while (ball.transform.position.z < -0.15f && Time.time - t0 < 0.6f && !ball.IsDead) yield return null;
            if (ball.IsDead || fault || landed) yield break;
            Vector3 at = ball.transform.position;
            float wall = CourtDimensions.NetHeight + 0.35f + BlockJump(b.Live) * 0.8f;
            bool lined = Mathf.Abs(at.x - b.Ground.x) < 0.9f && Mathf.Abs(b.Ground.z) < 1.5f && at.y < wall;
            // The protagonist's Spike skill (power and angle) beats the block more often.
            float spikeSkill = skills != null ? skills.Normalized(SkillType.Spike) : 0f;
            float chance = Mathf.Lerp(0.25f, 0.6f, NpcProfile.N(b.Live.blocking)) * Mathf.Lerp(1f, 0.55f, spikeSkill);
            if (!lined || Random.value > chance) yield break;
            CountBlock(far, b);
            near.touches = 0;
            ballTeam = near;
            lastSide = -1f;
            var down = new Vector3(at.x + Random.Range(-1f, 1f), VolleyBall.Radius, -Random.Range(0.8f, 2.6f));
            ball.Launch(ball.VelocityToReach(at, Clamp(down, -1f), at.y + 0.1f));
            ui.Bark(b.DisplayName, BarkPool.Once("Stuffed!", "Not today.", "Block!") ?? "Block!", 1.4f);
            AssignReceiver(near);
        }

        void OnHumanGraded(HitType type, float q)
        {
            if (near == null || Finished) return;
            if (q >= 0.5f) Stats.humanGoodTouches++;
        }

        void OnHumanPancake(float q) { if (near != null && !Finished) Stats.humanPancakes++; }

        void OnHumanServed(float side) => humanServed = true;

        void OnLanded(Vector3 p)
        {
            if (near == null || Finished) return;
            landed = true;
            landedAt = p;
            if (debugLog) Debug.Log($"[StoryMatch] landed at {p:F1} after {rallyTouches} touches, last {(humanTouchedLast ? "you" : lastAi != null ? lastAi.DisplayName : "-")}");
        }

        // ---------- Scoring ----------

        MatchPoint Resolve(int index)
        {
            var mp = new MatchPoint { index = index, landedAt = landedAt, rallyTouches = rallyTouches, humanLastTouch = humanTouchedLast, lastAi = lastAi };
            Team loser;
            if (fault)
            {
                if (faultReason == PointReason.Error || lastTouchTeam == null)
                {
                    mp.reason = PointReason.Error;
                    mp.nearScore = near.score;
                    mp.farScore = far.score;
                    hesitating = null;
                    return mp; // replayed
                }
                loser = lastTouchTeam;
                mp.reason = PointReason.Fault;
            }
            else
            {
                Vector3 p = landedAt;
                bool inCourt = Mathf.Abs(p.x) <= CourtDimensions.HalfWidth + 0.05f && Mathf.Abs(p.z) <= CourtDimensions.SideLength + 0.05f;
                Team sideTeam = p.z < 0f ? near : far;
                if (inCourt)
                {
                    loser = sideTeam;
                    bool netBall = lastTouchTeam == sideTeam;
                    mp.reason = netBall ? (Mathf.Abs(p.z) < 1.5f ? PointReason.Net : PointReason.Fault)
                        : rallyTouches == 0 ? PointReason.Ace : PointReason.Kill;
                    if (loser == near && !netBall && near.touches == 0)
                    {
                        // Dropped between the two of them with nobody moving: the communication point.
                        float dh = Flat(player.position - p), dp = Flat(near.ais[0].Ground - p);
                        if (dh < 3f && dp < 3.5f) { mp.reason = PointReason.Gap; Stats.gapBallsLost++; }
                    }
                }
                else
                {
                    loser = lastTouchTeam ?? sideTeam;
                    mp.reason = PointReason.Out;
                }
            }
            Team winner = loser == near ? far : near;
            winner.score++;
            if (serving != winner) serving = winner;
            mp.nearWon = winner == near;
            mp.nearScore = near.score;
            mp.farScore = far.score;
            if (loser == near && humanTouchedLast && mp.reason != PointReason.Kill) Stats.humanErrors++;
            if (winner == near && humanTouchedLast && (mp.reason == PointReason.Kill || mp.reason == PointReason.Ace)) Stats.humanKills++;
            Stats.longestRally = Mathf.Max(Stats.longestRally, rallyTouches);
            hesitating = null;
            return mp;
        }

        // ---------- Helpers ----------

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        /// <summary>Keeps a target inside the court on side <paramref name="side"/>.</summary>
        static Vector3 Clamp(Vector3 p, float side)
        {
            float hx = CourtDimensions.HalfWidth - 0.4f;
            p.x = Mathf.Clamp(p.x, -hx, hx);
            p.z = side * Mathf.Clamp(Mathf.Abs(p.z), 0.7f, CourtDimensions.SideLength - 0.4f);
            p.y = Mathf.Max(p.y, VolleyBall.Radius);
            return p;
        }
    }
}
