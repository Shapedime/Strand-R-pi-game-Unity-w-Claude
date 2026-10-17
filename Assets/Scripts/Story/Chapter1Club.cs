using System.Collections;
using BeachVolley.Player;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Chapter 1 at Coastline Beach Club: Scene 3 (arrival, Áron, Máté, Bence's spike), Scene 4
    /// (first touches: passing, setting, attacking), Scene 5 (Bence's taunt, the rivalry starts)
    /// and Scene 6 (one more try, the first spike over the net).
    /// </summary>
    public class Chapter1Club : StoryDirector
    {
        [SerializeField] NpcFigure aron, mate, bence;
        [SerializeField] ScriptedPlay clubRally;
        [SerializeField] StoryDrill drill;
        [SerializeField] BallContact contact;
        [SerializeField] GameObject car, bike;

        // Where things happen (the training court is at the origin, player side −z).
        static readonly Vector3 EntranceSpot = new Vector3(-7f, 0f, -14f);
        static readonly Vector3 AronMeetSpot = new Vector3(-6.3f, 0f, -12.4f);
        static readonly Vector3 MateMeetSpot = new Vector3(-7.9f, 0f, -12.3f);
        static readonly Vector3 AronCoachSpot = new Vector3(2.7f, 0f, -1.4f);
        static readonly Vector3 AronSideline = new Vector3(4.8f, 0f, -4.2f);
        static readonly Vector3 MateSetterSpot = new Vector3(0f, 0f, -2f);
        static readonly Vector3 MateFeedSpot = new Vector3(1.4f, 0f, -6f);
        static readonly Vector3 MateNetSpot = new Vector3(0.5f, 0f, -1.3f);
        static readonly Vector3 BenchSpot = new Vector3(-5.6f, 0f, -3.5f);

        bool struggled;
        int totalGood, totalReps;

        protected override IEnumerator Run()
        {
            ui.SetBlack(true);
            bool bikeRide = StoryState.Is("transport", "bike");
            if (car != null) car.SetActive(!bikeRide);
            if (bike != null) bike.SetActive(bikeRide);
            contact.ShowControlsHint = false;
            contact.enabled = false;

            if (StoryState.CheckpointName != "club_training") yield return Arrival(bikeRide);
            yield return FirstTouches();
            yield return FirstConflict();
            yield return OneSmallSuccess();
            yield return GoToScene(HomeScene, "home_end");
        }

        static Quaternion Facing(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d) : Quaternion.identity;
        }

        static void Put(NpcFigure npc, Vector3 at, Vector3 faceTowards)
        {
            npc.transform.SetPositionAndRotation(at, Facing(at, faceTowards));
            npc.LookAt(null);
        }

        IEnumerator WalkTo(NpcFigure npc, Vector3 to)
        {
            npc.WalkTo(to);
            while (npc.IsWalking) yield return null;
        }

        /// <summary>Over-the-shoulder shot: behind <paramref name="from"/>, looking at <paramref name="at"/>'s face.</summary>
        void OverShoulder(Transform from, Transform at, float side = 1f)
        {
            Vector3 dir = at.position - from.position;
            dir.y = 0f;
            dir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 pos = from.position + Vector3.up * 1.75f - dir * 1.4f + right * (0.55f * side);
            cam.Frame(pos, at.position + Vector3.up * 1.5f, 45f);
        }

        /// <summary>Side-on two-shot of two people talking.</summary>
        void TwoShot(Transform a, Transform b, float side = 1f, float distance = 3.4f)
        {
            Vector3 mid = (a.position + b.position) * 0.5f + Vector3.up * 1.45f;
            Vector3 dir = b.position - a.position;
            dir.y = 0f;
            dir.Normalize();
            Vector3 n = Vector3.Cross(Vector3.up, dir) * side;
            cam.Frame(mid + n * distance + Vector3.up * 0.15f, mid, 45f);
        }

        // ---------- Scene 3 ----------

        IEnumerator Arrival(bool bikeRide)
        {
            PlaceAt(EntranceSpot, Facing(EntranceSpot, Vector3.zero));
            Put(aron, new Vector3(2.5f, 0f, -6f), Vector3.zero);
            Put(mate, new Vector3(-1.2f, 0f, -6.5f), Vector3.zero);
            cam.Frame(EntranceSpot + new Vector3(-1.2f, 2.2f, -1.6f), new Vector3(2f, 1f, 0f), 55f);
            yield return FadeIn(1.5f);
            yield return Narrate("Coastline Beach Club. Several sand courts. Players are warming up, passing, practising spikes. The thud of the ball on the sand fills the air.");
            yield return Narrate("He stands near the entrance, unsure where to go.");

            // Áron notices him and comes over.
            aron.LookAt(player);
            yield return WalkTo(aron, AronMeetSpot);
            aron.LookAt(player);
            motor.FaceTowards(aron.transform.position, 0.3f);
            yield return Wait(0.3f);
            OverShoulder(player, aron.transform);
            yield return Say("Coach Áron", "Hey! You must be the new guy.");
            OverShoulder(aron.transform, player, -1f);
            yield return Say("You", "Yeah. I'm here for the beginner training session.");
            OverShoulder(player, aron.transform);
            yield return Say("Áron", "Great. I'm Áron. Have you played before?");

            yield return Choose(
                "“Never. I'm a complete beginner.”",
                "“I've watched quite a few matches, but I haven't really played.”",
                "“I don't know much about it yet, but I want to learn.”");
            switch (Choice)
            {
                case 0:
                    SetFlag("first_answer", "never");
                    yield return Say("Áron", "Honest. I like that. Beginners don't have bad habits yet.");
                    break;
                case 1:
                    SetFlag("first_answer", "watched");
                    yield return Say("Áron", "Watching helps — you'll know what it should look like. Your body just doesn't know yet.");
                    break;
                default:
                    SetFlag("first_answer", "learn");
                    Relationship("aron", 1);
                    yield return Say("Áron", "Wanting to learn is the only requirement here.");
                    break;
            }
            yield return Say("Áron", "Good. Then today, the goal isn't to be perfect. It's to understand the basics and have a good time. The rest will come later.");

            // Máté.
            yield return Say("Áron", "Máté! Come here a sec.");
            mate.LookAt(player);
            yield return WalkTo(mate, MateMeetSpot);
            mate.LookAt(player);
            motor.FaceTowards(mate.transform.position, 0.3f);
            TwoShot(player, mate.transform);
            yield return Say("Áron", "This is Máté. He's been training with us for a few weeks.");
            yield return Say("Máté", "Hey! Don't worry, I was terrible on my first day.");
            yield return Narrate("He laughs.");
            yield return Say("Máté", "I hit the ball straight into the net so hard I thought I was going to knock it over.");
            yield return Narrate("He laughs a little too. The knot in his stomach loosens.");
            yield return Say("Máté", bikeRide
                ? "Wait, did you bike here? Nice. You're already warmed up."
                : "You drove? Smart. Trust me, you'll want your legs today.");
            Relationship("mate", 1);

            // Bence's spike on the next court.
            clubRally.StopLoop();
            yield return cam.BlendFrame(new Vector3(9f, 2.2f, -9f), clubRally.transform.position + new Vector3(-1.4f, 1.6f, -1.2f), 1.4f, 42f);
            yield return clubRally.PowerSpike();
            yield return Narrate("A powerful spike on the next court. The ball hits the sand with a loud thud.");
            yield return Say("Máté", "That's Bence. He's been playing for years. He's… not big on talking to new people.");
            yield return Narrate("Confident, athletic, serious about the game. He doesn't even glance their way.");
            cam.Frame(player.position + new Vector3(-0.8f, 1.75f, -1.2f), clubRally.transform.position + Vector3.up * 1.2f, 45f);
            yield return Think("I want to be able to do that someday.");
            clubRally.StartLoop();

            TwoShot(aron.transform, player, 1f, 4.2f);
            yield return Say("Áron", "Alright, everyone. Let's get started!");
            yield return FadeOut(0.8f);
            StoryState.Checkpoint("club_training");
        }

        // ---------- Scene 4 ----------

        IEnumerator FirstTouches()
        {
            skills.BeginSession();
            Put(aron, AronCoachSpot, drill.SpotFor(DrillKind.Passing));
            Put(mate, MateSetterSpot, drill.SpotFor(DrillKind.Passing));
            PlaceAt(drill.SpotFor(DrillKind.Passing), Quaternion.identity);
            drill.Prepare(DrillKind.Passing);
            aron.LookAt(player);
            mate.LookAt(player);
            TwoShot(player, aron.transform, -1f, 4.5f);
            yield return FadeIn(0.8f);

            yield return Say("Áron", "Three basics today. First, passing: you control the ball with your forearms. Arms straight, platform flat, use your legs.");
            yield return Say("Áron", "Second, setting: hands above your forehead, you place the ball up for your partner.");
            yield return Say("Áron", "Third, attacking: you jump and hit the ball over the net.");
            yield return Say("Áron", "And one thing about beach volleyball: there's only two of you on the court. You have to trust each other, communicate, and cover the space together. You can't do everything alone.");
            yield return Say("Áron", "Passing first. I toss, you pass it to Máté. Ready?");

            yield return Drill(DrillKind.Passing, 6);
            yield return Say("Áron", Grade(lastGood, 6,
                "Look at that — Máté barely had to move. Nice hands.",
                "Some of those were really good. Keep your platform still and the rest will follow.",
                "Don't worry about where they went. You're watching the ball now; that's step one."));

            // Setting: Máté feeds from the back court.
            Put(mate, MateFeedSpot, drill.SpotFor(DrillKind.Setting));
            mate.LookAt(player);
            aron.WalkTo(AronSideline);
            yield return Say("Áron", "Now setting. Máté sends you the ball, you set it up high by the net. Get under it early.");
            drill.Prepare(DrillKind.Setting);
            yield return Drill(DrillKind.Setting, 5);
            yield return Say("Áron", Grade(lastGood, 5,
                "Those sets were hittable. Máté's going to want you as a partner.",
                "Better and better. Get your feet under the ball and your hands will do the rest.",
                "Setting is the hardest touch to feel at first. It'll come."));

            // Attacking: Máté sets by the net.
            Put(mate, MateNetSpot, drill.SpotFor(DrillKind.Attacking));
            mate.LookAt(player);
            yield return Say("Áron", "Last one: attacking. Start at the marker, wait for Máté's set, run in, jump, and hit it over. Timing over power.");
            drill.Prepare(DrillKind.Attacking);
            yield return Drill(DrillKind.Attacking, 5);
            yield return Say("Áron", Grade(lastGood, 5,
                "Ha! You've got some bounce. We can work with that.",
                "The timing's starting to show. That's the hardest part of all.",
                "Spiking is the last thing that clicks for everyone. Don't let it get to you."));
            drill.HideMarker();

            SetFlag("ch1_drill_score", $"{totalGood}/{totalReps}");
            if (totalGood * 2 >= totalReps) Relationship("aron", 1);
        }

        int lastGood;

        IEnumerator Drill(DrillKind kind, int reps)
        {
            cutscene = false;
            contact.enabled = true;
            contact.ShowControlsHint = true;
            cam.Follow();
            yield return drill.Run(kind, reps, good => lastGood = good, struggled ? null : (System.Func<IEnumerator>)Struggle);
            totalGood += lastGood;
            totalReps += reps;
            contact.enabled = false;
            contact.ShowControlsHint = false;
            cutscene = true;
            TwoShot(player, aron.transform, -1f, 4.5f);
        }

        IEnumerator Struggle()
        {
            struggled = true;
            contact.enabled = false;
            yield return Say("You", "Why is this so much harder than it looks?");
            yield return Say("Áron", "Because on television, you don't see the ten thousand hours of practice.");
            yield return Narrate("He picks up the ball and tries again.");
            contact.enabled = true;
        }

        static string Grade(int good, int reps, string great, string ok, string rough) =>
            good * 3 >= reps * 2 ? great : good * 3 >= reps ? ok : rough;

        // ---------- Scene 5 ----------

        IEnumerator FirstConflict()
        {
            yield return FadeOut(0.6f);
            PlaceAt(BenchSpot, Facing(BenchSpot, Vector3.zero));
            Put(mate, BenchSpot + new Vector3(1.1f, 0f, 0.3f), BenchSpot);
            Put(bence, BenchSpot + new Vector3(2.6f, 0f, 2.2f), BenchSpot);
            Put(aron, new Vector3(2.5f, 0f, -5f), Vector3.zero);
            clubRally.StopLoop();
            mate.LookAt(player);
            cam.Frame(BenchSpot + new Vector3(2.6f, 1.7f, -2.4f), BenchSpot + new Vector3(0.3f, 1.2f, 0.8f), 48f);
            yield return FadeIn(0.6f);
            yield return Narrate("After the drills, a short break.");

            bence.LookAt(player);
            yield return WalkTo(bence, BenchSpot + new Vector3(1.4f, 0f, 1.6f));
            bence.LookAt(player);
            motor.FaceTowards(bence.transform.position, 0.3f);
            OverShoulder(player, bence.transform);
            yield return Say("Bence", "You're the new guy?");
            OverShoulder(bence.transform, player, -1f);
            yield return Say("You", "Yeah.");
            yield return Narrate("Bence keeps looking at him.");
            yield return Say("You", "…What?");
            OverShoulder(player, bence.transform);
            yield return Say("Bence", "Nothing. I just thought someone who can barely hit the ball might want to wait a little longer before showing up to training.");
            TwoShot(mate.transform, bence.transform, 1f, 4f);
            yield return Say("Máté", "Come on, man. Everyone was a beginner once.");
            yield return Narrate("Bence shrugs.");
            yield return Say("Bence", "Sure. But not everyone thinks they're going to become a pro after a few training sessions.");

            OverShoulder(bence.transform, player, -1f);
            yield return Narrate("That stings. He could let it go. He decides not to back down.");
            yield return Choose("“We'll see who's laughing in the end.”");
            yield return Say("You", "We'll see who's laughing in the end.");
            OverShoulder(player, bence.transform);
            yield return Narrate("Bence smiles, but his expression turns a little more serious.");
            yield return Say("Bence", "Alright. I'll remember that.");
            SetFlag("rivalry_bence", "started");
            Relationship("bence_rivalry", 1);
            Relationship("mate", 1);

            bence.LookAt(null);
            bence.WalkTo(clubRally.transform.position + new Vector3(-1.5f, 0f, -4.5f));
            TwoShot(player, mate.transform, 1f, 3.6f);
            yield return Wait(1.2f);
            yield return Say("Máté", "Don't let him get to you. He's like that with everyone. Well… mostly.");

            // Áron saw it. A first hint that Bence has his reasons.
            aron.LookAt(player);
            yield return WalkTo(aron, BenchSpot + new Vector3(0.6f, 0f, 1.8f));
            aron.LookAt(player);
            yield return Say("Áron", "Bence takes this more seriously than anyone here. He's got his reasons. Maybe one day he'll tell you.");
            SetFlag("bence_hint", "reasons");
            bence.transform.position = clubRally.transform.position + new Vector3(-1.5f, 0f, -4.5f);
            bence.transform.rotation = Quaternion.identity;
            clubRally.StartLoop();
        }

        // ---------- Scene 6 ----------

        IEnumerator OneSmallSuccess()
        {
            yield return FadeOut(0.8f);
            yield return ui.TitleCard("An hour later", "", 1.6f);
            PlaceAt(BenchSpot, Facing(BenchSpot, Vector3.zero));
            Put(aron, BenchSpot + new Vector3(0.8f, 0f, 0.1f), Vector3.zero);
            Put(mate, new Vector3(-1.2f, 0f, -2.6f), BenchSpot);
            aron.LookAt(player);
            cam.Frame(BenchSpot + new Vector3(-1.2f, 1.2f, 2.8f), BenchSpot + new Vector3(0.4f, 1.1f, 0f), 45f);
            yield return FadeIn(0.8f);

            yield return Narrate("His legs ache. Sand sticks to his clothes. His forearms are red from the ball. He sits down at the edge of the court.");
            yield return Say("You", "I don't know how I'm ever going to learn this.");
            yield return Narrate("Áron sits down beside him.");
            yield return Say("Áron", "Let me tell you the difference between you and the players you saw on television.");
            yield return Narrate("He looks up.");
            yield return Say("Áron", "They were here once, too. They stood in this same place, and they missed balls just like you did.");
            yield return Wait(0.6f);
            yield return Say("Áron", "The difference is that they came back the next day.");

            mate.LookAt(player);
            yield return Narrate("Máté picks up a ball.");
            yield return Say("Áron", "Want to try one more?");
            yield return Narrate("He hesitates.");
            yield return Choose("Stand up.  “One more.”");
            yield return Say("You", "One more.");

            // The last attack: Máté sets until one goes over. The assist quietly grows after each miss.
            yield return FadeOut(0.4f);
            Put(mate, MateNetSpot, drill.SpotFor(DrillKind.Attacking));
            Put(aron, AronSideline, Vector3.zero);
            mate.LookAt(player);
            aron.LookAt(player);
            PlaceAt(drill.SpotFor(DrillKind.Attacking), Quaternion.identity);
            drill.Prepare(DrillKind.Attacking);
            yield return FadeIn(0.4f);
            cutscene = false;
            contact.enabled = true;
            cam.Follow();
            yield return drill.Run(DrillKind.Attacking, 1, null, null, untilSuccess: true);
            contact.enabled = false;
            cutscene = true;
            drill.HideMarker();

            yield return Narrate("It isn't a perfect spike. The movement is awkward, and the ball doesn't land where he meant it to. But it goes over.");
            TwoShot(player, mate.transform, 1f, 3.6f);
            yield return Narrate("For a moment, he just stands there.");
            yield return Say("Máté", "There! Did you see that?");
            yield return Say("You", "I saw it.");
            TwoShot(player, aron.transform, -1f, 4f);
            yield return Narrate("Áron smiles.");
            yield return Say("Áron", "Then I'll see you here the day after tomorrow.");
            Relationship("aron", 1);
            yield return Narrate("He looks across the court. For the first time, the idea of coming back doesn't feel intimidating. It feels exciting.");
            skills.EndSession();

            // On the way out: Bence is still training, alone.
            yield return FadeOut(0.8f);
            clubRally.StopLoop();
            cam.Frame(new Vector3(8f, 2f, -12f), clubRally.transform.position + new Vector3(-1.4f, 1.4f, -1.5f), 40f);
            yield return FadeIn(0.8f);
            yield return Narrate("On his way out, almost everyone has gone home. On the far court, Bence is still there, hitting ball after ball.");
            yield return clubRally.PowerSpike();
            yield return Wait(0.6f);
            SetFlag("bence_trains_late", "seen");
        }
    }
}
