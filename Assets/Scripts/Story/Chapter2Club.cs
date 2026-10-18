using System.Collections;
using System.Collections.Generic;
using BeachVolley.Match;
using BeachVolley.Player;
using BeachVolley.Skills;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Chapter 2 at Coastline Beach Club, "The Sand Never Forgets": five training days, each with
    /// its own light, opening, drills, people and a choice, ending in two real 2v2 games (with
    /// Marlon, then with Dave). Each day ends with a training card and goes home (Chapter2Home),
    /// where the points are spent.
    /// </summary>
    public class Chapter2Club : StoryDirector
    {
        [SerializeField] StoryActor steve, dave, marlon, alex, nina;
        [SerializeField] ScriptedPlay clubRally;
        [SerializeField] StoryDrill drill;
        [SerializeField] StoryMatch match;
        [SerializeField] BallContact contact;
        [SerializeField] GameObject car, bike;
        [Tooltip("Free-roam spots: the water cooler, the club board, and 'back to the court'.")]
        [SerializeField] Interactable cooler, board, court;

        protected override int ChapterNumber => 2;

        // The training court is at the origin, the player's side is −z.
        static readonly Vector3 EntranceSpot = new Vector3(-7f, 0f, -14f);
        static readonly Vector3 BenchSpot = new Vector3(-5.95f, 0f, -3.74f);

        static readonly Vector3 SteveSideline = new Vector3(4.8f, 0f, -4.2f);
        static readonly Vector3 SteveCoach = new Vector3(3f, 0f, -2.2f);
        static readonly Vector3 TosserLeft = new Vector3(-2.2f, 0f, -1f);
        static readonly Vector3 TosserRight = new Vector3(2.2f, 0f, -1f);
        static readonly Vector3 DaveNet = new Vector3(0.5f, 0f, -1.3f);
        static readonly Vector3 Sideline = new Vector3(5.2f, 0f, -2.6f);
        static readonly Vector3 Sideline2 = new Vector3(5.6f, 0f, -5.4f);
        static readonly Vector3 Huddle = new Vector3(0.8f, 0f, -6.5f);

        int day;
        readonly List<(string label, DrillResult result)> log = new List<(string, DrillResult)>();
        readonly List<TrainingLine> extraLines = new List<TrainingLine>();
        bool energyShown;

        protected override IEnumerator Run()
        {
            ui.SetBlack(true);
            contact.ShowControlsHint = false;
            contact.enabled = false;
            drill.RepEnded += OnRep;
            day = Mathf.Clamp(DayFrom(StoryState.CheckpointName), 1, 5);
            if (car != null) car.SetActive(day == 1 && !StoryState.Is("transport", "bike"));
            if (bike != null) bike.SetActive(day == 1 && StoryState.Is("transport", "bike"));
            if (alex != null) alex.gameObject.SetActive(day >= 3);
            if (nina != null) nina.gameObject.SetActive(true);
            Light(day);
            skills.BeginSession();

            switch (day)
            {
                case 1: yield return Day1(); break;
                case 2: yield return Day2(); break;
                case 3: yield return Day3(); break;
                case 4: yield return Day4(); break;
                default: yield return Day5(); break;
            }

            ui.SetEnergy(null);
            yield return Card();
            yield return GoToScene(HomeScene, "home2_d" + day);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (drill != null) drill.RepEnded -= OnRep;
        }

        static int DayFrom(string checkpoint) =>
            checkpoint != null && checkpoint.StartsWith("club2_d") && int.TryParse(checkpoint.Substring(7), out int d) ? d : 1;

        // ---------- Light: every day looks different ----------

        void Light(int d)
        {
            var sun = RenderSettings.sun;
            if (sun == null)
                foreach (var l in FindObjectsByType<Light>())
                    if (l.type == LightType.Directional) { sun = l; break; }
            (Vector3 rot, Color color, float intensity, float exposure, Color fog, float fogStart) p = d switch
            {
                1 => (new Vector3(30f, 230f, 0f), new Color(1f, 0.88f, 0.7f), 1.35f, 1.3f, new Color(0.85f, 0.82f, 0.78f), 90f),
                2 => (new Vector3(36f, 105f, 0f), new Color(1f, 0.97f, 0.92f), 1.5f, 1.35f, new Color(0.78f, 0.87f, 0.95f), 100f),
                3 => (new Vector3(74f, 180f, 0f), new Color(1f, 0.98f, 0.9f), 1.45f, 1.4f, new Color(0.9f, 0.88f, 0.82f), 25f),
                4 => (new Vector3(20f, 250f, 0f), new Color(1f, 0.74f, 0.55f), 1.2f, 1.25f, new Color(0.72f, 0.66f, 0.66f), 70f),
                _ => (new Vector3(13f, 262f, 0f), new Color(1f, 0.66f, 0.42f), 1.2f, 1.25f, new Color(0.95f, 0.7f, 0.55f), 60f),
            };
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(p.rot);
                sun.color = p.color;
                sun.intensity = p.intensity;
            }
            // A copy of the sky, so play never edits the asset. The procedural sky follows the sun.
            if (RenderSettings.skybox != null)
            {
                var sky = new Material(RenderSettings.skybox);
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", p.exposure);
                RenderSettings.skybox = sky;
            }
            RenderSettings.fogColor = p.fog;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = d == 3 ? 160f : 420f;
            DynamicGI.UpdateEnvironment();
        }

        // ---------- Drills ----------

        IEnumerator Drill(DrillKind kind, int reps, string label, System.Func<IEnumerator> onStruggle = null)
        {
            if (day <= 2) skills.ResetFatigue();
            playerActor.EndActing();
            cutscene = false;
            contact.enabled = true;
            contact.ShowControlsHint = true;
            cam.Follow();
            DrillResult res = default;
            yield return drill.Run(kind, reps, r => res = r, onStruggle);
            lastResult = res;
            if (label != null) log.Add((label, res));
            contact.enabled = false;
            contact.ShowControlsHint = false;
            cutscene = true;
            playerActor.BeginActing();
        }

        DrillResult lastResult;

        /// <summary>Spot for the drill, facing the net, with the ball prepared.</summary>
        void Ready(DrillKind kind)
        {
            drill.FeederA = drill.FeederB = null;
            PlaceAt(drill.SpotFor(kind), Quaternion.identity);
            drill.Prepare(kind);
        }

        static string Verdict(DrillResult r, string great, string ok, string rough) => Grade(r.good, r.reps, great, ok, rough);

        void ShowEnergy() { energyShown = true; ui.SetEnergy(skills.Fatigue); }

        // ---------- Live barks during drills (never block play) ----------

        void Bark(StoryActor who, string line, float duration = 2.8f)
        {
            if (line == null || who == null) return;
            ui.Bark(who.displayName, line, duration);
            who.Talk(true);
            StartCoroutine(StopTalking(who, Mathf.Min(duration, 1.6f)));
        }

        IEnumerator StopTalking(StoryActor who, float after)
        {
            yield return new WaitForSeconds(after);
            who.Talk(false);
        }

        /// <summary>A short back-and-forth called out while the drill keeps running.</summary>
        IEnumerator Banter(params (StoryActor who, string line)[] lines)
        {
            foreach (var (who, line) in lines)
            {
                if (who == null) ui.Bark("You", line, 2.6f);
                else Bark(who, line, 2.6f);
                yield return new WaitForSeconds(2.4f);
            }
        }

        bool coachedPassLR, pancakeJoke, pancakeCheer, marlonPassed;

        void OnRep(DrillRep r)
        {
            if (energyShown) ui.SetEnergy(skills.Fatigue);
            switch (r.kind)
            {
                case DrillKind.PassLR:
                    if (!r.ok && !coachedPassLR && r.index >= 1)
                    {
                        coachedPassLR = true;
                        StartCoroutine(Banter(
                            (dave, "You're reacting to the ball instead of reading it."),
                            (null, "Are you coaching me now?"),
                            (dave, "Someone has to.")));
                    }
                    else if (r.ok && r.streak == 3) Bark(steve, BarkPool.Once("There. You moved before the toss, not after.", "Three in a row. Your feet are listening."));
                    else if (r.ok && r.successes == 1) Bark(nina, BarkPool.Once("Nice platform!", "Ooh, clean one."));
                    else if (!r.ok && r.miss == DrillMiss.NoTouch) Bark(steve, BarkPool.Once("Move early. Don't wait for the ball to reach you.", "First step happens when the ball leaves the hand."));
                    break;

                case DrillKind.PassLow:
                    if (r.ok && r.successes == 1) Bark(steve, BarkPool.Once("Low and quiet. That's it.", "See? You don't have to dive at everything."));
                    else if (!r.ok && r.miss == DrillMiss.Early) Bark(steve, BarkPool.Once("You're swinging at it. Let it come to the platform.", "Too early. Patience."));
                    else if (!r.ok && r.miss == DrillMiss.Late) Bark(steve, BarkPool.Once("Late — get the platform out before it arrives.", "Arms ready sooner."));
                    else if (!r.ok) Bark(dave, BarkPool.Once("Those low ones are evil. He does it on purpose.", "Bend the knees, not the back!"));
                    break;

                case DrillKind.PassPressure:
                    if (r.streak == drill.ScoreToBeat + 1 && drill.ScoreToBeat > 0) Bark(dave, BarkPool.Once("Okay, that's my record. I'm not crying.", "New club record. Don't tell anyone it was a drill."), 3.2f);
                    else if (r.streak == 3) Bark(dave, BarkPool.Once("Three! Keep it going!", "He's on fire. Small fire. Candle-sized."));
                    else if (!r.ok && r.index > 3) Bark(steve, BarkPool.Once("Reset. Next ball is a new ball.", "Don't carry the last one into this one."));
                    break;

                case DrillKind.SetControl:
                    if (r.ok && r.successes == 2) Bark(steve, BarkPool.Once("Hands shaped before the ball arrives. Good.", "Soft wrists. That one floated."));
                    else if (!r.ok && r.miss == DrillMiss.Early) Bark(steve, BarkPool.Once("Wait for it above your forehead.", "You're reaching up for it — let it drop in."));
                    else if (!r.ok) Bark(dave, BarkPool.Once("Welcome to my world.", "The ball has opinions, right?"));
                    break;

                case DrillKind.ThreeTouch:
                    if (r.ok && r.successes == 1) Bark(dave, BarkPool.Once("THREE TOUCHES! Did you see that?", "That's a real point! Kind of!"));
                    else if (!r.ok && r.miss == DrillMiss.TooFar) Bark(dave, BarkPool.Once("I can't set what I can't reach!", "A bit closer next time, I'm not a kangaroo."));
                    else if (!r.ok && r.miss == DrillMiss.Net) Bark(steve, BarkPool.Once("Wait for the set to start dropping, then go.", "You're early. Watch his hands."));
                    break;

                case DrillKind.SpikeTiming:
                    if (r.index == 1 && !marlonPassed && marlon != null)
                    {
                        marlonPassed = true;
                        StartCoroutine(MarlonWalksPast());
                    }
                    else if (r.ok && r.streak == 2) Bark(steve, BarkPool.Once("Contact first. You're getting it.", "That's a clean hand on the ball."));
                    else if (!r.ok) Bark(dave, BarkPool.Once("Again! I've got all day.", "One more. I'll set it prettier."));
                    break;

                case DrillKind.ServeTarget:
                    if (r.ok && r.successes == 1) Bark(alex, BarkPool.Once("Good toss. Same toss every time — that's the whole secret.", "In. Now do it again without thinking about it."));
                    else if (!r.ok && r.miss == DrillMiss.Net) Bark(alex, BarkPool.Once("Hit through the ball, not down at it.", "Toss a little more in front."));
                    else if (!r.ok && r.miss == DrillMiss.Out) Bark(nina, BarkPool.Once("That one's in the parking lot.", "Long! Easy on the power."));
                    break;

                case DrillKind.ServeReceive:
                    if (r.ok && r.successes == 1) Bark(alex, BarkPool.Once("Nice read.", "You saw that one early."));
                    else if (!r.ok) Bark(steve, BarkPool.Once("Watch his shoulder, not the ball. The shoulder tells you where.", "Feet first, then arms."));
                    break;

                case DrillKind.Pepper:
                    if (r.ok) Bark(dave, BarkPool.Once("Look at us! We're a rally!", "Keep it going, keep it going…"));
                    else Bark(dave, BarkPool.Once("My fault. Mostly. Partially.", "Again! Roll it, don't smash it."));
                    break;

                case DrillKind.Pancake:
                    if (!r.ok && !pancakeJoke && r.index <= 2)
                    {
                        pancakeJoke = true;
                        StartCoroutine(Banter(
                            (dave, "That looked painful."),
                            (null, "I was testing the sand."),
                            (dave, "And?"),
                            (null, "It's still sand.")));
                    }
                    else if (r.pancake && !pancakeCheer)
                    {
                        pancakeCheer = true;
                        SetFlag("ch2_pancake", "yes");
                        StartCoroutine(Banter((dave, "You actually got it!"), (null, "I think I surprised myself.")));
                    }
                    else if (!r.ok && r.miss == DrillMiss.Early) Bark(steve, BarkPool.Once("Too early — the hand's on the sand before the ball is.", "Later. Wait for the last moment."));
                    else if (!r.ok && r.miss == DrillMiss.Late) Bark(steve, BarkPool.Once("Late. Start the dive a beat sooner.", "Commit earlier."));
                    break;

                case DrillKind.BlockSignal:
                case DrillKind.BlockDig:
                    if (r.ok && r.streak == 2) Bark(dave, BarkPool.Once("We're a wall! A small wall. A fence.", "That's the system working!"));
                    else if (!r.ok && r.miss == DrillMiss.WrongZone) Bark(steve, BarkPool.Once("Opposite of his signal. He takes one shot, you take the other.", "Check the fingers, then move."));
                    else if (r.ok && r.successes == 1) Bark(alex, BarkPool.Once("Okay, you read that.", "Good spot."));
                    break;

                case DrillKind.BlockOrDefend:
                    if (!r.ok && r.miss == DrillMiss.StayedOnNet) Bark(steve, BarkPool.Once("That set was miles off the net. Nobody's spiking that — get off the net!", "You blocked a ghost."));
                    else if (!r.ok && r.miss == DrillMiss.NoTouch) Bark(alex, BarkPool.Once("Hands up earlier — I'm not going to wait for you.", "Too late on the block."));
                    else if (r.ok) Bark(steve, BarkPool.Once("Right read.", "Exactly — defend what they can actually do."));
                    break;
            }
        }

        IEnumerator MarlonWalksPast()
        {
            Vector3 from = new Vector3(-7f, 0f, -8.5f), to = new Vector3(8f, 0f, -9f);
            clubRally.StopLoop();
            marlon.transform.position = from;
            marlon.WalkTo(to);
            yield return new WaitForSeconds(2.2f);
            Bark(marlon, "Jump later. Or don't jump at all, honestly.", 3f);
            yield return new WaitForSeconds(3f);
            Bark(dave, "Ignore him. Your timing's getting better.", 2.8f);
            while (marlon.IsWalking) yield return null;
            // Back to his own court.
            clubRally.ResetPositions();
            clubRally.StartLoop();
        }

        // ---------- Breaks: free roam with optional talks ----------

        IEnumerator Break(string objective, params Interactable[] items)
        {
            var list = new List<Interactable>(items);
            if (court != null) { court.Available = true; list.Add(court); }
            contact.enabled = false;
            cam.Follow();
            playerActor.EndActing();
            bool done = false;
            yield return Explore(list, it => UseBreak(it, () => done = true), () => done, objective);
            playerActor.BeginActing();
        }

        System.Func<Interactable, System.Action, IEnumerator> breakTalk;

        IEnumerator UseBreak(Interactable it, System.Action finish)
        {
            if (it == court) { finish(); yield break; }
            if (it == cooler)
            {
                playerActor.Gesture("Drink");
                skills.Rest(0.3f);
                if (energyShown) ui.SetEnergy(skills.Fatigue);
                yield return Narrate(BarkPool.Next(
                    "Cold water. He drinks half the cup and pours the rest over his neck.",
                    "The cooler is nearly empty. He gets the last of it. Someone groans behind him.",
                    "Water. It tastes like the best thing he's ever had."));
                it.Available = false;
                yield break;
            }
            if (breakTalk != null) yield return breakTalk(it, finish);
        }

        // ---------- Day 1: Control the Ball ----------

        IEnumerator Day1()
        {
            // Arrival: he walks in from the parking area, Dave is warming up.
            PlaceAt(EntranceSpot, Facing(EntranceSpot, Vector3.zero));
            Put(steve, new Vector3(2.5f, 0f, -6f), Vector3.zero);
            Put(dave, new Vector3(-2.4f, 0f, -9.3f), EntranceSpot);
            Put(nina, new Vector3(1.2f, 0f, -3f), Vector3.zero);
            dave.Gesture("Stretch");
            cam.Frame(EntranceSpot + new Vector3(-1.4f, 2.1f, -1.8f), new Vector3(-2f, 1f, -8f), 50f);
            yield return FadeIn(1.4f);
            yield return Narrate("Coastline Beach Club, two days later. Same courts, same thud of the ball. This time he knows where to go.");

            yield return WalkTo(dave, new Vector3(-5.6f, 0f, -12.4f));
            dave.LookAt(player);
            motor.FaceTowards(dave.transform.position, 0.3f);
            TwoShot(player, dave.transform);
            yield return Say("Dave", "Look who decided to come back.");
            yield return Say("You", "You sound surprised.");
            yield return Say("Dave", "Last time you looked like you were reconsidering every decision that led you here.");
            yield return Say("You", "I was. Then I remembered I hit one ball over the net.");
            dave.Gesture("Laugh");
            yield return Say("Dave", "Ah, yes. The legendary spike. History will remember it.");
            playerActor.Gesture("Laugh");
            yield return Narrate("They both laugh. His forearms still remember it too — two faint red marks.");

            // Steve and the development screen.
            yield return WalkTo(steve, new Vector3(-6.1f, 0f, -11.2f));
            steve.LookAt(player);
            TwoShot(player, steve.transform, -1f, 3.8f);
            steve.Gesture("Wave");
            yield return Say("Steve", "Good. You're both here. Last time was about getting the ball over the net. Today is about the first touch — controlling where the rally goes.");
            yield return Say("Steve", "And starting today, you earn development points. Every session, you'll earn some. At home, you decide where they go.");
            yield return Say("You", "So I can focus on whatever I want?");
            yield return Say("Steve", "Within reason. If you want to be a great attacker, you still need to get to the ball, control it and understand what's happening around you.");
            steve.Gesture("Point");
            yield return Say("Steve", "Here — this is you right now.");
            yield return ui.Development(skills, "Steve: \"Don't worry about a perfect build. Just pay attention to what you struggle with.\"", false);
            yield return Say("Steve", "Eleven skills. Today we only care about two: Bump and Court Awareness. Your feet and your eyes.");
            yield return Say("Dave", "He made me do this for a month. My Court Awareness went up and I still walked into the net post.");
            yield return Say("Steve", "That was a different kind of awareness, Dave.");
            yield return FadeOut(0.7f);

            // Drill 1: left and right.
            Ready(DrillKind.PassLR);
            Put(dave, TosserLeft, drill.SpotFor(DrillKind.PassLR));
            Put(nina, TosserRight, drill.SpotFor(DrillKind.PassLR));
            Put(steve, SteveSideline, Vector3.zero);
            drill.FeederA = dave.transform;
            drill.FeederB = nina.transform;
            TwoShot(player, steve.transform, -1f, 5f);
            yield return FadeIn(0.6f);
            yield return Say("Steve", "This is Nina — she's been here a year. She and Dave toss, left and right. You move, you get under it, you pass. Don't worry about where it goes — worry about getting there.");
            yield return Say("Nina", "Hi! I'll go easy. For the first three.");
            yield return Drill(DrillKind.PassLR, 8, "Left and right passing");
            steve.Gesture(lastResult.Ratio >= 0.5f ? "Clap" : "Nod");
            TwoShot(player, dave.transform, 1f, 4.4f);
            yield return Say("Steve", Verdict(lastResult,
                "Better than better. You were moving before Nina's hands opened.",
                "Better. Your technique still needs work, but your positioning improved.",
                "You're still standing in the middle hoping. That's normal. It goes away."));

            dave.LookAt(player);
            yield return Say("Dave", lastResult.Ratio >= 0.5f ? "See? Reading, not reacting. Told you." : "Reading, not reacting. It clicks. It took me two weeks.");
            yield return Choose("“Thanks, coach Dave.”", "“Lucky guesses. All of them.”");
            if (Choice == 0)
            {
                SetFlag("dave_tone", "warm");
                Relationship("dave", 1);
                dave.Gesture("Happy");
                yield return Say("Dave", "Coach Dave. I like that. Put it on a shirt.");
            }
            else
            {
                SetFlag("dave_tone", "teasing");
                dave.Gesture("SarcasticNod");
                yield return Say("Dave", "Eight lucky guesses in a row would be its own skill.");
            }

            // Drill 2: low balls from Steve.
            yield return FadeOut(0.5f);
            Ready(DrillKind.PassLow);
            Put(steve, new Vector3(0.6f, 0f, -1.4f), drill.SpotFor(DrillKind.PassLow));
            Put(dave, Sideline, Vector3.zero);
            Put(nina, Sideline2, Vector3.zero);
            OverShoulder(player, steve.transform);
            yield return FadeIn(0.5f);
            steve.Volley(HitType.Spike);
            yield return Say("Steve", "Now the ugly ones. Lower, faster, less time. You don't need to be faster than every ball. You need to stop wasting movement.");
            yield return Drill(DrillKind.PassLow, 6, "Low balls");
            yield return Say("Steve", Verdict(lastResult,
                "That's a defender's platform. Quiet arms, busy feet.",
                "Half of those you'd have shanked two days ago.",
                "Low balls are the hardest thing in this sport. Don't measure yourself on them yet."));

            // Drill 3: pressure, and Dave's record.
            yield return FadeOut(0.5f);
            Ready(DrillKind.PassPressure);
            Put(steve, new Vector3(0f, 0f, -1.3f), drill.SpotFor(DrillKind.PassPressure));
            Put(dave, TosserLeft, drill.SpotFor(DrillKind.PassPressure));
            Put(nina, TosserRight, drill.SpotFor(DrillKind.PassPressure));
            drill.FeederA = dave.transform;
            drill.FeederB = nina.transform;
            drill.ScoreToBeat = 6;
            TwoShot(player, dave.transform, 1f, 5f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Last one. All three of us, any order, any speed. Count your streak.");
            yield return Say("Dave", "For the record, my best is six. Unofficially seven, but Nina says one hit my face.");
            yield return Say("Nina", "It hit your face.");
            yield return Drill(DrillKind.PassPressure, 12, "Passing under pressure");
            drill.ScoreToBeat = 0;
            bool beatDave = lastResult.bestStreak > 6;
            if (beatDave) SetFlag("beat_dave_record", "yes");

            // After training.
            yield return FadeOut(0.8f);
            drill.HideMarker();
            SitPlayer(BenchSeat(-0.4f), BenchFacing);
            SitNpc(dave, BenchSeat(0.5f), BenchFacing);
            Put(steve, BenchSpot + new Vector3(1.2f, 0f, 2.2f), BenchSpot);
            cam.Frame(BenchSpot + new Vector3(2.4f, 1.3f, 2.6f), BenchSpot + new Vector3(0.5f, 0.9f, 0.3f), 44f);
            yield return FadeIn(0.8f);
            yield return Narrate("They sit at the edge of the court, sand to the ankles.");
            dave.Gesture("Sigh");
            yield return Say("Dave", "My legs already hate this place.");
            yield return Say("You", "You said that last time.");
            yield return Say("Dave", "And they proved me right. Twice now.");
            if (beatDave) yield return Say("Dave", $"Also, {lastResult.bestStreak} in a row. You beat my record on day two. I want you to know I'm being very mature about it.");
            steve.LookAt(player);
            yield return Say("Steve", "You read the ball earlier today. Your technique still needs work, but you stopped standing still.");
            yield return Say("Steve", "That's how you improve. One part at a time.");
            dave.Sit(false);
            yield return Wait(0.3f);
            yield return StandPlayer();
        }

        // ---------- Day 2: Build the Attack ----------

        IEnumerator Day2()
        {
            yield return ui.TitleCard("Day 2", "Build the Attack", 2f);
            // Cold open: Dave, alone, losing a fight with the ball.
            PlaceAt(new Vector3(-4.6f, 0f, -9f), Facing(new Vector3(-4.6f, 0f, -9f), new Vector3(0f, 0f, -4f)));
            Put(dave, new Vector3(0f, 0f, -4f), new Vector3(0f, 0f, 0f));
            Put(steve, SteveSideline, Vector3.zero);
            Put(nina, new Vector3(-5.6f, 0f, 2f), Vector3.zero);
            cam.Frame(new Vector3(-3f, 1.7f, -8.6f), dave.transform.position + Vector3.up * 1.6f, 42f);
            var ball = FindAnyObjectByType<BeachVolley.Ball.VolleyBall>();
            ball.Place(dave.transform.position + Vector3.up * 2f + dave.transform.forward * 0.3f);
            yield return FadeIn(0.9f);
            yield return Narrate("Morning. The sand is still cool. Dave is already on the court, setting to himself.");
            for (int i = 0; i < 2; i++)
            {
                dave.Volley(HitType.Set);
                Vector3 from = dave.transform.position + Vector3.up * 2.1f + dave.transform.forward * 0.3f;
                ball.Place(from);
                ball.Launch(ball.VelocityToReach(from, dave.transform.position + new Vector3(i == 0 ? 1.6f : -2.4f, 0.2f, i == 0 ? 0.8f : 1.5f), 4.6f));
                yield return Wait(1.9f);
                dave.Gesture(i == 0 ? "Shrug" : "HeadShake");
                yield return Wait(0.6f);
            }
            yield return WalkTo(dave, new Vector3(-3.4f, 0f, -7.4f));
            dave.LookAt(player);
            TwoShot(player, dave.transform);
            yield return Say("You", "Having trouble?");
            yield return Say("Dave", "No. I'm teaching the ball independence.");
            yield return Say("You", "Looks like it's moving out.");
            dave.Gesture("Laugh");
            yield return Say("Dave", "It's going through a phase.");

            steve.LookAt(player);
            yield return WalkTo(steve, new Vector3(-2.6f, 0f, -6.2f));
            TwoShot(player, steve.transform, -1f, 4f);
            steve.Gesture("Clap");
            yield return Say("Steve", "Today you turn a good first touch into an actual attack. Pass, set, hit. Three touches, one idea.");
            yield return Say("Steve", "And a set isn't just putting the ball in the air. A good set gives your attacker a real chance. A bad one gives them a problem.");
            yield return Say("Dave", "I give people a lot of problems.");
            yield return Say("Steve", "We're fixing that today too.");
            yield return FadeOut(0.6f);

            // Drill 1: setting control.
            Ready(DrillKind.SetControl);
            Put(steve, new Vector3(2.3f, 0f, -4.1f), drill.SpotFor(DrillKind.SetControl));
            Put(dave, Sideline, Vector3.zero);
            OverShoulder(steve.transform, player, -1f);
            yield return FadeIn(0.6f);
            yield return Say("Steve", "I toss, you set it straight back up. High ones and quick ones. Get under it early and let your legs do the lifting.");
            yield return Drill(DrillKind.SetControl, 6, "Setting control");
            yield return Say("Steve", Verdict(lastResult,
                "Those were hittable. Dave, take notes.",
                "Consistent height is coming. Keep your hands shaped before the ball arrives.",
                "Setting is the hardest touch to feel. That's not an excuse, it's a promise it gets better."));

            // Drill 2: the three-touch attack, with a bad start.
            yield return FadeOut(0.5f);
            Ready(DrillKind.ThreeTouch);
            Put(steve, new Vector3(0.4f, 0f, 3.6f), drill.SpotFor(DrillKind.ThreeTouch));
            Put(dave, new Vector3(0.3f, 0f, -1.9f), drill.SpotFor(DrillKind.ThreeTouch));
            TwoShot(player, dave.transform, 1f, 5f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Now together. I send a free ball over. You pass to Dave, Dave sets you, you attack. Talk to each other.");
            drill.StageTightSet = true;
            yield return Drill(DrillKind.ThreeTouch, 1, null);
            bool firstWorked = lastResult.good > 0;
            TwoShot(player, dave.transform, 1f, 3.6f);
            if (!firstWorked)
            {
                playerActor.Gesture("Point");
                yield return Say("You", "That was your fault.");
                dave.Gesture("Arguing");
                yield return Say("Dave", "You jumped before I even touched the ball!");
                yield return Choose("“I was building anticipation.”", "“Your set was glued to the net.”", "“Yeah… that one's on me.”");
            }
            else
            {
                yield return Say("Dave", "Wait — that worked? My set was basically inside the net.");
                yield return Choose("“I was building anticipation.”", "“Your set was glued to the net.”", "“Your set saved it.”");
            }
            switch (Choice)
            {
                case 0:
                    SetFlag("blame_style", "joke");
                    dave.Gesture("Laugh");
                    yield return Say("Dave", "Build it a little later next time.");
                    break;
                case 1:
                    SetFlag("blame_style", "blame");
                    dave.Gesture("HeadShake");
                    yield return Say("Dave", "Okay, it was close. It was a bit close. But you were already in the air.");
                    break;
                default:
                    SetFlag("blame_style", "owns_it");
                    Relationship("dave", 1);
                    dave.Gesture("Nod");
                    yield return Say("Dave", firstWorked ? "Ha. I'll take the credit, thanks." : "And the set was tight. So… both of us.");
                    break;
            }
            steve.LookAt(player);
            yield return Say("Steve", "Again. And this time say something. \"Up!\", \"Mine!\", \"Here!\" — anything. You have to know what your partner is doing.");
            yield return Drill(DrillKind.ThreeTouch, 5, "Three-touch attack");
            int threeGood = lastResult.good + (firstWorked ? 1 : 0);
            log[log.Count - 1] = ("Three-touch attack", new DrillResult { kind = DrillKind.ThreeTouch, reps = 6, good = threeGood, bestStreak = lastResult.bestStreak });
            yield return Say("Steve", Grade(threeGood, 6,
                "That's volleyball. Not three touches — one sequence.",
                "When it works, you can see it. Now make it work on purpose.",
                "The touches are there. The timing between them isn't yet. That's partners — it takes reps."));

            // Drill 3: spike timing (Marlon walks past).
            yield return FadeOut(0.5f);
            Ready(DrillKind.SpikeTiming);
            Put(dave, DaveNet, drill.SpotFor(DrillKind.SpikeTiming));
            Put(steve, SteveSideline, Vector3.zero);
            OverShoulder(player, dave.transform);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Last block: attacking. Dave mixes the sets — high, quicker, left, right. Watch the ball start to fall, then go.");
            yield return Drill(DrillKind.SpikeTiming, 6, "Spike timing");
            yield return Say("Steve", "Power comes later. Right now, I want you to make good contact. " + Verdict(lastResult,
                "And you did.", "Most of the time, you did.", "Some of the time, you did. That's where everyone starts."));

            // The walk out.
            yield return FadeOut(0.8f);
            drill.HideMarker();
            PlaceAt(new Vector3(-5.6f, 0f, -10.6f), Facing(new Vector3(-5.6f, 0f, -10.6f), EntranceSpot));
            Put(dave, new Vector3(-4.6f, 0f, -10.2f), EntranceSpot);
            cam.Frame(new Vector3(-8.6f, 1.6f, -13.4f), new Vector3(-5f, 1.2f, -10.4f), 44f);
            yield return FadeIn(0.8f);
            playerActor.EndActing();
            yield return Wait(0.2f);
            playerActor.BeginActing();
            dave.WalkTo(EntranceSpot + new Vector3(1f, 0f, 0.5f));
            yield return Say("Dave", "You know what I realized?");
            yield return Say("You", "That your setting needs work?");
            yield return Say("Dave", "Besides that. When we actually get three good touches… it feels amazing.");
            yield return Say("You", "Yeah. It's like everything makes sense for a second.");
            yield return Say("Dave", "Exactly. One second. Then Steve makes you do it forty more times.");
            Relationship("dave", 1);
        }

        // ---------- Day 3: Keep It Alive ----------

        IEnumerator Day3()
        {
            yield return ui.TitleCard("Day 3", "Keep It Alive", 2f);
            SitPlayer(BenchSeat(-0.4f), BenchFacing);
            SitNpc(dave, BenchSeat(0.5f), BenchFacing);
            Put(steve, BenchSpot + new Vector3(1.6f, 0f, 2.4f), BenchSpot);
            Put(alex, new Vector3(-1.4f, 0f, -2f), Vector3.zero);
            Put(nina, new Vector3(1.2f, 0f, -2.8f), new Vector3(-1.4f, 0f, -2f));
            cam.Frame(BenchSpot + new Vector3(2.8f, 1.4f, 2.4f), BenchSpot + new Vector3(0.5f, 0.9f, 0.3f), 46f);
            yield return FadeIn(1f);
            yield return Narrate("Noon. The air shimmers over the sand. Even the seagulls are standing in the shade.");
            dave.Gesture("Drink");
            yield return Say("Dave", "Whose idea was a noon session?");
            steve.LookAt(player);
            yield return Say("Steve", "Mine. Tournaments don't wait for nice weather.");
            yield return Say("Steve", "Today is about the uncomfortable part. Anyone can look good when the ball comes straight to them. What matters is what you do when the rally gets ugly.");
            dave.Sit(false);
            yield return StandPlayer();

            // Alex.
            alex.LookAt(player);
            yield return WalkTo(alex, BenchSpot + new Vector3(1.4f, 0f, 1.8f));
            alex.LookAt(player);
            motor.FaceTowards(alex.transform.position, 0.3f);
            OverShoulder(player, alex.transform);
            yield return Say("Steve", "You haven't met Alex. He's been here since spring. If you want to learn to read a serve, watch him.");
            alex.Gesture("Nod");
            yield return Say("Alex", "Hey. Dave says you hit one over the net on your first day.");
            yield return Say("You", "He tells everyone that.");
            yield return Say("Alex", "He told me twice. It's a good story. Most people take a week.");
            yield return Say("Dave", "See? Legendary.");
            Relationship("alex", 1);
            yield return FadeOut(0.6f);

            // Drill 1: serving.
            Ready(DrillKind.ServeTarget);
            Put(alex, new Vector3(-1.8f, 0f, 5f), new Vector3(0f, 0f, -8f));
            Put(nina, new Vector3(1.8f, 0f, 5f), new Vector3(0f, 0f, -8f));
            Put(steve, new Vector3(3.6f, 0f, -8.2f), drill.SpotFor(DrillKind.ServeTarget));
            Put(dave, new Vector3(-3.8f, 0f, -8.6f), drill.SpotFor(DrillKind.ServeTarget));
            drill.FeederA = alex.transform;
            drill.FeederB = nina.transform;
            TwoShot(player, steve.transform, -1f, 4.4f);
            yield return FadeIn(0.6f);
            yield return Say("Steve", "Every rally starts with a serve. Toss with C, then hit — left mouse for power, F to drop it short. Get it in. That's the whole job today.");
            ShowEnergy();
            yield return Drill(DrillKind.ServeTarget, 6, "Serving");
            yield return Say("Steve", Verdict(lastResult,
                "Reliable. In a match, reliable beats fancy.",
                "Some in, some not. Same toss every time and the rest follows.",
                "Your toss is wandering. Fix the toss and the serve fixes itself."));

            // Drill 2: receiving Alex's serve.
            yield return FadeOut(0.5f);
            Ready(DrillKind.ServeReceive);
            Put(alex, new Vector3(0.5f, 0f, 8.7f), Vector3.zero);
            Put(nina, Sideline2, Vector3.zero);
            Put(steve, SteveSideline, Vector3.zero);
            drill.FeederA = alex.transform;
            cam.Frame(new Vector3(-2.8f, 1.8f, -8f), alex.transform.position + Vector3.up * 1.5f, 40f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Switch. Alex serves, you receive. You'll feel how your Serve and your Bump are two halves of the same rally.");
            yield return Say("Alex", "I'll keep them floaty. Mostly.");
            yield return Drill(DrillKind.ServeReceive, 6, "Serve receive");
            yield return Say("Alex", Verdict(lastResult,
                "You read those better than I did my first month.",
                "You're getting your feet there. The arms will catch up.",
                "Don't watch the ball leave my hand — watch my shoulder. It gives it away."));

            // Drill 3: pepper, after a ball nobody takes.
            yield return FadeOut(0.5f);
            PlaceAt(new Vector3(-0.9f, 0f, -4.6f), Quaternion.identity);
            Put(dave, new Vector3(0.9f, 0f, -4.6f), Vector3.zero);
            Put(steve, new Vector3(0f, 0f, -1.2f), new Vector3(0f, 0f, -4.6f));
            Put(alex, Sideline, Vector3.zero);
            TwoShot(player, dave.transform, 1f, 5f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Pepper. Keep the ball alive between you. If you can, roll it — don't smash it. Control, not power.");
            yield return Say("Dave", "Ready?");
            yield return Say("You", "No.");
            yield return Say("Dave", "Perfect.");
            yield return Say("Steve", "First, a free ball. Between you two.");
            {
                // Staged: it drops right between them.
                var ball = FindAnyObjectByType<BeachVolley.Ball.VolleyBall>();
                steve.Volley(HitType.Bump);
                Vector3 from = steve.transform.position + Vector3.up * 1.4f + steve.transform.forward * 0.4f;
                ball.Place(from);
                ball.Launch(ball.VelocityToReach(from, new Vector3(0f, 0.16f, -4.7f), 4f));
                cam.Frame(new Vector3(0f, 1.4f, -9.2f), new Vector3(0f, 1f, -4.6f), 44f);
                yield return Wait(1.1f);
                playerActor.Gesture("Shrug");
                dave.Gesture("Shrug");
                yield return Wait(1f);
                dave.Gesture("Point");
                yield return Say("Dave", "Mine!");
                yield return Say("You", "You said that after it hit the sand!");
            }
            steve.LookAt(player);
            TwoShot(steve.transform, dave.transform, 1f, 4.6f);
            yield return Say("Steve", "And that's the most common point in beach volleyball. Two good players, one ball, nobody talking.");
            yield return Say("Steve", "You don't need to shout constantly. You need to make your intentions clear — early. \"Mine\" after the ball lands is just a sound.");
            yield return FadeOut(0.5f);

            Ready(DrillKind.Pepper);
            Put(dave, new Vector3(0f, 0f, 3.6f), drill.SpotFor(DrillKind.Pepper));
            Put(steve, SteveSideline, Vector3.zero);
            TwoShot(player, dave.transform, 1f, 7f);
            yield return FadeIn(0.5f);
            yield return Say("Dave", "Over the net this time, so we can't both stand there. I send it, you get it back. Bump it up first if you need to, then roll it over.");
            yield return Drill(DrillKind.Pepper, 4, "Pepper");
            int chain = drill.LongestChain;
            SetFlag("ch2_best_chain", chain.ToString());
            if (chain >= 5)
            {
                dave.Gesture("Cheer");
                yield return Say("Dave", $"Okay. {chain} in a row. That was actually pretty good.");
                yield return Say("You", "Don't sound so surprised.");
                yield return Say("Steve", "That's what you're looking for. Not perfection. Consistency.");
            }
            else if (chain >= 3)
            {
                yield return Say("Dave", "We kept it alive! For a bit! That counts!");
                yield return Say("Steve", "It counts. Next time, make the bit longer.");
            }
            else
            {
                yield return Say("Dave", "In my defense, the wind exists.");
                yield return Say("Steve", "There's no wind, Dave. — Short chains are normal at first. Every ball you return is a ball you didn't give away.");
            }
            yield return Say("Steve", "And watch your energy. Tired legs make late decisions. Drink something.");

            // Break: optional talks.
            yield return FadeOut(0.5f);
            drill.HideMarker();
            Put(alex, new Vector3(-4.4f, 0f, -6.8f), BenchSpot);
            Put(dave, new Vector3(-3.6f, 0f, -2.4f), Vector3.zero);
            Put(steve, SteveSideline, Vector3.zero);
            PlaceAt(new Vector3(-1.5f, 0f, -5f), Facing(new Vector3(-1.5f, 0f, -5f), BenchSpot));
            yield return FadeIn(0.5f);
            breakTalk = Day3Talk;
            yield return Break("Five-minute break\n<size=16>Walk around (WASD), E to talk or drink  ·  go back to the court when you're ready</size>",
                Talkable(alex, "Talk to Alex"), Talkable(dave, "Talk to Dave"), cooler);
            breakTalk = null;

            // Drill 4: the pancake.
            yield return FadeOut(0.5f);
            Ready(DrillKind.Pancake);
            Put(steve, new Vector3(1.6f, 0f, -2f), drill.SpotFor(DrillKind.Pancake));
            Put(dave, Sideline, Vector3.zero);
            drill.FeederA = steve.transform;
            TwoShot(player, steve.transform, -1f, 4.4f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Last thing. Sometimes you can't get there with your feet. That's when you decide whether an emergency save is possible.");
            steve.Volley(HitType.Dive);
            yield return Say("Steve", "A pancake: you dive, slide your hand flat on the sand, and the ball bounces off the back of it instead of the beach.");
            yield return Say("Steve", "It's not something you use when there's an easier option. It's a last-second save. Late is right; early is sand in your mouth. E to dive.");
            yield return Drill(DrillKind.Pancake, 6, "Pancake");
            if (lastResult.pancakes > 0) SetFlag("ch2_pancake", "yes");
            yield return Say("Steve", lastResult.pancakes > 0
                ? "Good. That's one more tool. But remember: the best defense is still getting yourself in position early enough that you don't need a miracle."
                : "No pancakes yet — that's fine. You know what it is now. It's a tool for later, and the best defense is still getting there early enough not to need it.");

            // After training: everything hurts.
            yield return FadeOut(0.8f);
            drill.HideMarker();
            SitPlayer(BenchSeat(-0.4f), BenchFacing);
            SitNpc(dave, BenchSeat(0.5f), BenchFacing);
            Put(steve, BenchSpot + new Vector3(1.4f, 0f, 2.3f), BenchSpot);
            cam.Frame(BenchSpot + new Vector3(-1.4f, 1.2f, 2.8f), BenchSpot + new Vector3(0.5f, 0.9f, 0f), 44f);
            yield return FadeIn(0.8f);
            playerActor.Gesture("Sigh");
            yield return Say("You", "I thought volleyball was supposed to be fun.");
            yield return Say("Dave", "It is.");
            yield return Say("You", "Then why does everything hurt?");
            yield return Say("Dave", "Because you're doing it wrong.");
            steve.Gesture("Laugh");
            yield return Say("Steve", "He's joking. Mostly.");
            yield return Say("Steve", "Stamina matters — you felt your legs go today. But so does recovery. Sleep, food, water. Training is what you do here; getting better happens after you leave.");
            dave.Sit(false);
            yield return Wait(0.3f);
            yield return StandPlayer();
        }

        IEnumerator Day3Talk(Interactable it, System.Action finish)
        {
            var who = it.GetComponent<StoryActor>();
            if (who == alex)
            {
                alex.LookAt(player);
                OverShoulder(player, alex.transform);
                yield return Say("Alex", "You're wondering about Marlon. Everyone does, week one.");
                yield return Say("You", "Is it that obvious?");
                yield return Say("Alex", "You keep looking at his court. — He's the best player here, and he knows it. He's also here every day before anyone and after everyone.");
                yield return Say("Alex", "Doesn't make him nice. Makes him serious.");
                SetFlag("alex_on_marlon", "serious");
                Relationship("alex", 1);
                it.Available = false;
            }
            else if (who == dave)
            {
                dave.LookAt(player);
                OverShoulder(player, dave.transform);
                yield return Say("Dave", StoryState.Is("dave_tone", "teasing")
                    ? "Don't tell me — another lucky guess on that last serve?"
                    : "Coach Dave's tip of the day: never stand where Steve can reach you with a ball.");
                yield return Say("You", "Why did you start? Volleyball, I mean.");
                yield return Say("Dave", "My sister played. She was so good it was annoying. I wanted to be annoying back.");
                yield return Say("Dave", "Now she's at university and I'm the one who stayed. Funny how that works.");
                SetFlag("dave_sister", "known");
                Relationship("dave", 1);
                it.Available = false;
            }
            cam.Follow();
        }

        Interactable Talkable(StoryActor who, string prompt)
        {
            if (who == null) return null;
            var it = who.GetComponent<Interactable>();
            if (it == null) return null;
            it.prompt = prompt;
            it.Available = true;
            return it;
        }

        // ---------- Day 4: Read the Game ----------

        IEnumerator Day4()
        {
            yield return ui.TitleCard("Day 4", "Read the Game", 2f);
            ShowEnergy();
            // Cold open: watching Marlon on the next court.
            Vector3 watch = new Vector3(8.6f, 0f, -7.6f);
            PlaceAt(watch, Facing(watch, clubRally.transform.position));
            Put(dave, watch + new Vector3(1f, 0f, 0.2f), clubRally.transform.position);
            Put(steve, SteveSideline, Vector3.zero);
            Put(alex, new Vector3(1f, 0f, 2.6f), Vector3.zero);
            Put(nina, new Vector3(-0.6f, 0f, 2.6f), Vector3.zero);
            clubRally.StopLoop();
            cam.Frame(watch + new Vector3(-1.2f, 1.75f, -1.4f), clubRally.transform.position + new Vector3(-1.2f, 1.6f, -1f), 40f);
            yield return FadeIn(0.9f);
            yield return Narrate("Evening. The wind has picked up and the flags snap on their poles. On the next court, the experienced players are working on block and defense.");
            yield return clubRally.PowerSpike();
            yield return Narrate("Marlon goes up, lands, and is already moving to the next spot before the ball stops rolling.");
            yield return Say("You", "How does he know where the ball is going?");
            yield return Say("Dave", "He watches the attacker. Not just the ball.");
            clubRally.StartLoop();
            yield return WalkTo(steve, watch + new Vector3(0.4f, 0f, 1.5f));
            steve.LookAt(player);
            TwoShot(player, steve.transform, -1f, 3.8f);
            yield return Say("Steve", "Today, that's what we work on. You've learned to move, pass, set and attack. Now you need to understand what the other team is trying to do.");
            yield return Say("Steve", "A defender who reacts after the ball is hit is already late. A good one reads it before.");
            yield return FadeOut(0.6f);

            // The signals.
            PlaceAt(new Vector3(-1f, 0f, -4.8f), Quaternion.identity);
            Put(dave, new Vector3(0.3f, 0f, -0.7f), new Vector3(0f, 0f, 5f));
            Put(steve, new Vector3(1.8f, 0f, -3.4f), new Vector3(-1f, 0f, -4.8f));
            cam.Frame(new Vector3(-0.2f, 1.6f, -3.2f), dave.transform.position + new Vector3(0f, 1f, 0f), 46f);
            yield return FadeIn(0.6f);
            yield return Say("Steve", "In beach volleyball, one of you blocks, the other defends behind. And the blocker tells the defender the plan — behind their back, so the other team can't see.");
            ui.ShowSignal(1, null);
            yield return Say("Steve", "One finger: the blocker takes the line — the shot straight down the sideline. You, behind, take the cross-court shot.");
            ui.ShowSignal(2, null);
            yield return Say("Steve", "Two fingers: the blocker takes the cross. You cover the line.");
            ui.ShowSignal(0, null);
            yield return Say("Steve", "It only works if you both read the same plan. Two good decisions that don't match are worse than one bad one.");
            yield return Say("Dave", "I'll show the signal. You go to the side I'm NOT blocking. The rings show you the two shots.");

            // Drill 1: signals (the first rep sets up the lesson).
            Ready(DrillKind.BlockSignal);
            Put(dave, new Vector3(0.3f, 0f, -0.7f), new Vector3(0f, 0f, 5f));
            Put(alex, new Vector3(2.6f, 0f, 1.3f), new Vector3(0f, 0f, -5f));
            Put(nina, new Vector3(-0.4f, 0f, 2.4f), new Vector3(2.6f, 0f, 1.3f));
            Put(steve, SteveSideline, Vector3.zero);
            drill.FeederA = alex.transform;
            drill.FeederB = nina.transform;
            yield return Drill(DrillKind.BlockSignal, 1, null);
            bool firstRead = lastResult.good > 0;
            TwoShot(player, dave.transform, 1f, 5f);
            if (!firstRead)
            {
                steve.LookAt(player);
                yield return Say("Steve", "You both made a decision. The problem is that you made two different decisions.");
                yield return Say("Dave", "I thought you were going line.");
                yield return Say("You", "I thought you were taking line!");
                yield return Say("Steve", "And that's why you agree on the plan before the rally starts. Again.");
            }
            else
            {
                yield return Say("Steve", "Good — one finger, he took line, you took cross. That's the whole system. Now do it when it's not the first ball.");
            }
            yield return Drill(DrillKind.BlockSignal, 5, "Blocking signals");
            yield return Say("Steve", Verdict(lastResult,
                "You're reading his hand like a book.",
                "Most of the time you were where you needed to be. That's a defense.",
                "Fingers, then feet. Every time. It becomes automatic."));

            // Drill 2: block or defend.
            yield return FadeOut(0.5f);
            Ready(DrillKind.BlockOrDefend);
            Put(alex, new Vector3(1f, 0f, 2.6f), new Vector3(0f, 0f, -5f));
            Put(nina, new Vector3(-0.8f, 0f, 2.3f), new Vector3(1f, 0f, 2.6f));
            Put(dave, Sideline2, Vector3.zero);
            Put(steve, new Vector3(4.6f, 0f, -1.8f), Vector3.zero);
            drill.FeederA = alex.transform;
            drill.FeederB = nina.transform;
            cam.Frame(new Vector3(-2.4f, 2f, -4.2f), new Vector3(0.6f, 1.6f, 0.6f), 48f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Now you're the blocker. Nina sets Alex. Sometimes it's a good set, tight to the net — then you block, Q. Sometimes it's a bad one, far off the net.");
            yield return Say("Steve", "Then think. Can he make a strong attack from there? If not, don't stand at the net blocking air. Get off it — behind the line — and dig the soft shot.");
            yield return Drill(DrillKind.BlockOrDefend, 6, "Block or defend?");
            yield return Say("Steve", "Don't block the attack you expected. Defend the attack they're actually capable of making.");
            yield return Narrate("For the first time, he understands that court awareness isn't just knowing where the ball is. It's knowing what the other side can do with it.");

            // Break: the team board, Marlon.
            yield return FadeOut(0.5f);
            drill.HideMarker();
            marlon.transform.position = new Vector3(6.9f, 0f, -6.2f);
            marlon.transform.rotation = Quaternion.LookRotation(Vector3.left);
            clubRally.StopLoop();
            Put(alex, new Vector3(-4.4f, 0f, -6.8f), BenchSpot);
            Put(dave, new Vector3(-3.8f, 0f, -2.6f), Vector3.zero);
            PlaceAt(new Vector3(-1f, 0f, -5f), Facing(new Vector3(-1f, 0f, -5f), new Vector3(-5f, 0f, -9f)));
            yield return FadeIn(0.5f);
            breakTalk = Day4Talk;
            yield return Break("Short break\n<size=16>Walk around (WASD), E to look or talk  ·  back to the court when you're ready</size>",
                board, Talkable(marlon, "Talk to Marlon"), Talkable(alex, "Talk to Alex"), cooler);
            breakTalk = null;
            clubRally.ResetPositions();
            clubRally.StartLoop();

            // Drill 3: block and dig with Dave.
            yield return FadeOut(0.5f);
            Ready(DrillKind.BlockDig);
            Put(dave, new Vector3(0.3f, 0f, -0.7f), new Vector3(0f, 0f, 5f));
            Put(alex, new Vector3(2.6f, 0f, 1.3f), new Vector3(0f, 0f, -5f));
            Put(nina, new Vector3(-0.4f, 0f, 2.4f), new Vector3(2.6f, 0f, 1.3f));
            Put(steve, SteveSideline, Vector3.zero);
            drill.FeederA = alex.transform;
            drill.FeederB = nina.transform;
            TwoShot(player, dave.transform, 1f, 5f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Put it together. Faster, no captions — just the hand. Dave blocks, you dig, Dave turns for the next touch.");
            yield return Say("Dave", "I take line, you cover cross. Unless I show two. Then the other thing.");
            yield return Say("You", "Got it. The other thing.");
            yield return Drill(DrillKind.BlockDig, 6, "Block and dig");
            steve.Gesture("Nod");
            yield return Say("Steve", lastResult.Ratio >= 0.5f ? "Good. Now you're defending as a team." : "When it worked, you were defending as a team. That's what we're building.");
            yield return Say("Steve", "The block and the dig are two parts of the same defense.");

            // After: watching Marlon, and the question.
            yield return FadeOut(0.8f);
            PlaceAt(new Vector3(8.6f, 0f, -7.6f), Facing(new Vector3(8.6f, 0f, -7.6f), clubRally.transform.position));
            Put(dave, new Vector3(9.6f, 0f, -7.4f), clubRally.transform.position);
            cam.Frame(new Vector3(7.4f, 1.7f, -9.2f), clubRally.transform.position + new Vector3(-1.4f, 1.4f, -1f), 40f);
            yield return FadeIn(0.8f);
            yield return clubRally.PowerSpike();
            clubRally.StartLoop();
            yield return Say("You", "He's really good.");
            yield return Say("Dave", "Yeah.");
            yield return Choose("“Does he always act like everyone else is beneath him?”", "“I want to beat him. Not today. But one day.”", "“I wonder why he's like that.”");
            switch (Choice)
            {
                case 0:
                    SetFlag("marlon_view", "arrogant");
                    dave.Gesture("Shrug");
                    yield return Say("Dave", "Pretty much. But don't let him get in your head. That's the only place he can actually beat you right now.");
                    break;
                case 1:
                    SetFlag("marlon_view", "rival");
                    Relationship("marlon_rivalry", 1);
                    dave.Gesture("Laugh");
                    yield return Say("Dave", "You told him that on day one, remember? \"We'll see who's laughing.\" He remembers. Trust me.");
                    break;
                default:
                    SetFlag("marlon_view", "curious");
                    yield return Say("Dave", "Steve knows. He won't say. Something happened before I got here — Marlon used to have a partner, I think.");
                    SetFlag("marlon_hint_partner", "yes");
                    break;
            }
            yield return Narrate("He watches Marlon go up for another block. One day, he thinks, he'll be on the other side of that net. Or the same side. He isn't sure which scares him more.");
        }

        IEnumerator Day4Talk(Interactable it, System.Action finish)
        {
            if (it == board)
            {
                ui.ShowPhone(true, "CLUB BOARD");
                ui.PhoneCard("Lost and found", "One sandal (left). Sunglasses, three pairs. A towel that has seen things.");
                ui.PhoneCard("Sunday", "Beach clean-up, 9 AM. Coffee for everyone who shows up. — S.");
                ui.PhoneCard("Court 3", "Net fixed. STOP HANGING ON IT.");
                ui.PhoneCard("Photo", "Last summer's members, squinting into the sun. A younger Marlon in the back row — grinning, next to someone whose face has been covered by a newer flyer.");
                yield return ui.PhoneChoose("Step back");
                ui.ShowPhone(false);
                yield return Think("Marlon, smiling. I didn't know his face could do that.");
                SetFlag("saw_old_photo", "yes");
                it.Available = false;
                yield break;
            }
            var who = it.GetComponent<StoryActor>();
            if (who == marlon)
            {
                marlon.LookAt(player);
                OverShoulder(player, marlon.transform);
                marlon.Gesture("ArmsCrossed");
                yield return Say("Marlon", "You're still here?");
                yield return Say("You", "Day four. Turns out I'm stubborn.");
                marlon.EndPose("ArmsCrossed");
                yield return Say("Marlon", "Stubborn is easy. Showing up for a year is hard.");
                yield return Say("You", "Do you ever just… play for fun?");
                yield return Narrate("For half a second, something in his face changes.");
                yield return Say("Marlon", "Some of us don't have time to be beginners.");
                marlon.LookAt(null);
                SetFlag("marlon_hint2", "no_time");
                it.Available = false;
            }
            else if (who == alex)
            {
                alex.LookAt(player);
                OverShoulder(player, alex.transform);
                yield return Say("Alex", "Want the one thing nobody tells beginners? Call every ball. Loud. Even the ones you're not sure about.");
                yield return Say("You", "Even if I'm wrong?");
                yield return Say("Alex", "Wrong and loud, your partner adjusts. Right and quiet, the ball lands between you.");
                Relationship("alex", 1);
                it.Available = false;
            }
            cam.Follow();
        }

        // ---------- Day 5: Two Against Two ----------

        CourtAI daveAI, marlonAI, alexAI;
        int beat;
        int beatArmedAt = -1;
        bool marlonFinally, mockedLucky, daveNextBall;

        IEnumerator Day5()
        {
            yield return ui.TitleCard("Day 5", "Two Against Two", 2f);
            daveAI = dave.GetComponent<CourtAI>();
            marlonAI = marlon.GetComponent<CourtAI>();
            alexAI = alex.GetComponent<CourtAI>();
            clubRally.StopLoop();

            // The rotation announcement.
            PlaceAt(Huddle + new Vector3(-0.9f, 0f, 0f), Facing(Huddle, new Vector3(0.8f, 0f, -3.8f)));
            Put(steve, new Vector3(0.8f, 0f, -3.8f), Huddle);
            Put(dave, Huddle + new Vector3(0.2f, 0f, 0.4f), steve.transform.position);
            Put(alex, Huddle + new Vector3(1.2f, 0f, 0.2f), steve.transform.position);
            marlon.transform.position = Huddle + new Vector3(2.3f, 0f, -0.2f);
            marlon.transform.rotation = Facing(marlon.transform.position, steve.transform.position);
            Put(nina, Sideline2, Vector3.zero);
            cam.Frame(new Vector3(0.6f, 2.2f, -1.4f), Huddle + Vector3.up * 1.2f, 50f);
            yield return FadeIn(0.9f);
            yield return Narrate("Late afternoon, the sun low and gold. Everyone's here. Even the people who usually leave early.");
            steve.Gesture("Clap");
            yield return Say("Steve", "Games today. Short ones, two against two, first to ten. We rotate, so everyone plays with different people.");
            yield return Think("Dave. Please be Dave.");
            yield return Say("Steve", "Game one: you and Marlon, against Dave and Alex.");
            marlon.LookAt(player);
            OverShoulder(player, marlon.transform);
            yield return Say("Marlon", "You're kidding.");
            yield return Say("Steve", "No. You're playing together.");
            marlon.Gesture("Dismiss");
            yield return Say("Marlon", "And you expect me to win?");
            yield return Say("Steve", "I expect you to play volleyball. Get on the court.");
            OverShoulder(marlon.transform, player, -1f);
            yield return Say("Marlon", "Listen carefully. If the ball comes to you, call it. If you can't reach it, get out of the way. And please don't make me do everything.");
            yield return Say("You", "Nice to meet you, too.");
            yield return Say("Marlon", "Just play.");
            yield return FadeOut(0.6f);

            // ---- Game 1: with Marlon ----
            Put(steve, new Vector3(5.4f, 0f, -0.4f), Vector3.zero);
            match.Setup("YOU & MARLON", marlonAI, "DAVE & ALEX", daveAI, alexAI, farBias: 1f);
            match.BetweenPoints = Game1Beat;
            match.PointScored += Game1Bark;
            contact.Played += Game1Touch;
            beat = 0;
            yield return PlayMatch();
            match.PointScored -= Game1Bark;
            contact.Played -= Game1Touch;
            var g1 = match.Stats;
            bool wonWithMarlon = g1.NearWon;
            SetFlag("ch2_marlon_match", wonWithMarlon ? "won" : "lost");
            extraLines.Add(new TrainingLine("Game 1 · with Marlon", $"{g1.nearPoints} : {g1.farPoints}  {(wonWithMarlon ? "won" : "lost")}", (wonWithMarlon ? 1 : 0) + (g1.PlayedWell ? 1 : 0)));

            // After game 1.
            yield return FadeOut(0.6f);
            PlaceAt(new Vector3(-2.4f, 0f, -9.2f), Facing(new Vector3(-2.4f, 0f, -9.2f), new Vector3(4f, 0f, -9f)));
            marlon.transform.position = new Vector3(1.6f, 0f, -8.8f);
            marlon.transform.rotation = Quaternion.LookRotation(Vector3.right);
            Put(dave, new Vector3(-1.4f, 0f, -8.4f), new Vector3(-2.4f, 0f, -9.2f));
            cam.Frame(new Vector3(-4.8f, 1.7f, -10.8f), new Vector3(0f, 1.2f, -8.8f), 44f);
            yield return FadeIn(0.6f);
            marlon.WalkTo(new Vector3(9f, 0f, -9f));
            yield return Narrate(wonWithMarlon
                ? "They win. Marlon collects the ball, and walks to the next court without a word — as if they hadn't."
                : "They lose. Marlon collects the ball and heads for the next court without saying anything else.");
            if (wonWithMarlon) yield return Say("Marlon", "We won. Don't get used to it.");
            dave.LookAt(player);
            TwoShot(player, dave.transform, 1f, 3.4f);
            yield return Say("Dave", "You alright?");
            yield return Choose("“Yeah. I'm fine.”", "“Honestly? No.”");
            if (Choice == 0)
            {
                SetFlag("marlon_match_reaction", "brushed_off");
                yield return Say("Dave", "You don't have to pretend.");
                yield return Narrate("He looks across the sand at Marlon.");
                yield return Say("You", "He's good. I know he's good. But that doesn't mean he gets to talk to people like that.");
            }
            else
            {
                SetFlag("marlon_match_reaction", "honest");
                Relationship("dave", 1);
                yield return Say("You", "He's good. I know he's good. I just didn't think it would feel that bad. Being the reason, I mean.");
                yield return Say("Dave", "You weren't the reason. You were the excuse.");
            }
            yield return Say("Dave", "No. It doesn't. For what it's worth, you made some good plays. He just wasn't interested in noticing them.");
            if (g1.humanGoodTouches >= 6) yield return Say("Dave", $"I counted. Well — I noticed. That dig in the middle of the game? I'd have shanked that.");
            yield return Narrate("He nods. It helps more than he expected.");

            // ---- Game 2: with Dave ----
            yield return FadeOut(0.6f);
            PlaceAt(Huddle + new Vector3(-0.9f, 0f, 0f), Facing(Huddle, new Vector3(0.8f, 0f, -3.8f)));
            Put(steve, new Vector3(0.8f, 0f, -3.8f), Huddle);
            Put(dave, Huddle + new Vector3(0.3f, 0f, 0.4f), player.position);
            cam.Frame(new Vector3(-0.4f, 1.8f, -4.2f), Huddle + Vector3.up * 1.3f, 46f);
            yield return FadeIn(0.6f);
            yield return Say("Steve", "Next rotation. You and Dave, against Marlon and Alex.");
            dave.LookAt(player);
            TwoShot(player, dave.transform, 1f, 3f);
            yield return Say("Dave", "Ready?");
            yield return Say("You", "I think so.");
            yield return Say("Dave", "Good enough. I'll take the first ball if it comes short.");
            yield return Say("You", "Got it. I'll cover deep.");
            yield return Say("Dave", "And if it goes between us, it's yours. I'll yell anyway.");
            yield return FadeOut(0.6f);

            Put(steve, new Vector3(5.4f, 0f, -0.4f), Vector3.zero);
            match.Setup("YOU & DAVE", daveAI, "MARLON & ALEX", marlonAI, alexAI, farBias: 0.85f);
            match.BetweenPoints = Game2Beat;
            match.PointScored += Game2Bark;
            yield return PlayMatch();
            match.PointScored -= Game2Bark;
            var g2 = match.Stats;
            bool wonWithDave = g2.NearWon;
            SetFlag("ch2_dave_match", wonWithDave ? "won" : "lost");
            if (wonWithDave) Relationship("dave", 1);
            extraLines.Add(new TrainingLine("Game 2 · with Dave", $"{g2.nearPoints} : {g2.farPoints}  {(wonWithDave ? "won" : "lost")}", (wonWithDave ? 1 : 0) + (g2.PlayedWell ? 1 : 0)));

            // Debrief.
            yield return FadeOut(0.6f);
            PlaceAt(Huddle + new Vector3(-0.9f, 0f, 0f), Facing(Huddle, new Vector3(0.8f, 0f, -3.8f)));
            Put(steve, new Vector3(0.8f, 0f, -3.8f), Huddle);
            Put(dave, Huddle + new Vector3(0.2f, 0f, 0.4f), steve.transform.position);
            Put(alex, Huddle + new Vector3(1.2f, 0f, 0.2f), steve.transform.position);
            marlon.transform.position = Huddle + new Vector3(2.4f, 0f, -0.3f);
            marlon.transform.rotation = Facing(marlon.transform.position, steve.transform.position);
            cam.Frame(new Vector3(0.6f, 2.2f, -1.4f), Huddle + Vector3.up * 1.2f, 50f);
            yield return FadeIn(0.6f);
            yield return Narrate(wonWithDave
                ? $"{g2.nearPoints} to {g2.farPoints}. They won. Dave hasn't stopped grinning."
                : $"{g2.nearPoints} to {g2.farPoints}. They lost — but it never felt like losing alone.");
            yield return Say("Steve", "That's enough for today.");
            steve.LookAt(player);
            yield return Say("Steve", "You two made mistakes. Plenty of them. But you communicated, you adapted, and you kept playing together. That's progress.");
            steve.LookAt(marlon.transform);
            yield return Say("Steve", "Marlon. Remember what we talked about earlier.");
            marlon.Gesture("LookAway");
            yield return Say("Marlon", "I remember.");
            yield return Narrate("No handshake. No apology. But he doesn't walk away until Steve does.");
            Relationship("steve", 1);
        }

        IEnumerator PlayMatch()
        {
            ui.SetBlack(true);
            StartCoroutine(FadeIn(0.6f));
            playerActor.EndActing();
            cutscene = false;
            contact.enabled = true;
            cam.Follow();
            ShowEnergy();
            yield return match.Play();
            contact.enabled = false;
            contact.ShowControlsHint = false;
            cutscene = true;
            playerActor.BeginActing();
        }

        /// <summary>Pauses play for a cutscene between points, then hands control back.</summary>
        IEnumerator Interlude(IEnumerator scene)
        {
            foreach (var ai in new[] { daveAI, marlonAI, alexAI }) if (ai != null) ai.Face(null);
            contact.enabled = false;
            cutscene = true;
            playerActor.BeginActing();
            yield return scene;
            playerActor.EndActing();
            cutscene = false;
            contact.enabled = true;
            cam.Follow();
        }

        // Game 1 staging: an early complaint, the gap ball, the bad set, and Steve.
        void Game1Touch(HitType type, float height)
        {
            if (beat == 3 && type == HitType.Set)
            {
                match.StageNetAttack(marlonAI);
                beat = 4;
            }
        }

        IEnumerator Game1Beat(MatchPoint mp)
        {
            if (match.Finished) yield break;
            switch (beat)
            {
                case 0:
                    beat = 1;
                    yield return Interlude(Game1Opener(mp));
                    match.StageGapBall();
                    break;
                case 1:
                    // The point the staged gap ball was played in. If their attack never comes, move on.
                    if (!match.GapPending)
                    {
                        beat = 2;
                        yield return Interlude(GapScene(mp.reason == PointReason.Gap));
                        beat = 3;
                        beatArmedAt = mp.index;
                    }
                    else if (mp.index >= 5)
                    {
                        match.CancelGap();
                        beat = 3;
                        beatArmedAt = mp.index;
                    }
                    break;
                case 3:
                    // If he hasn't set the ball in three points, Marlon's next attack goes in anyway.
                    if (mp.index - beatArmedAt >= 3) { match.StageNetAttack(marlonAI); beat = 5; }
                    break;
                case 4:
                case 5:
                    if (mp.lastAi == marlonAI && !mp.nearWon)
                    {
                        bool afterSet = beat == 4;
                        beat = 6;
                        yield return Interlude(SteveStepsIn(afterSet));
                    }
                    break;
            }
        }

        IEnumerator Game1Opener(MatchPoint mp)
        {
            OverShoulder(player, marlon.transform);
            marlon.LookAt(player);
            yield return Say("Marlon", mp.nearWon
                ? "Fine. Now pass it higher. I can't hit what I have to chase."
                : "Higher. Pass it higher. I'm not running across the whole court for your first touch.");
            marlon.LookAt(null);
        }

        IEnumerator GapScene(bool dropped)
        {
            TwoShot(player, marlon.transform, 1f, 3.4f);
            marlon.LookAt(player);
            if (dropped)
            {
                marlon.Gesture("Angry");
                yield return Say("Marlon", "Seriously?! You just stood there!");
                yield return Say("You", "I thought you were taking it!");
                yield return Say("Marlon", "Then you should have called it!");
                yield return Narrate("On the sideline, Steve watches. He doesn't say anything. Yet.");
            }
            else
            {
                marlon.Gesture("Arguing");
                yield return Say("Marlon", "That was mine! You went for my ball — call it!");
                yield return Say("You", "It was in the middle! Nobody called anything!");
                yield return Say("Marlon", "Then call it. Every time.");
            }
            marlon.LookAt(null);
        }

        IEnumerator SteveStepsIn(bool afterSet)
        {
            TwoShot(player, marlon.transform, 1f, 3.2f);
            marlon.LookAt(player);
            marlon.Gesture("Angry");
            yield return Say("Marlon", afterSet ? "What was that set?" : "What was that pass?");
            yield return Say("You", "It wasn't that bad.");
            yield return Say("Marlon", afterSet ? "It was terrible. Do you even know where you're supposed to put the ball?" : "It was terrible. Do you even know where the ball is supposed to go?");
            yield return Say("You", "I'm trying to learn.");
            yield return Say("Marlon", "Then learn faster. I'm not here to babysit someone who doesn't know the first thing about this sport.");
            yield return Narrate("And he keeps going. The words get smaller and meaner — about how he plays, how he looks, whether he should even be here.");
            playerActor.Gesture("LookAway");
            yield return Narrate("He looks down at the sand. His face is hot, and it isn't the sun.");

            // The whistle.
            steve.LookAt(marlon.transform);
            yield return WalkTo(steve, new Vector3(3.2f, 0f, -0.8f));
            OverShoulder(marlon.transform, steve.transform, -1f);
            yield return Say("Steve", "Marlon. Enough.");
            marlon.LookAt(steve.transform);
            yield return Say("Marlon", "What? I'm telling him what he's doing wrong.");
            yield return Say("Steve", "There's a difference between giving someone feedback and humiliating them.");
            yield return Say("Marlon", "We're playing a match. If he can't handle criticism, he shouldn't be here.");
            yield return WalkTo(steve, new Vector3(2.2f, 0f, -0.6f));
            steve.LookAt(marlon.transform);
            yield return Say("Steve", "He's a beginner. You knew that before you stepped onto the court.");
            yield return Say("Marlon", "And I'm supposed to lose because of it?");
            yield return Say("Steve", "You're supposed to play as a team. You're one of the strongest players here, and you know exactly how much experience you have over him.");
            yield return Wait(0.6f);
            yield return Say("Steve", "Being better than someone doesn't give you the right to treat them like they're beneath you. If you want to be a great player, learn how to make your partner better, too.");
            marlon.Gesture("LookAway");
            yield return Say("Marlon", "Whatever.");
            yield return Say("Steve", "No. Look at me.");
            marlon.LookAt(steve.transform);
            yield return Say("Steve", "You can be competitive. You can expect effort. You can tell him when a ball was poorly played. But you will not insult him like that on my court. Understood?");
            yield return Wait(0.8f);
            yield return Say("Marlon", "Understood.");
            yield return Say("Steve", "Good. Finish the game.");
            marlon.LookAt(null);
            Relationship("steve", 1);
            SetFlag("steve_stepped_in", "yes");
            yield return WalkTo(steve, new Vector3(5.4f, 0f, -0.4f));
            yield return Narrate("The air is different now. Marlon doesn't apologize. He doesn't forgive him either. They play on.");
        }

        void Game1Bark(MatchPoint mp)
        {
            if (beat < 6) return;
            if (mp.nearWon && mp.lastAi == marlonAI && mp.reason == PointReason.Kill && !marlonFinally)
            {
                marlonFinally = true;
                Bark(marlon, "Finally.", 2f);
                return;
            }
            if (mp.nearWon && mp.humanLastTouch) Bark(dave, BarkPool.Once("Nice one!", "Okay, I felt that from here."), 2f);
            else if (!mp.nearWon && mp.lastAi == alexAI && mp.reason == PointReason.Kill) Bark(alex, BarkPool.Once("Sorry. Not sorry.", "Open court."), 2f);
        }

        IEnumerator Game2Beat(MatchPoint mp)
        {
            if (match.Finished) yield break;
            if (mp.index == 1)
                yield return Interlude(Game2Talk(mp.nearWon));
        }

        IEnumerator Game2Talk(bool wonFirst)
        {
            TwoShot(player, dave.transform, 1f, 3f);
            dave.LookAt(player);
            yield return Say("Dave", wonFirst ? "See? Talking. It's like a cheat code." : "That's fine. That's one. We talk, we fix it, we go again.");
            yield return Say("You", "Short ones are yours, deep ones are mine.");
            yield return Say("Dave", "And the middle is yours. I'll yell anyway.");
            dave.LookAt(null);
        }

        void Game2Bark(MatchPoint mp)
        {
            if (mp.nearWon)
            {
                if (mp.rallyTouches >= 5 && !mockedLucky)
                {
                    mockedLucky = true;
                    StartCoroutine(Banter((marlon, "You got lucky."), (dave, "Then we should keep getting lucky.")));
                }
                else if (mp.humanLastTouch) Bark(dave, BarkPool.Once("YES! That's us!", "Who taught you that? Oh right. Coach Dave.", "That's going in my highlight reel."), 2.2f);
                else Bark(dave, BarkPool.Once("Great pass! That's yours as much as mine.", "Your set, my point. I mean our point."), 2.2f);
            }
            else
            {
                if (!daveNextBall && mp.humanLastTouch)
                {
                    daveNextBall = true;
                    Bark(dave, "Forget it. Next ball. We've got this.", 2.6f);
                }
                else if (mp.reason == PointReason.Gap) Bark(dave, BarkPool.Once("That was mine — my bad. I'll call earlier.", "Middle ball, my fault. Yours next time, loud."), 2.6f);
                else if (mp.lastAi == marlonAI && mp.reason == PointReason.Kill && mp.farScore >= 7)
                {
                    if (BarkPool.Once("This isn't a lesson. It's a game.") is string jab)
                    {
                        Bark(marlon, jab, 2.2f);
                        StartCoroutine(SteveReminds());
                    }
                }
                else if (mp.lastAi == alexAI) Bark(alex, BarkPool.Once("Told you I'd serve you.", "Nothing personal."), 2f);
            }
        }

        IEnumerator SteveReminds()
        {
            yield return new WaitForSeconds(2.2f);
            Bark(steve, "It's a training game, Marlon.", 2.4f);
        }

        // ---------- End of day ----------

        IEnumerator Card()
        {
            var lines = new List<TrainingLine>();
            DrillKind? weakest = null;
            float worst = 2f;
            int total = 0;
            foreach (var (label, r) in log)
            {
                int pts = (r.Ratio >= 0.5f ? 1 : 0) + (r.Ratio >= 0.8f ? 1 : 0);
                string grade = r.kind == DrillKind.Pepper ? $"best chain {drill.LongestChain}" : $"{r.good}/{r.reps}" + (r.pancakes > 0 ? $" · {r.pancakes} pancake{(r.pancakes > 1 ? "s" : "")}" : "");
                lines.Add(new TrainingLine(label, grade, pts));
                total += pts;
                if (r.Ratio < worst) { worst = r.Ratio; weakest = r.kind; }
            }
            foreach (var l in extraLines) { lines.Add(l); total += l.points; }
            lines.Add(new TrainingLine("Showed up", "day " + day + " of 5", 1));
            total += 1;
            skills.AwardPoints(total, "Training day " + day);
            SetFlag("dev_note", NoteFor(weakest));
            SetFlag("ch2_points_d" + day, total.ToString());

            string footer = day switch
            {
                1 => "Spend them at home: the laptop on your desk opens Player Development.",
                3 => "New technique: Dive (Pancake) — a last-second save. You can invest in it now.",
                5 => "Every skill is open now. There's no right build — just yours.",
                _ => "Points that aren't spent stay saved for later.",
            };
            yield return ui.TrainingCard($"TRAINING COMPLETE  ·  DAY {day}", lines, footer);
        }

        /// <summary>Steve's note for the development screen, from the day's weakest drill. Never a forced build.</summary>
        static string NoteFor(DrillKind? weakest) => weakest switch
        {
            DrillKind.PassLR or DrillKind.PassPressure => "Steve: \"You were late to a lot of balls today. Athleticism gets you there; Court Awareness gets you there earlier.\"",
            DrillKind.PassLow or DrillKind.ServeReceive => "Steve: \"Your first touch wandered. Bump is the skill for that — or Court Awareness, to read it sooner.\"",
            DrillKind.SetControl => "Steve: \"Your sets were the shakiest part. Set would help — so would patience.\"",
            DrillKind.ThreeTouch or DrillKind.SpikeTiming => "Steve: \"Attacking timing was the hard part. Spike sharpens the swing — and one day the gym will get you higher.\"",
            DrillKind.ServeTarget => "Steve: \"Serves went everywhere. Serve skill steadies them.\"",
            DrillKind.Pepper => "Steve: \"Keeping it alive is touch and control. Roll Shot and Bump both help.\"",
            DrillKind.Pancake => "Steve: \"The pancake is timing. Dive (Pancake) widens that window.\"",
            DrillKind.BlockSignal or DrillKind.BlockDig => "Steve: \"Reading the play is the hard part. Court Awareness is exactly that.\"",
            DrillKind.BlockOrDefend => "Steve: \"Blocking took a while to click. Block makes a bigger wall; Court Awareness reads the set.\"",
            _ => "Steve: \"No obvious weak spot today. Build what you enjoy.\"",
        };
    }
}
