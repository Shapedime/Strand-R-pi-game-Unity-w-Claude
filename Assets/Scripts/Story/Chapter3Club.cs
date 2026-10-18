using System.Collections;
using System.Collections.Generic;
using BeachVolley.Match;
using BeachVolley.Player;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Chapter 3 ("The Price") at Coastline Beach Club. Five sessions, each shaped by what the
    /// protagonist chose the evening or morning before:
    /// 1 More Than the Sand (moving passes, three-touch, keep it alive; Dave's café invitation),
    /// 2 No Days Off? (Steve reacts to the gym / normal / rest morning; Dave's five-attack challenge;
    ///   Marlon's three versions),
    /// 3 You Can't Be Everywhere (short session, then conditioning / the group / a quiet evening),
    /// 4 The Warning Signs (fatigue shows; Steve and Marlon on pushing through; how to prepare),
    /// 5 Prove It (evaluation drills and a 2v2 with Dave against Marlon and Alex; strong / average /
    ///   poor outcome; Steve's closing speech).
    /// Fatigue carries over from day to day (StoryState), so nothing here resets it.
    /// Checkpoint "club3_teaser" plays the Chapter 4 announcement instead.
    /// </summary>
    public class Chapter3Club : StoryDirector
    {
        [SerializeField] StoryActor steve, dave, marlon, alex, nina;
        [SerializeField] ScriptedPlay clubRally;
        [SerializeField] StoryDrill drill;
        [SerializeField] StoryMatch match;
        [SerializeField] BallContact contact;
        [SerializeField] GameObject car, bike;

        protected override int ChapterNumber => 3;

        static readonly Vector3 EntranceSpot = new Vector3(-7f, 0f, -14f);
        static readonly Vector3 BenchSpot = new Vector3(-5.95f, 0f, -3.74f);
        static readonly Vector3 SteveSideline = new Vector3(4.8f, 0f, -4.2f);
        static readonly Vector3 TosserLeft = new Vector3(-2.2f, 0f, -1f);
        static readonly Vector3 TosserRight = new Vector3(2.2f, 0f, -1f);
        static readonly Vector3 DaveNet = new Vector3(0.5f, 0f, -1.3f);
        static readonly Vector3 Sideline = new Vector3(5.2f, 0f, -2.6f);
        static readonly Vector3 Sideline2 = new Vector3(5.6f, 0f, -5.4f);
        static readonly Vector3 Huddle = new Vector3(0.8f, 0f, -6.5f);

        int day;
        readonly List<(string label, DrillResult result)> log = new List<(string, DrillResult)>();
        readonly List<TrainingLine> extraLines = new List<TrainingLine>();
        string nextScene = HomeScene, nextCheckpoint;
        CourtAI daveAI, marlonAI, alexAI;

        protected override IEnumerator Run()
        {
            ui.SetBlack(true);
            contact.ShowControlsHint = false;
            contact.enabled = false;
            drill.RepEnded += OnRep;
            string cp = StoryState.CheckpointName;
            if (car != null) car.SetActive(false);
            if (bike != null) bike.SetActive(false);
            alex.gameObject.SetActive(true);
            nina.gameObject.SetActive(true);
            daveAI = dave.GetComponent<CourtAI>();
            marlonAI = marlon.GetComponent<CourtAI>();
            alexAI = alex.GetComponent<CourtAI>();

            if (cp == "club3_teaser")
            {
                DayLight(6);
                yield return Teaser();
                yield break;
            }

            day = cp != null && cp.StartsWith("club3_d") && int.TryParse(cp.Substring(7), out int d) ? Mathf.Clamp(d, 1, 5) : 1;
            nextCheckpoint = "home3_d" + day;
            DayLight(day);
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
            yield return GoToScene(nextScene, nextCheckpoint);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (drill != null) drill.RepEnded -= OnRep;
        }

        // ---------- Light ----------

        void DayLight(int d)
        {
            var sun = RenderSettings.sun;
            if (sun == null)
                foreach (var l in FindObjectsByType<Light>())
                    if (l.type == LightType.Directional) { sun = l; break; }
            (Vector3 rot, Color color, float intensity, float exposure, Color fog) p = d switch
            {
                1 => (new Vector3(24f, 240f, 0f), new Color(1f, 0.84f, 0.64f), 1.3f, 1.3f, new Color(0.88f, 0.8f, 0.72f)),
                2 => (new Vector3(30f, 235f, 0f), new Color(1f, 0.9f, 0.76f), 1.4f, 1.3f, new Color(0.82f, 0.84f, 0.86f)),
                3 => (new Vector3(45f, 210f, 0f), new Color(1f, 0.95f, 0.86f), 1.45f, 1.35f, new Color(0.84f, 0.88f, 0.92f)),
                4 => (new Vector3(26f, 245f, 0f), new Color(0.95f, 0.8f, 0.68f), 1.05f, 1.15f, new Color(0.7f, 0.68f, 0.7f)),
                5 => (new Vector3(32f, 110f, 0f), new Color(1f, 0.96f, 0.9f), 1.5f, 1.35f, new Color(0.8f, 0.87f, 0.95f)),
                _ => (new Vector3(40f, 200f, 0f), new Color(1f, 0.97f, 0.9f), 1.45f, 1.35f, new Color(0.82f, 0.88f, 0.95f)),
            };
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(p.rot);
                sun.color = p.color;
                sun.intensity = p.intensity;
            }
            if (RenderSettings.skybox != null)
            {
                var sky = new Material(RenderSettings.skybox);
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", p.exposure);
                RenderSettings.skybox = sky;
            }
            RenderSettings.fogColor = p.fog;
            DynamicGI.UpdateEnvironment();
        }

        // ---------- Condition ----------

        Condition Now => Chapter3Rules.ConditionOf(skills.Fatigue);

        /// <summary>Energy bar on, and a reminder of how he feels (it never changes his skill levels).</summary>
        void ShowCondition()
        {
            ui.SetEnergy(skills.Fatigue);
            var c = Now;
            ui.Toast("Condition: " + Chapter3Rules.Name(c), c == Condition.Fresh ? new Color(0.55f, 1f, 0.55f)
                : c == Condition.Tired ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.55f, 0.4f), 2.6f);
        }

        // ---------- Drills ----------

        DrillResult lastResult;

        IEnumerator Drill(DrillKind kind, int reps, string label, System.Func<IEnumerator> onStruggle = null)
        {
            playerActor.EndActing();
            cutscene = false;
            contact.enabled = true;
            contact.ShowControlsHint = true;
            cam.Follow();
            ui.SetEnergy(skills.Fatigue);
            DrillResult res = default;
            yield return drill.Run(kind, reps, r => res = r, onStruggle);
            lastResult = res;
            if (label != null) log.Add((label, res));
            contact.enabled = false;
            contact.ShowControlsHint = false;
            cutscene = true;
            playerActor.BeginActing();
        }

        void Ready(DrillKind kind)
        {
            drill.FeederA = drill.FeederB = null;
            PlaceAt(drill.SpotFor(kind), Quaternion.identity);
            drill.Prepare(kind);
        }

        /// <summary>Fewer reps when he's tired, fewer still when exhausted (never fewer than three).</summary>
        int Reps(int normal) => Mathf.Max(3, normal - (Now == Condition.Exhausted ? 3 : Now == Condition.Tired ? 1 : 0));

        static string Verdict(DrillResult r, string great, string ok, string rough) => Grade(r.good, r.reps, great, ok, rough);

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

        IEnumerator Banter(params (StoryActor who, string line)[] lines)
        {
            foreach (var (who, line) in lines)
            {
                if (who == null) ui.Bark("You", line, 2.6f);
                else Bark(who, line, 2.6f);
                yield return new WaitForSeconds(2.4f);
            }
        }

        bool challengeOn;
        int challengeClean;

        void OnRep(DrillRep r)
        {
            ui.SetEnergy(skills.Fatigue);
            switch (r.kind)
            {
                case DrillKind.PassLR:
                    if (!r.ok && r.miss == DrillMiss.NoTouch) Bark(steve, BarkPool.Once("Feet first. You reached again.", "Don't reach — get there.", "Move, then pass. In that order."));
                    else if (r.ok && r.streak == 3) Bark(steve, BarkPool.Once("That's moving your feet.", "There. You were there early."));
                    break;
                case DrillKind.PassPressure:
                    if (r.ok && r.streak == 4) Bark(dave, BarkPool.Once("Four! The rally lives!", "Keep it alive, keep it alive…"));
                    else if (!r.ok && day == 4 && r.index > 1) Bark(marlon, BarkPool.Once("Late.", "Again. Faster.", "You saw that one coming."));
                    else if (!r.ok) Bark(steve, BarkPool.Once("Next ball is a new ball.", "Reset your feet.") );
                    break;
                case DrillKind.ThreeTouch:
                    if (challengeOn && r.ok)
                    {
                        challengeClean++;
                        Bark(dave, challengeClean switch
                        {
                            1 => BarkPool.Once("One. Don't get excited."),
                            3 => BarkPool.Once("Three?! Who taught you this?"),
                            4 => BarkPool.Once("Four. I'm sweating and I'm not even hitting."),
                            5 => BarkPool.Once("Five. Okay. Okay! I'm buying."),
                            _ => null,
                        });
                    }
                    else if (r.ok && r.successes == 1) Bark(dave, BarkPool.Once("Clean! That's one for the book.", "That's how we drew it up!"));
                    else if (!r.ok && r.miss == DrillMiss.Net) Bark(steve, BarkPool.Once("Wait for the set to drop.", "Early again — watch his hands."));
                    break;
                case DrillKind.SpikeTiming:
                    if (r.ok && r.streak == 2) Bark(dave, BarkPool.Once("Two clean! I'm a genius setter.", "Look at that contact."));
                    break;
                case DrillKind.ServeTarget:
                    if (r.ok && r.successes == 1) Bark(alex, BarkPool.Once("Same toss as last week. Good.", "In. Keep that toss."));
                    else if (!r.ok && r.miss == DrillMiss.Net) Bark(nina, BarkPool.Once("Net's winning today.", "Through the ball!"));
                    break;
                case DrillKind.ServeReceive:
                    if (r.ok && r.successes == 1) Bark(alex, BarkPool.Once("Read it.", "Good platform."));
                    else if (!r.ok) Bark(steve, BarkPool.Once("Shoulder, not ball.", "Get behind it."));
                    break;
                case DrillKind.BlockDig:
                    if (r.ok && r.streak == 2) Bark(dave, BarkPool.Once("Wall and dig! We're a system!", "That's the read."));
                    else if (!r.ok && r.miss == DrillMiss.WrongZone) Bark(steve, BarkPool.Once("Opposite of his signal.", "Check the hand, then move."));
                    break;
                case DrillKind.SetControl:
                    if (r.ok && r.successes == 2) Bark(steve, BarkPool.Once("Quiet hands. Good.", "That's the touch."));
                    break;
            }
        }

        // ---------- Day 1: More Than the Sand ----------

        IEnumerator Day1()
        {
            yield return ui.TitleCard("Day 1", "More Than the Sand", 2f);
            PlaceAt(EntranceSpot, Facing(EntranceSpot, Vector3.zero));
            Put(dave, new Vector3(-5.2f, 0f, -11.8f), EntranceSpot);
            Put(steve, new Vector3(2.5f, 0f, -6f), Vector3.zero);
            Put(alex, new Vector3(-15f, 0f, -2f), Vector3.zero);
            Put(nina, new Vector3(1.2f, 0f, -3f), Vector3.zero);
            cam.Frame(EntranceSpot + new Vector3(-1.4f, 2.1f, -1.8f), new Vector3(-5f, 1f, -11f), 48f);
            yield return FadeIn(1.4f);
            yield return Narrate("Late afternoon. The sun hangs low over Coastline Beach Club. Dave is already there, juggling a ball between his hands.");
            ShowCondition();
            dave.LookAt(player);
            motor.FaceTowards(dave.transform.position, 0.3f);
            TwoShot(player, dave.transform);
            yield return Say("Dave", "Look who finally decided to become a professional.");
            yield return Say("You", "I thought professionals usually trained more than once every few days.");
            yield return Say("Dave", "Exactly. You're already behind schedule.");
            dave.Gesture("Laugh");
            yield return Say("Dave", "Relax. Steve hasn't started yelling yet. That's usually how you know the session is going well.");

            yield return WalkTo(steve, new Vector3(-4.4f, 0f, -10.6f));
            steve.LookAt(player);
            TwoShot(player, steve.transform, -1f, 4f);
            yield return Say("Steve", "Good. You're both here. Today we're doing something different.");
            yield return Say("Dave", "Please tell me it's not another hundred passes.");
            yield return Say("Steve", "It's not a hundred passes.");
            dave.Gesture("Sigh");
            yield return Say("Steve", "It's controlled passing, movement, and a three-touch attack. And if you two keep complaining, we'll do two hundred.");
            yield return Say("Dave", "I liked the hundred better.");
            playerActor.Gesture("Laugh");
            yield return Say("Steve", "You've learned the basic movements. Now I want to see whether you can still perform them when you're moving, thinking, and getting tired.");
            steve.Gesture("Point");
            yield return Say("Steve", "And from this week, tiredness is something you'll have to manage, not just survive. It carries over. A hard day today is a heavier start tomorrow.");
            yield return FadeOut(0.6f);

            // Drill 1: moving passes.
            Ready(DrillKind.PassLR);
            Put(dave, TosserLeft, drill.SpotFor(DrillKind.PassLR));
            Put(nina, TosserRight, drill.SpotFor(DrillKind.PassLR));
            Put(steve, SteveSideline, Vector3.zero);
            drill.FeederA = dave.transform;
            drill.FeederB = nina.transform;
            TwoShot(player, steve.transform, -1f, 5f);
            yield return FadeIn(0.6f);
            yield return Say("Steve", "Balls to different areas. Get there, then pass. Bump and Court Awareness — feet and eyes.");
            yield return Drill(DrillKind.PassLR, 8, "Moving passes");
            yield return Say("Steve", Verdict(lastResult,
                "Good. You were there before the ball was.",
                "Better than last week. Some of those you reached for.",
                "Too many of those were arms only. We'll fix it."));

            // Drill 2: three-touch attack with Dave.
            yield return FadeOut(0.5f);
            Ready(DrillKind.ThreeTouch);
            Put(steve, new Vector3(0.4f, 0f, 3.6f), drill.SpotFor(DrillKind.ThreeTouch));
            Put(dave, new Vector3(0.3f, 0f, -1.9f), drill.SpotFor(DrillKind.ThreeTouch));
            Put(nina, Sideline2, Vector3.zero);
            TwoShot(player, dave.transform, 1f, 5f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Three-touch. Free ball over, you pass to Dave, Dave sets, you finish. Talk to each other.");
            yield return Drill(DrillKind.ThreeTouch, 5, "Three-touch attack");

            // Drill 3: keep the rally alive.
            yield return FadeOut(0.5f);
            Ready(DrillKind.PassPressure);
            Put(steve, new Vector3(0f, 0f, -1.3f), drill.SpotFor(DrillKind.PassPressure));
            Put(dave, TosserLeft, drill.SpotFor(DrillKind.PassPressure));
            Put(nina, TosserRight, drill.SpotFor(DrillKind.PassPressure));
            drill.FeederA = dave.transform;
            drill.FeederB = nina.transform;
            TwoShot(player, steve.transform, -1f, 5f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Last one. Harder balls, any order. Keep it playable. Recover after every touch.");
            yield return Drill(DrillKind.PassPressure, 10, "Keep the rally alive");

            // After the drills.
            yield return FadeOut(0.7f);
            drill.HideMarker();
            PlaceAt(new Vector3(-0.8f, 0f, -5.4f), Facing(new Vector3(-0.8f, 0f, -5.4f), new Vector3(1f, 0f, -4.6f)));
            Put(dave, new Vector3(0.4f, 0f, -6.2f), player.position);
            Put(steve, new Vector3(1f, 0f, -4.6f), player.position);
            cam.Frame(new Vector3(-2.6f, 1.5f, -7.6f), new Vector3(0.2f, 1.1f, -5.2f), 46f);
            yield return FadeIn(0.7f);
            playerActor.Gesture("Tired_Idle");
            yield return Narrate("He bends over, hands on his knees, catching his breath.");
            yield return Say("You", "How long have we been doing this?");
            yield return Say("Dave", "Long enough for me to reconsider every decision I've ever made.");
            yield return Say("Steve", "That's enough for today.");
            playerActor.EndPose("Tired_Idle");
            yield return Say("You", "Already?");
            steve.LookAt(player);
            OverShoulder(player, steve.transform);
            yield return Say("Steve", "You came here to improve, not to prove you can keep going forever. There's a difference.");
            yield return Say("Steve", "You did well. Your positioning is improving. But when you get tired, you start reaching for the ball instead of moving your feet.");
            yield return Say("You", "I noticed.");
            yield return Say("Steve", "Good. Now you know what to work on next time.");
            Relationship("steve", 1);

            // The invitation.
            yield return FadeOut(0.7f);
            SitPlayer(BenchSeat(-0.4f), BenchFacing);
            SitNpc(dave, BenchSeat(0.6f), BenchFacing);
            Put(steve, new Vector3(4f, 0f, -8f), Vector3.zero);
            cam.Frame(BenchSpot + new Vector3(2.4f, 1.3f, 2.6f), BenchSpot + new Vector3(0.5f, 0.9f, 0.3f), 44f);
            yield return FadeIn(0.7f);
            yield return Narrate("Dave sits at the edge of the court, knocking the sand out of his shoes. The protagonist reaches for his bag.");
            dave.LookAt(player);
            yield return Say("Dave", "Hey, wait a second.");
            yield return Say("You", "What?");
            yield return Say("Dave", "I'm getting something to eat. There's a café down the beach. You coming?");
            yield return Say("You", "I should probably get home.");
            yield return Say("Dave", "You can go home after eating. It's not like the volleyball is going to run away.");
            yield return Say("You", "Steve might have hidden another two hundred passes somewhere.");
            dave.Gesture("Happy");
            yield return Say("Dave", "Then we'll eat quickly.");
            yield return Choose("“Sure. Let's go.”   (friendship · no extra fatigue)",
                "“Not today. I need some time to myself.”   (home earlier · no penalty)");
            if (Choice == 0)
            {
                SetFlag("ch3_cafe", "yes");
                Relationship("dave", 1);
                yield return Say("Dave", "Excellent. You're paying for nothing. Yet.");
                nextScene = TownScene;
                nextCheckpoint = "town3_d1";
            }
            else
            {
                SetFlag("ch3_cafe", "no");
                Chapter3Rules.MissInvite();
                dave.Gesture("Nod");
                yield return Say("Dave", "No worries. Another time?");
                yield return Say("You", "Definitely. I'll see you at the next session.");
                yield return Say("Dave", "You'd better. I need someone else to suffer through Steve's drills with me.");
            }
            dave.Sit(false);
            yield return StandPlayer();
        }

        // ---------- Day 2: No Days Off? ----------

        IEnumerator Day2()
        {
            string route = StoryState.Flag("ch3_d2", "normal");
            yield return ui.TitleCard("Day 2", "No Days Off?", 2f);
            PlaceAt(EntranceSpot, Facing(EntranceSpot, Vector3.zero));
            Put(dave, new Vector3(-5.6f, 0f, -12.2f), EntranceSpot);
            Put(steve, new Vector3(-2.4f, 0f, -7.4f), Vector3.zero);
            Put(marlon, new Vector3(0.5f, 0f, -8.6f), Vector3.zero);
            Put(alex, new Vector3(-1.8f, 0f, 5f), Vector3.zero);
            Put(nina, Sideline2, Vector3.zero);
            cam.Frame(new Vector3(-6.8f, 1.8f, -15.6f), new Vector3(-1f, 1.1f, -8.6f), 46f);
            yield return FadeIn(1f);
            marlon.Volley(HitType.Spike);
            yield return Narrate("Marlon is already on the court, practising serves alone. Same toss, same swing, again and again.");
            ShowCondition();

            if (route == "gym")
            {
                dave.LookAt(player);
                TwoShot(player, dave.transform);
                yield return Say("Dave", "You look tired.");
                yield return Say("You", "Good to see you too.");
                yield return Say("Dave", "I'm serious. Did you sleep badly?");
                yield return Say("You", "Went to the gym this morning.");
                yield return Say("Dave", "The gym? Before training?");
                yield return Say("You", "Marlon's doing extra work. I figured I should, too.");
                yield return Say("Dave", "Did Marlon tell you to do that?");
                yield return Say("You", "No.");
                yield return Say("Dave", "Then why are you letting him write your schedule?");
                yield return WalkTo(steve, new Vector3(-4.2f, 0f, -11.4f));
                steve.LookAt(player);
                TwoShot(player, steve.transform, -1f, 4f);
                yield return Say("Steve", "I heard you went to the gym this morning.");
                yield return Say("You", "Yeah. I wanted to get stronger.");
                yield return Say("Steve", "That's a good goal. What did you work on?");
                yield return Say("You", "Squats, some curls, power cleans.");
                steve.Gesture("Nod");
                yield return Say("Steve", "All useful things. But how are you feeling right now?");
                yield return Say("You", "A little tired. Nothing serious.");
                yield return Narrate("Steve studies his posture for a moment.");
                yield return Say("Steve", "We'll adjust today's session. I want to see how you're moving before we increase the intensity.");
                yield return Say("You", "But I came here to train.");
                yield return Say("Steve", "And you will. Just not every session needs to be your hardest one.");
                yield return Say("Steve", "Remember, the goal isn't to finish every day completely exhausted. The goal is to be a better player next week than you are today.");
                Relationship("steve", 1);
            }
            else
            {
                yield return WalkTo(steve, new Vector3(-4.6f, 0f, -11.6f));
                steve.LookAt(player);
                TwoShot(player, steve.transform, -1f, 4f);
                yield return Say("Steve", "He's been here since early morning.");
                yield return Say("You", "Should I be doing that too?");
                yield return Say("Steve", "Why?");
                yield return Say("You", "Because he's getting better. I don't want to fall behind.");
                steve.Gesture("ArmsCrossed");
                yield return Say("Steve", "Then focus on getting better. Don't confuse copying someone's schedule with understanding what you need.");
                steve.EndPose("ArmsCrossed");
                yield return Say("Steve", route == "rest"
                    ? "You look fresh, by the way. That's not an accident. Show me what you learned yesterday."
                    : "You came in rested and on time. You're building consistency instead of chasing Marlon's routine. Show me what you learned yesterday.");
            }
            yield return FadeOut(0.6f);

            // Drill 1: serves (shorter after the gym).
            bool reduced = route == "gym";
            Ready(DrillKind.ServeTarget);
            Put(alex, new Vector3(-1.8f, 0f, 5f), new Vector3(0f, 0f, -8f));
            Put(nina, new Vector3(1.8f, 0f, 5f), new Vector3(0f, 0f, -8f));
            Put(steve, new Vector3(3.6f, 0f, -8.2f), drill.SpotFor(DrillKind.ServeTarget));
            Put(dave, new Vector3(-3.8f, 0f, -8.6f), drill.SpotFor(DrillKind.ServeTarget));
            clubRally.ResetPositions(); // Marlon back to his own court (his substitute leaves)
            drill.FeederA = alex.transform;
            drill.FeederB = nina.transform;
            TwoShot(player, steve.transform, -1f, 4.4f);
            yield return FadeIn(0.6f);
            yield return Say("Steve", reduced ? "Serves. Five, not ten. Quality over quantity today." : "Serves first. Same toss every time.");
            yield return Drill(DrillKind.ServeTarget, reduced ? 5 : Reps(7), "Serves");

            // Drill 2: the three-touch attack — Dave's challenge, if they made it at the café.
            yield return FadeOut(0.5f);
            Ready(DrillKind.ThreeTouch);
            Put(steve, new Vector3(0.4f, 0f, 3.6f), drill.SpotFor(DrillKind.ThreeTouch));
            Put(dave, new Vector3(0.3f, 0f, -1.9f), drill.SpotFor(DrillKind.ThreeTouch));
            Put(alex, Sideline, Vector3.zero);
            Put(nina, Sideline2, Vector3.zero);
            TwoShot(player, dave.transform, 1f, 4.4f);
            yield return FadeIn(0.5f);
            bool challenge = StoryState.Is("ch3_challenge", "set");
            if (challenge)
            {
                dave.LookAt(player);
                yield return Say("Dave", "So. Five clean attacks. Seven tries each. I already went while you were serving.");
                yield return Say("You", "And?");
                yield return Say("Dave", "Four. Which is excellent, and also not five. Your turn.");
                yield return Say("Steve", "What are you two talking about?");
                yield return Say("Dave", "Professional development.");
                challengeOn = true;
                challengeClean = 0;
                yield return Drill(DrillKind.ThreeTouch, 7, "Three-touch (five-attack challenge)");
                challengeOn = false;
                int clean = lastResult.good;
                string outcome = clean >= 5 ? "won" : clean == 4 ? "tie" : "lost";
                SetFlag("ch3_challenge", outcome);
                TwoShot(player, dave.transform, 1f, 3.4f);
                if (outcome == "won")
                {
                    dave.Gesture("Shrug");
                    yield return Say("Dave", $"{clean}. Fine. I owe you a drink. I'll pay it in installments.");
                }
                else if (outcome == "tie")
                {
                    yield return Say("Dave", "Four each. Nobody buys. That's the worst possible result for my wallet and my ego at the same time.");
                }
                else
                {
                    dave.Gesture("FistPump");
                    yield return Say("Dave", $"{clean}! I win! Steve, write it down!");
                    yield return Say("Steve", "I'm not writing that down.");
                }
            }
            else
            {
                yield return Say("Steve", "Three-touch again. Controlled attacks — I'd rather see it land in than see it fly.");
                yield return Drill(DrillKind.ThreeTouch, reduced ? 4 : Reps(6), "Three-touch attack");
            }
            if (route == "gym")
            {
                yield return Narrate("His legs feel heavier on every approach. The timing is still his — the spring isn't.");
                yield return Say("Steve", "Reset. Don't chase the last mistake into the next ball.");
                yield return Say("Dave", "One ball at a time. You've got this.");
            }

            // Drill 3: serve and receive.
            yield return FadeOut(0.5f);
            Ready(DrillKind.ServeReceive);
            Put(alex, new Vector3(0.5f, 0f, 8.7f), Vector3.zero);
            Put(nina, Sideline2, Vector3.zero);
            Put(steve, SteveSideline, Vector3.zero);
            Put(dave, Sideline, Vector3.zero);
            drill.FeederA = alex.transform;
            cam.Frame(new Vector3(-2.8f, 1.8f, -8f), alex.transform.position + Vector3.up * 1.5f, 40f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", reduced ? "Short serve-and-receive, then we stop. Longer breaks today." : "Serve receive. Movement after the touch, too.");
            yield return Drill(DrillKind.ServeReceive, reduced ? 4 : Reps(6), "Serve receive");

            // Marlon after practice.
            yield return FadeOut(0.7f);
            drill.HideMarker();
            PlaceAt(BenchSpot + new Vector3(0.6f, 0f, -1.2f), Facing(BenchSpot, new Vector3(0f, 0f, -4f)));
            Put(marlon, BenchSpot + new Vector3(2.2f, 0f, -0.6f), player.position);
            Put(dave, BenchSpot + new Vector3(-1.4f, 0f, -2.6f), player.position);
            cam.Frame(BenchSpot + new Vector3(-0.8f, 1.6f, -3.6f), BenchSpot + new Vector3(1.4f, 1.2f, -0.8f), 44f);
            yield return FadeIn(0.7f);
            playerActor.Gesture("PickUp");
            yield return Narrate("He's packing his bag when Marlon walks over.");
            marlon.LookAt(player);
            OverShoulder(player, marlon.transform);
            switch (route)
            {
                case "gym":
                    yield return Say("Marlon", "Heard you went to the gym this morning.");
                    yield return Say("You", "Word travels fast.");
                    yield return Say("Marlon", "So, you finally started taking this seriously.");
                    yield return Say("You", "I've been taking it seriously.");
                    yield return Say("Marlon", "Then keep going. How was it?");
                    yield return Say("You", "Harder than I expected. Steve had to reduce today's session because I was tired.");
                    yield return Say("Marlon", "Then you need to get used to it.");
                    yield return Say("You", "Steve said training harder doesn't always mean training better.");
                    yield return Say("Marlon", "Steve isn't wrong. But if you always stop when you're uncomfortable, you'll never find out what you're capable of.");
                    yield return Say("You", "And if I keep going when I'm exhausted?");
                    yield return Say("Marlon", "Then you learn where your limits are.");
                    yield return Say("You", "Sounds like a pretty good way to make mistakes.");
                    marlon.Gesture("Shrug");
                    yield return Say("Marlon", "Maybe. But I'd rather find my limits than spend my career wondering where they were.");
                    SetFlag("marlon_limits", "heard");
                    break;
                case "rest":
                    yield return Say("Marlon", "Taking it easy?");
                    yield return Say("You", "Making sure I can train properly tomorrow.");
                    yield return Say("Marlon", "We'll see if it pays off.");
                    break;
                default:
                    yield return Say("Marlon", "No gym today?");
                    yield return Say("You", "I had other priorities.");
                    yield return Say("Marlon", "Just make sure you know what you're sacrificing.");
                    break;
            }
            yield return Say("Marlon", "See you next session.");
            marlon.WalkTo(new Vector3(9f, 0f, -9f));
            yield return Wait(0.8f);
            WalkToNoWait(dave, BenchSpot + new Vector3(-0.4f, 0f, -1.8f));
            yield return Wait(1.2f);
            dave.LookAt(player);
            TwoShot(player, dave.transform, 1f, 3.2f);
            yield return Say("Dave", "For what it's worth, I think Steve has a point.");
            yield return Say("You", "You think Marlon's wrong?");
            yield return Say("Dave", "I think Marlon's trying to get somewhere. I just don't think his way has to be your way.");
            yield return Say("You", "I just don't want to fall behind.");
            yield return Say("Dave", "Then keep showing up. Learn from today. That's how you get ahead.");
        }

        static void WalkToNoWait(StoryActor npc, Vector3 to) => npc.WalkTo(to);

        // ---------- Day 3: You Can't Be Everywhere ----------

        IEnumerator Day3()
        {
            yield return ui.TitleCard("Day 3", "You Can't Be Everywhere", 2f);
            Ready(DrillKind.PassLR);
            Put(dave, TosserLeft, drill.SpotFor(DrillKind.PassLR));
            Put(nina, TosserRight, drill.SpotFor(DrillKind.PassLR));
            Put(steve, SteveSideline, Vector3.zero);
            Put(alex, new Vector3(-15f, 0f, -2f), Vector3.zero);
            drill.FeederA = dave.transform;
            drill.FeederB = nina.transform;
            TwoShot(player, steve.transform, -1f, 5f);
            yield return FadeIn(0.8f);
            yield return Narrate("A short session today. Steve calls it \"maintenance\". Dave calls it \"suspicious\".");
            ShowCondition();
            yield return Say("Steve", "Movement first, then attacking. Short and sharp. Then you're free.");
            yield return Drill(DrillKind.PassLR, Reps(6), "Movement passing");

            yield return FadeOut(0.5f);
            Ready(DrillKind.SpikeTiming);
            Put(dave, DaveNet, drill.SpotFor(DrillKind.SpikeTiming));
            Put(nina, Sideline2, Vector3.zero);
            OverShoulder(player, dave.transform);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Dave sets, you hit. Contact first, then everything else.");
            yield return Drill(DrillKind.SpikeTiming, Reps(5), "Attack timing");

            // The choice, at the bench.
            yield return FadeOut(0.7f);
            drill.HideMarker();
            SitPlayer(BenchSeat(-0.4f), BenchFacing);
            Put(dave, BenchSpot + new Vector3(1.2f, 0f, 1.6f), BenchSpot);
            cam.Frame(BenchSpot + new Vector3(2.4f, 1.3f, 2.6f), BenchSpot + new Vector3(0.5f, 0.9f, 0.3f), 44f);
            yield return FadeIn(0.7f);
            ui.SetEnergy(skills.Fatigue);
            dave.LookAt(player);
            yield return Say("Dave", "So — did you see my message? A few of us are grabbing food later. Alex is coming. Nina's coming if Alex is coming.");
            yield return Think("Volleyball, gym, friends… I can't fit everything into one day.");
            float f = skills.Fatigue;
            yield return Choose(
                $"Extra conditioning at the gym   (gym points · {Chapter3Rules.Preview(f, Chapter3Rules.GymConditioning)} · miss the evening)",
                $"Join Dave and the others   (friendship · {Chapter3Rules.Preview(f, 0f)})",
                $"Take a quiet evening   (recover · {Chapter3Rules.Preview(f, -Chapter3Rules.RestEvening)})");
            switch (Choice)
            {
                case 0:
                    SetFlag("ch3_d3", "gym");
                    Chapter3Rules.MissInvite();
                    yield return Say("You", "I can't tonight. I want to get a conditioning session in.");
                    dave.Gesture("Shrug");
                    yield return Say("Dave", "All right. Just don't forget about us when you become famous.");
                    nextScene = TownScene;
                    nextCheckpoint = "town3_d3";
                    break;
                case 1:
                    SetFlag("ch3_d3", "group");
                    Relationship("dave", 1);
                    yield return Say("You", "Yeah. I'm in.");
                    dave.Gesture("FistPump");
                    yield return Say("Dave", "Yes! Alex owes me five. He said you'd pick the gym.");
                    nextScene = TownScene;
                    nextCheckpoint = "town3_d3";
                    break;
                default:
                    SetFlag("ch3_d3", "rest");
                    Chapter3Rules.MissInvite();
                    yield return Say("You", "I think I need a quiet night. My legs are filing complaints.");
                    yield return Say("Dave", "Honestly, that sounds like a good idea. We'll catch up soon.");
                    break;
            }
            yield return StandPlayer();
        }

        // ---------- Day 4: The Warning Signs ----------

        bool outburst;

        IEnumerator Day4()
        {
            yield return ui.TitleCard("Day 4", "The Warning Signs", 2f);
            Ready(DrillKind.PassPressure);
            Put(steve, new Vector3(0f, 0f, -1.3f), drill.SpotFor(DrillKind.PassPressure));
            Put(marlon, TosserLeft, drill.SpotFor(DrillKind.PassPressure));
            Put(dave, TosserRight, drill.SpotFor(DrillKind.PassPressure));
            Put(alex, Sideline, Vector3.zero);
            Put(nina, Sideline2, Vector3.zero);
            drill.FeederA = marlon.transform;
            drill.FeederB = dave.transform;
            TwoShot(player, steve.transform, -1f, 5f);
            yield return FadeIn(0.8f);
            yield return Narrate("A demanding session. Marlon is feeding today, and he doesn't do gentle.");
            ShowCondition();
            yield return Say("Steve", "Defensive pressure. Three feeders, no pattern. Recover after every touch.");
            outburst = false;
            yield return Drill(DrillKind.PassPressure, 10, "Defensive pressure", Outburst);

            bool cut = Now == Condition.Exhausted;
            if (!cut)
            {
                yield return FadeOut(0.5f);
                Ready(DrillKind.BlockDig);
                Put(dave, new Vector3(0.3f, 0f, -0.7f), new Vector3(0f, 0f, 5f));
                Put(alex, new Vector3(2.6f, 0f, 1.3f), new Vector3(0f, 0f, -5f));
                Put(nina, new Vector3(-0.4f, 0f, 2.4f), new Vector3(2.6f, 0f, 1.3f));
                Put(marlon, Sideline, Vector3.zero);
                Put(steve, SteveSideline, Vector3.zero);
                drill.FeederA = alex.transform;
                drill.FeederB = nina.transform;
                TwoShot(player, dave.transform, 1f, 5f);
                yield return FadeIn(0.5f);
                yield return Say("Steve", "Block and dig. Read Dave's hand, then go.");
                yield return Drill(DrillKind.BlockDig, 6, "Block and dig", Outburst);
            }

            // Steve stops it.
            yield return FadeOut(0.5f);
            drill.HideMarker();
            PlaceAt(new Vector3(-0.6f, 0f, -5.2f), Facing(new Vector3(-0.6f, 0f, -5.2f), new Vector3(1.2f, 0f, -4.4f)));
            Put(steve, new Vector3(1.2f, 0f, -4.4f), player.position);
            Put(marlon, new Vector3(0.9f, 0f, -6.4f), player.position);
            Put(dave, new Vector3(-1.8f, 0f, -6.6f), player.position);
            Put(alex, Sideline, Vector3.zero);
            Put(nina, Sideline2, Vector3.zero);
            cam.Frame(new Vector3(-2.8f, 1.6f, -8f), new Vector3(0.4f, 1.2f, -5.2f), 46f);
            yield return FadeIn(0.5f);
            if (!outburst)
            {
                yield return Narrate("A ball drops into the sand a step away from him. He was late. Again.");
                playerActor.Gesture("Angry");
                yield return Say("You", "Come on! I should've had that.");
                yield return Say("Marlon", "You were late. You need to move sooner.");
                yield return Say("You", "I know!");
            }
            steve.Gesture("Clap");
            yield return Say("Steve", "Enough. Over here, both of you.");
            steve.LookAt(player);
            OverShoulder(player, steve.transform);
            yield return Say("Steve", "What happened on that ball?");
            yield return Say("You", "I reacted too late.");
            yield return Say("Steve", "And why?");
            yield return Narrate("He hesitates.");
            yield return Say("You", "I'm tired. I've been trying to fit too much in.");
            marlon.Gesture("Dismiss");
            yield return Say("Marlon", "Then get used to it. Everyone gets tired.");
            steve.LookAt(marlon.transform);
            yield return Say("Steve", "Everyone gets tired. That doesn't mean every kind of tiredness should be ignored.");
            yield return Say("Marlon", "You can't take a break every time something gets difficult.");
            yield return Say("Steve", "I didn't say that. I said learn the difference between discomfort and a warning sign.");
            yield return Narrate("Silence settles over the court.");
            steve.LookAt(player);
            yield return Say("Steve", "How have you been sleeping?");
            yield return Say("You", StoryState.Is("ch3_d3", "rest") ? "Better than last week. Still not great." : "Not great. I've been trying to fit more in.");
            yield return Say("Steve", "We train to improve, not to collect hours. If your technique falls apart because you're exhausted, adding more work isn't necessarily helping.");
            if (cut) yield return Say("Steve", "And I already cut the block drill. You're running on empty — I'm not adding to it.");
            marlon.Gesture("LookAway");
            yield return Narrate("Marlon looks unconvinced, but doesn't argue further.");

            // A lighter technical session.
            yield return FadeOut(0.5f);
            Ready(DrillKind.SetControl);
            Put(steve, new Vector3(2.3f, 0f, -4.1f), drill.SpotFor(DrillKind.SetControl));
            Put(dave, Sideline, Vector3.zero);
            clubRally.ResetPositions(); // Marlon back to his own court (his substitute leaves)
            OverShoulder(steve.transform, player, -1f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Hands only. Slow and clean. No running.");
            yield return Drill(DrillKind.SetControl, 5, "Light technical work");

            // How to prepare for tomorrow.
            yield return FadeOut(0.6f);
            drill.HideMarker();
            SitPlayer(BenchSeat(-0.4f), BenchFacing);
            Put(steve, BenchSpot + new Vector3(1.4f, 0f, 2f), BenchSpot);
            cam.Frame(BenchSpot + new Vector3(2.4f, 1.3f, 2.6f), BenchSpot + new Vector3(0.5f, 0.9f, 0.3f), 44f);
            yield return FadeIn(0.6f);
            steve.LookAt(player);
            yield return Say("Steve", "Tomorrow I'm running an evaluation. Nobody gets cut. But I want to see you at your best — not your most tired.");
            ui.SetEnergy(skills.Fatigue);
            yield return Think("How do I get ready for tomorrow?");
            float f = skills.Fatigue;
            yield return Choose(
                $"Prioritize recovery   (best recovery · {Chapter3Rules.Preview(f, -Chapter3Rules.RestEvening)})",
                $"Study professional matches   (tactics · {Chapter3Rules.Preview(f, -Chapter3Rules.StudyEvening)})",
                $"Do extra practice anyway   (small practice reward · {Chapter3Rules.Preview(f, Chapter3Rules.ExtraPractice)})");
            switch (Choice)
            {
                case 0:
                    SetFlag("ch3_d4", "rest");
                    yield return Say("Steve", "Good. Eat, sleep. That's training too.");
                    break;
                case 1:
                    SetFlag("ch3_d4", "study");
                    yield return Say("Steve", "Watch the players without the ball. That's where the game is.");
                    break;
                default:
                    SetFlag("ch3_d4", "practice");
                    steve.Gesture("HeadShake");
                    yield return Say("Steve", "I'd rather you didn't. I'm not angry that you want it — I'm worried about how you're going about it.");
                    yield return Say("You", "I'll keep it short.");
                    yield return Say("Steve", "Shorter than you think.");
                    break;
            }
            yield return StandPlayer();
        }

        /// <summary>Day 4: after three misses in a row, the frustration boils over (the drill keeps running).</summary>
        IEnumerator Outburst()
        {
            if (outburst) yield break;
            outburst = true;
            yield return Banter(
                (null, "Come on! I should've had that."),
                (marlon, "You were late. You need to move sooner."),
                (null, "I know!"));
        }

        // ---------- Day 5: Prove It ----------

        int savesSeen;

        IEnumerator Day5()
        {
            yield return ui.TitleCard("Day 5", "Prove It", 2f);
            clubRally.StopLoop();
            PlaceAt(Huddle + new Vector3(-0.9f, 0f, 0f), Facing(Huddle, new Vector3(0.8f, 0f, -3.8f)));
            Put(steve, new Vector3(0.8f, 0f, -3.8f), Huddle);
            Put(dave, Huddle + new Vector3(0.2f, 0f, 0.4f), steve.transform.position);
            Put(alex, Huddle + new Vector3(1.2f, 0f, 0.2f), steve.transform.position);
            Put(marlon, Huddle + new Vector3(2.3f, 0f, -0.2f), steve.transform.position);
            Put(nina, Sideline2, Vector3.zero);
            cam.Frame(new Vector3(0.6f, 2.2f, -1.4f), Huddle + Vector3.up * 1.2f, 50f);
            yield return FadeIn(0.9f);
            yield return Narrate("Morning. Coastline Beach Club. Steve has everyone gathered before the sand is even warm.");
            ShowCondition();
            yield return Say("Steve", "Today, we're checking what you've learned. This isn't a tournament, and nobody is getting cut.");
            yield return Say("Dave", "Good. I haven't prepared a victory speech.");
            yield return Say("Alex", "You prepared one yesterday.");
            yield return Say("Dave", "That was a draft.");
            steve.Gesture("Happy");
            yield return Say("Steve", "We'll start with controlled passing, move into attack combinations, and finish with a 2v2 game. I want to see good decisions, communication, and how you respond to mistakes.");
            marlon.Volley(HitType.Set);
            yield return Say("Marlon", "So, basically, everything.");
            yield return Say("Steve", "That's usually how volleyball works.");
            yield return FadeOut(0.6f);

            // Evaluation drills.
            int drillReps = 0, drillGood = 0;
            Ready(DrillKind.ServeReceive);
            Put(alex, new Vector3(0.5f, 0f, 8.7f), Vector3.zero);
            Put(steve, SteveSideline, Vector3.zero);
            Put(dave, Sideline, Vector3.zero);
            Put(marlon, Sideline2 + new Vector3(0.8f, 0f, 0f), Vector3.zero);
            drill.FeederA = alex.transform;
            cam.Frame(new Vector3(-2.8f, 1.8f, -8f), alex.transform.position + Vector3.up * 1.5f, 40f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "First touch. Control, not power.");
            yield return Drill(DrillKind.ServeReceive, 5, "Evaluation · first touch");
            drillReps += lastResult.reps; drillGood += lastResult.good;

            yield return FadeOut(0.5f);
            Ready(DrillKind.ThreeTouch);
            Put(steve, new Vector3(0.4f, 0f, 3.6f), drill.SpotFor(DrillKind.ThreeTouch));
            Put(dave, new Vector3(0.3f, 0f, -1.9f), drill.SpotFor(DrillKind.ThreeTouch));
            TwoShot(player, dave.transform, 1f, 5f);
            yield return FadeIn(0.5f);
            yield return Say("Steve", "Set and attack. Together.");
            yield return Drill(DrillKind.ThreeTouch, 5, "Evaluation · attack combination");
            drillReps += lastResult.reps; drillGood += lastResult.good;

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
            yield return Say("Steve", "Defensive positioning and recovery. Read, move, dig.");
            yield return Drill(DrillKind.BlockDig, 5, "Evaluation · defence");
            drillReps += lastResult.reps; drillGood += lastResult.good;
            float drillRatio = drillReps > 0 ? drillGood / (float)drillReps : 0f;

            // The 2v2.
            yield return FadeOut(0.6f);
            drill.HideMarker();
            PlaceAt(Huddle + new Vector3(-0.9f, 0f, 0f), Facing(Huddle, new Vector3(0.8f, 0f, -3.8f)));
            Put(steve, new Vector3(0.8f, 0f, -3.8f), Huddle);
            Put(dave, Huddle + new Vector3(0.3f, 0f, 0.4f), player.position);
            Put(alex, Huddle + new Vector3(1.4f, 0f, 0.2f), steve.transform.position);
            Put(marlon, Huddle + new Vector3(2.4f, 0f, -0.3f), steve.transform.position);
            cam.Frame(new Vector3(-0.4f, 1.8f, -4.2f), Huddle + Vector3.up * 1.3f, 46f);
            yield return FadeIn(0.6f);
            yield return Say("Steve", "Last part. You and Dave, against Marlon and Alex. First to ten.");
            dave.LookAt(player);
            TwoShot(player, dave.transform, 1f, 3f);
            string ch = StoryState.Flag("ch3_challenge");
            yield return Say("Dave", ch == "won" ? "All right. Remember our five clean attacks? I'd like a rematch. Mid-game, if possible."
                : ch == "lost" || ch == "tie" ? "All right. Remember our five clean attacks? I'm still undefeated, technically."
                : "All right. Remember the café challenge? We never got to it. Consider this round one.");
            yield return Say("You", "This isn't the café challenge.");
            yield return Say("Dave", "Everything is the café challenge if you believe in yourself.");
            yield return Say("Alex", "Can we start before he says something else?");
            yield return FadeOut(0.6f);

            Put(steve, new Vector3(5.4f, 0f, -0.4f), Vector3.zero);
            match.Setup("YOU & DAVE", daveAI, "MARLON & ALEX", marlonAI, alexAI, farBias: 0.85f);
            match.BetweenPoints = null;
            match.PointScored += EvalBark;
            savesSeen = 0;
            yield return PlayMatch();
            match.PointScored -= EvalBark;
            var g = match.Stats;
            extraLines.Add(new TrainingLine("Evaluation game", $"{g.nearPoints} : {g.farPoints}  {(g.NearWon ? "won" : "lost")}", (g.NearWon ? 1 : 0) + (g.PlayedWell ? 1 : 0)));

            var perf = Chapter3Rules.Evaluate(drillRatio, g, skills.Fatigue);
            string band = perf == Performance.Strong ? "strong" : perf == Performance.Average ? "average" : "poor";
            SetFlag("ch3_eval", band);
            SetFlag("ch3_eval_condition", Chapter3Rules.Name(Now).ToLowerInvariant());

            // Outcome.
            yield return FadeOut(0.6f);
            PlaceAt(new Vector3(-2.4f, 0f, -9.2f), Facing(new Vector3(-2.4f, 0f, -9.2f), new Vector3(4f, 0f, -9f)));
            Put(dave, new Vector3(-1.4f, 0f, -8.4f), new Vector3(-2.4f, 0f, -9.2f));
            Put(marlon, new Vector3(0.4f, 0f, -9.6f), new Vector3(-2.4f, 0f, -9.2f));
            Put(alex, new Vector3(2.6f, 0f, -8.6f), Vector3.zero);
            Put(steve, new Vector3(4.4f, 0f, -6f), Vector3.zero);
            cam.Frame(new Vector3(-4.8f, 1.7f, -10.8f), new Vector3(-0.6f, 1.2f, -8.8f), 44f);
            yield return FadeIn(0.6f);
            dave.LookAt(player);
            switch (perf)
            {
                case Performance.Strong:
                    yield return Narrate("He read one attack early, got under it, and Dave turned the dig into a clean three-touch point. It felt like the whole week in one rally.");
                    dave.Gesture("FistPump");
                    yield return Say("Dave", "That's it! We've been working on that!");
                    marlon.LookAt(player);
                    OverShoulder(player, marlon.transform);
                    yield return Say("Marlon", "Your positioning was better today.");
                    yield return Say("You", "Thanks.");
                    yield return Say("Marlon", "Don't get comfortable. Next time, I'm going harder.");
                    yield return Say("You", "Good. I'll be ready.");
                    yield return Narrate("Marlon gives a small, approving smile. It's gone almost before it's there.");
                    Relationship("marlon", 1);
                    extraLines.Add(new TrainingLine("Performance bonus", "strong evaluation", 2));
                    break;
                case Performance.Average:
                    yield return Narrate("A few good plays. A few he'd like back. After one mistake, Dave was already talking him into the next ball, and they strung together some real rallies.");
                    yield return Say("Dave", "That was better than last time.");
                    yield return Say("You", "I still messed up a lot.");
                    yield return Say("Dave", "Yeah. And then you got the next ball. That's what matters.");
                    marlon.LookAt(player);
                    OverShoulder(player, marlon.transform);
                    yield return Say("Marlon", "You're learning. Now you need to make it consistent.");
                    break;
                default:
                    yield return Narrate("Mistimed swings. Slow first steps. Points he would normally have saved, dropping into the sand.");
                    yield return Say("Dave", Chapter3Rules.DaveBand() == Band.Low ? "Next ball. Come on." : "Forget that point. Next ball.");
                    yield return Say("You", "I'm making too many mistakes.");
                    yield return Say("Dave", "Then we'll work on them. We're not done yet.");
                    // Steve, privately.
                    yield return FadeOut(0.5f);
                    Put(dave, new Vector3(-5f, 0f, -12f), Vector3.zero);
                    Put(marlon, new Vector3(9f, 0f, -9f), Vector3.zero);
                    Put(steve, new Vector3(-1.2f, 0f, -8.8f), player.position);
                    TwoShot(player, steve.transform, -1f, 3.4f);
                    yield return FadeIn(0.5f);
                    steve.LookAt(player);
                    yield return Say("Steve", "That was a difficult session.");
                    yield return Say("You", "I know.");
                    yield return Say("Steve", Now == Condition.Fresh
                        ? "But I saw what happened. You started rushing your decisions after the first few points. We'll work on that."
                        : "But I saw what happened. You were slower to react, and you started rushing your decisions. We'll work on those things.");
                    yield return Say("You", "So I haven't improved?");
                    yield return Say("Steve", "That's not what I said. One session doesn't define your progress. We learn from this and move forward.");
                    Put(marlon, new Vector3(0.4f, 0f, -10.2f), player.position);
                    marlon.LookAt(player);
                    yield return Say("Marlon", "You need to get more consistent.");
                    yield return Say("You", "I know. I'll work on it.");
                    yield return Narrate("No congratulations. No mockery, either.");
                    break;
            }

            // Steve's closing speech, for everyone.
            yield return FadeOut(0.6f);
            PlaceAt(Huddle + new Vector3(-0.9f, 0f, 0f), Facing(Huddle, new Vector3(0.8f, 0f, -3.8f)));
            Put(steve, new Vector3(0.8f, 0f, -3.8f), Huddle);
            Put(dave, Huddle + new Vector3(0.2f, 0f, 0.4f), steve.transform.position);
            Put(alex, Huddle + new Vector3(1.2f, 0f, 0.2f), steve.transform.position);
            Put(marlon, Huddle + new Vector3(2.4f, 0f, -0.3f), steve.transform.position);
            Put(nina, Huddle + new Vector3(-1.6f, 0f, 0.6f), steve.transform.position);
            cam.Frame(new Vector3(0.6f, 2.2f, -1.4f), Huddle + Vector3.up * 1.2f, 50f);
            yield return FadeIn(0.6f);
            yield return Narrate("Steve gathers everyone near the net.");
            yield return Say("Steve", "You've all improved, but you're still learning. What matters now is whether you can take what you learn here and apply it when things get difficult.");
            steve.LookAt(player);
            yield return Say("Steve", "And remember: your job isn't to train the most hours. It's to become the best player you can become.");
            playerActor.Gesture("Nod");
            yield return Narrate("He nods.");
            Relationship("steve", 1);
        }

        void EvalBark(MatchPoint mp)
        {
            var s = match.Stats;
            if (s.humanSaves > savesSeen)
            {
                savesSeen = s.humanSaves;
                Bark(dave, BarkPool.Once("Nice read!", "Great dig!", "That's the read!"));
                return;
            }
            if (mp.index == 2)
            {
                StartCoroutine(Banter((alex, "I'm covering line!"), (marlon, "Then I'll take cross!")));
                return;
            }
            if (!mp.nearWon && mp.humanLastTouch)
                Bark(dave, Chapter3Rules.DaveBand() == Band.Low
                    ? BarkPool.Once("Next one.", "Shake it off.")
                    : BarkPool.Once("Forget that point. Next ball.", "That's on both of us. Next one.", "Breathe. We've got this."));
            else if (mp.nearWon && mp.reason == PointReason.Kill && mp.humanLastTouch)
                Bark(dave, BarkPool.Once("YES! That's the one!", "Clean!", "Did you see that, Marlon?"));
            else if (!mp.nearWon && mp.reason == PointReason.Kill && mp.lastAi == marlonAI)
                Bark(marlon, BarkPool.Once("Keep up.", "Too slow.", "Again."));
        }

        IEnumerator PlayMatch()
        {
            ui.SetBlack(true);
            StartCoroutine(FadeIn(0.6f));
            playerActor.EndActing();
            cutscene = false;
            contact.enabled = true;
            cam.Follow();
            ui.SetEnergy(skills.Fatigue);
            yield return match.Play();
            contact.enabled = false;
            contact.ShowControlsHint = false;
            cutscene = true;
            playerActor.BeginActing();
        }

        // ---------- Chapter 4 teaser ----------

        IEnumerator Teaser()
        {
            yield return ui.TitleCard("A few days later", "Coastline Beach Club", 2.2f);
            PlaceAt(Huddle + new Vector3(-0.9f, 0f, 0f), Facing(Huddle, new Vector3(0.8f, 0f, -3.8f)));
            Put(steve, new Vector3(0.8f, 0f, -3.8f), Huddle);
            Put(dave, Huddle + new Vector3(0.2f, 0f, 0.4f), steve.transform.position);
            Put(alex, Huddle + new Vector3(1.2f, 0f, 0.2f), steve.transform.position);
            Put(marlon, Huddle + new Vector3(2.3f, 0f, -0.2f), steve.transform.position);
            Put(nina, Huddle + new Vector3(-1.6f, 0f, 0.6f), steve.transform.position);
            cam.Frame(new Vector3(0.6f, 2.2f, -1.4f), Huddle + Vector3.up * 1.2f, 50f);
            yield return FadeIn(1f);
            yield return Narrate("Steve gathers the players before practice. He's holding a printed sheet, which is new.");
            yield return Say("Steve", "I've entered us in a local 2v2 tournament.");
            dave.LookAt(steve.transform);
            yield return Say("Dave", "Wait. An actual tournament?");
            yield return Say("Steve", "An actual tournament.");
            marlon.Gesture("Point");
            yield return Say("Marlon", "Who's in it? How many teams? What did they do last season? Is the Harbor Club pair entered?");
            yield return Say("Steve", "Marlon. Breathe.");
            cam.Frame(new Vector3(-1.6f, 1.6f, -9.4f), new Vector3(0f, 1.4f, 2f), 44f);
            yield return Narrate("He looks toward the court. For the first time, practice is about to lead to a competition where the opponents won't be familiar faces.");
            yield return Think("I've learned how to train. I've learned how to work with a partner. Now I need to find out what happens when it really counts.");
            yield return FadeOut(1.6f);
            yield return ui.TitleCard("COMING NEXT:  4. The First Defeat",
                "“Training prepares you for the game. Losing teaches you what training couldn't.”", 7f);
            StoryState.Chapter = 4;
            yield return GoToScene(MainMenuScene, "ch4_start");
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
                lines.Add(new TrainingLine(label, $"{r.good}/{r.reps}", pts));
                total += pts;
                if (r.Ratio < worst) { worst = r.Ratio; weakest = r.kind; }
            }
            foreach (var l in extraLines) { lines.Add(l); total += l.points; }
            lines.Add(new TrainingLine("Showed up", "day " + day + " of 5", 1));
            total += 1;
            skills.AwardPoints(total, "Training day " + day);
            SetFlag("dev_note", NoteFor(weakest));
            SetFlag("ch3_points_d" + day, total.ToString());

            var c = Now;
            string footer = $"Condition now: {Chapter3Rules.Name(c)} — {Chapter3Rules.Effect(c)}  Tiredness carries into tomorrow; rest and sleep bring it down.";
            yield return ui.TrainingCard($"TRAINING COMPLETE  ·  DAY {day}", lines, footer);
        }

        static string NoteFor(DrillKind? weakest) => weakest switch
        {
            DrillKind.PassLR or DrillKind.PassPressure => "Steve: \"Late to a lot of balls. Court Awareness gets you there earlier; Athleticism gets you there faster.\"",
            DrillKind.ServeReceive => "Steve: \"First touch wandered. Bump steadies it.\"",
            DrillKind.SetControl => "Steve: \"Hands were the shaky part. Set would help.\"",
            DrillKind.ThreeTouch or DrillKind.SpikeTiming => "Steve: \"Attack timing. Spike sharpens the swing; Leg Power in the gym gets you higher.\"",
            DrillKind.ServeTarget => "Steve: \"Serves went everywhere. Serve skill steadies them.\"",
            DrillKind.BlockDig => "Steve: \"Reading the play — that's Court Awareness. Block helps at the net.\"",
            _ => "Steve: \"No obvious weak spot today. Build what you enjoy.\"",
        };
    }
}
