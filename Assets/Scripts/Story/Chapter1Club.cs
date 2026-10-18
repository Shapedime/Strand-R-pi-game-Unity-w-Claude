using System.Collections;
using BeachVolley.Player;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Chapter 1 at Coastline Beach Club: Scene 3 (arrival, Steve, Dave, Marlon's spike), Scene 4
    /// (first touches: passing, setting, attacking), Scene 5 (Marlon's taunt, the rivalry starts)
    /// and Scene 6 (one more try, the first spike over the net).
    /// </summary>
    public class Chapter1Club : StoryDirector
    {
        [SerializeField] StoryActor steve, dave, marlon;
        [SerializeField] ScriptedPlay clubRally;
        [SerializeField] StoryDrill drill;
        [SerializeField] BallContact contact;
        [SerializeField] GameObject car, bike;

        // Where things happen (the training court is at the origin, player side −z).
        static readonly Vector3 EntranceSpot = new Vector3(-7f, 0f, -14f);
        static readonly Vector3 SteveMeetSpot = new Vector3(-6.3f, 0f, -12.4f);
        static readonly Vector3 DaveMeetSpot = new Vector3(-7.9f, 0f, -12.3f);
        static readonly Vector3 SteveCoachSpot = new Vector3(2.7f, 0f, -1.4f);
        static readonly Vector3 SteveSideline = new Vector3(4.8f, 0f, -4.2f);
        static readonly Vector3 DaveSetterSpot = new Vector3(0f, 0f, -2f);
        static readonly Vector3 DaveFeedSpot = new Vector3(1.4f, 0f, -6f);
        static readonly Vector3 DaveNetSpot = new Vector3(0.5f, 0f, -1.3f);
        // On the bench seat (the sit clips move the hips back about 0.3 m onto it); Steve sits beside him.
        static readonly Vector3 BenchSpot = new Vector3(-5.95f, 0f, -3.74f);

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

        // ---------- Scene 3 ----------

        IEnumerator Arrival(bool bikeRide)
        {
            PlaceAt(EntranceSpot, Facing(EntranceSpot, Vector3.zero));
            Put(steve, new Vector3(2.5f, 0f, -6f), Vector3.zero);
            Put(dave, new Vector3(-1.2f, 0f, -6.5f), Vector3.zero);
            cam.Frame(EntranceSpot + new Vector3(-1.2f, 2.2f, -1.6f), new Vector3(2f, 1f, 0f), 55f);
            yield return FadeIn(1.5f);
            yield return Narrate("Coastline Beach Club. Several sand courts. Players are warming up, passing, practising spikes. The thud of the ball on the sand fills the air.");
            yield return Narrate("He stands near the entrance, unsure where to go.");

            // Steve notices him and comes over.
            steve.Gesture("Wave");
            steve.LookAt(player);
            yield return WalkTo(steve, SteveMeetSpot);
            steve.LookAt(player);
            motor.FaceTowards(steve.transform.position, 0.3f);
            yield return Wait(0.3f);
            OverShoulder(player, steve.transform);
            steve.Gesture("Wave");
            yield return Say("Coach Steve", "Hey! You must be the new guy.");
            OverShoulder(steve.transform, player, -1f);
            yield return Say("You", "Yeah. I'm here for the beginner training session.");
            OverShoulder(player, steve.transform);
            yield return Say("Steve", "Great. I'm Steve. Have you played before?");

            yield return Choose(
                "“Never. I'm a complete beginner.”",
                "“I've watched quite a few matches, but I haven't really played.”",
                "“I don't know much about it yet, but I want to learn.”");
            switch (Choice)
            {
                case 0:
                    SetFlag("first_answer", "never");
                    yield return Say("Steve", "Honest. I like that. Beginners don't have bad habits yet.");
                    break;
                case 1:
                    SetFlag("first_answer", "watched");
                    yield return Say("Steve", "Watching helps — you'll know what it should look like. Your body just doesn't know yet.");
                    break;
                default:
                    SetFlag("first_answer", "learn");
                    Relationship("steve", 1);
                    yield return Say("Steve", "Wanting to learn is the only requirement here.");
                    break;
            }
            steve.Gesture("Nod");
            yield return Say("Steve", "Good. Then today, the goal isn't to be perfect. It's to understand the basics and have a good time. The rest will come later.");

            // Dave.
            yield return Say("Steve", "Dave! Come here a sec.");
            dave.LookAt(player);
            yield return WalkTo(dave, DaveMeetSpot);
            dave.LookAt(player);
            motor.FaceTowards(dave.transform.position, 0.3f);
            TwoShot(player, dave.transform);
            yield return Say("Steve", "This is Dave. He's been training with us for a few weeks.");
            yield return Say("Dave", "Hey! Don't worry, I was terrible on my first day.");
            dave.Gesture("Laugh");
            yield return Narrate("He laughs.");
            yield return Say("Dave", "I hit the ball straight into the net so hard I thought I was going to knock it over.");
            playerActor.Gesture("Laugh");
            yield return Narrate("He laughs a little too. The knot in his stomach loosens.");
            yield return Say("Dave", bikeRide
                ? "Wait, did you bike here? Nice. You're already warmed up."
                : "You drove? Smart. Trust me, you'll want your legs today.");
            Relationship("dave", 1);

            // Marlon's spike on the next court.
            clubRally.StopLoop();
            yield return cam.BlendFrame(new Vector3(9f, 2.2f, -9f), clubRally.transform.position + new Vector3(-1.4f, 1.6f, -1.2f), 1.4f, 42f);
            yield return clubRally.PowerSpike();
            yield return Narrate("A powerful spike on the next court. The ball hits the sand with a loud thud.");
            yield return Say("Dave", "That's Marlon. He's been playing for years. He's… not big on talking to new people.");
            yield return Narrate("Confident, athletic, serious about the game. He doesn't even glance their way.");
            cam.Frame(player.position + new Vector3(-0.8f, 1.75f, -1.2f), clubRally.transform.position + Vector3.up * 1.2f, 45f);
            yield return Think("I want to be able to do that someday.");
            clubRally.StartLoop();

            TwoShot(steve.transform, player, 1f, 4.2f);
            steve.Gesture("Clap");
            yield return Say("Steve", "Alright, everyone. Let's get started!");
            yield return FadeOut(0.8f);
            StoryState.Checkpoint("club_training");
        }

        // ---------- Scene 4 ----------

        IEnumerator FirstTouches()
        {
            skills.BeginSession();
            Put(steve, SteveCoachSpot, drill.SpotFor(DrillKind.Passing));
            Put(dave, DaveSetterSpot, drill.SpotFor(DrillKind.Passing));
            PlaceAt(drill.SpotFor(DrillKind.Passing), Quaternion.identity);
            drill.Prepare(DrillKind.Passing);
            steve.LookAt(player);
            dave.LookAt(player);
            TwoShot(player, steve.transform, -1f, 4.5f);
            yield return FadeIn(0.8f);

            steve.Volley(HitType.Bump);
            yield return Say("Steve", "Three basics today. First, passing: you control the ball with your forearms. Arms straight, platform flat, use your legs.");
            steve.Volley(HitType.Set);
            yield return Say("Steve", "Second, setting: hands above your forehead, you place the ball up for your partner.");
            steve.Volley(HitType.Spike);
            yield return Say("Steve", "Third, attacking: you jump and hit the ball over the net.");
            yield return Say("Steve", "And one thing about beach volleyball: there's only two of you on the court. You have to trust each other, communicate, and cover the space together. You can't do everything alone.");
            yield return Say("Steve", "Passing first. I toss, you pass it to Dave. Ready?");

            yield return Drill(DrillKind.Passing, 6);
            yield return Say("Steve", Grade(lastGood, 6,
                "Look at that — Dave barely had to move. Nice hands.",
                "Some of those were really good. Keep your platform still and the rest will follow.",
                "Don't worry about where they went. You're watching the ball now; that's step one."));

            // Setting: Dave feeds from the back court.
            Put(dave, DaveFeedSpot, drill.SpotFor(DrillKind.Setting));
            dave.LookAt(player);
            steve.WalkTo(SteveSideline);
            yield return Say("Steve", "Now setting. Dave sends you the ball, you set it up high by the net. Get under it early.");
            drill.Prepare(DrillKind.Setting);
            yield return Drill(DrillKind.Setting, 5);
            yield return Say("Steve", Grade(lastGood, 5,
                "Those sets were hittable. Dave's going to want you as a partner.",
                "Better and better. Get your feet under the ball and your hands will do the rest.",
                "Setting is the hardest touch to feel at first. It'll come."));

            // Attacking: Dave sets by the net.
            Put(dave, DaveNetSpot, drill.SpotFor(DrillKind.Attacking));
            dave.LookAt(player);
            yield return Say("Steve", "Last one: attacking. Start at the marker, wait for Dave's set, run in, jump, and hit it over. Timing over power.");
            drill.Prepare(DrillKind.Attacking);
            yield return Drill(DrillKind.Attacking, 5);
            yield return Say("Steve", Grade(lastGood, 5,
                "Ha! You've got some bounce. We can work with that.",
                "The timing's starting to show. That's the hardest part of all.",
                "Spiking is the last thing that clicks for everyone. Don't let it get to you."));
            drill.HideMarker();

            SetFlag("ch1_drill_score", $"{totalGood}/{totalReps}");
            // Development points for the first session; they're spent from Chapter 2 on.
            skills.AwardPoints(2, "First training");
            if (totalGood * 2 >= totalReps) Relationship("steve", 1);
        }

        int lastGood;

        IEnumerator Drill(DrillKind kind, int reps)
        {
            playerActor.EndActing();
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
            playerActor.BeginActing();
            if (lastGood * 2 >= reps) steve.Gesture("Clap");
            TwoShot(player, steve.transform, -1f, 4.5f);
        }

        IEnumerator Struggle()
        {
            struggled = true;
            contact.enabled = false;
            playerActor.Gesture("Shrug");
            yield return Say("You", "Why is this so much harder than it looks?");
            yield return Say("Steve", "Because on television, you don't see the ten thousand hours of practice.");
            yield return Narrate("He picks up the ball and tries again.");
            contact.enabled = true;
        }

        // ---------- Scene 5 ----------

        IEnumerator FirstConflict()
        {
            yield return FadeOut(0.6f);
            PlaceAt(BenchSpot, Facing(BenchSpot, Vector3.zero));
            Put(dave, BenchSpot + new Vector3(1.1f, 0f, 0.3f), BenchSpot);
            Put(marlon, BenchSpot + new Vector3(2.6f, 0f, 2.2f), BenchSpot);
            Put(steve, new Vector3(2.5f, 0f, -5f), Vector3.zero);
            clubRally.StopLoop();
            dave.LookAt(player);
            cam.Frame(BenchSpot + new Vector3(2.6f, 1.7f, -2.4f), BenchSpot + new Vector3(0.3f, 1.2f, 0.8f), 48f);
            yield return FadeIn(0.6f);
            yield return Narrate("After the drills, a short break.");

            marlon.LookAt(player);
            yield return WalkTo(marlon, BenchSpot + new Vector3(1.4f, 0f, 1.6f));
            marlon.LookAt(player);
            motor.FaceTowards(marlon.transform.position, 0.3f);
            OverShoulder(player, marlon.transform);
            marlon.Gesture("ArmsCrossed");
            yield return Say("Marlon", "You're the new guy?");
            OverShoulder(marlon.transform, player, -1f);
            yield return Say("You", "Yeah.");
            yield return Narrate("Marlon keeps looking at him.");
            yield return Say("You", "…What?");
            OverShoulder(player, marlon.transform);
            marlon.EndPose("ArmsCrossed");
            marlon.Gesture("Dismiss");
            yield return Say("Marlon", "Nothing. I just thought someone who can barely hit the ball might want to wait a little longer before showing up to training.");
            TwoShot(dave.transform, marlon.transform, 1f, 4f);
            dave.Gesture("HeadShake");
            yield return Say("Dave", "Come on, man. Everyone was a beginner once.");
            marlon.EndPose("ArmsCrossed");
            marlon.Gesture("Shrug");
            yield return Narrate("Marlon shrugs.");
            yield return Say("Marlon", "Sure. But not everyone thinks they're going to become a pro after a few training sessions.");

            OverShoulder(marlon.transform, player, -1f);
            yield return Narrate("That stings. He could let it go. He decides not to back down.");
            yield return Choose("“We'll see who's laughing in the end.”");
            playerActor.Gesture("Point");
            yield return Say("You", "We'll see who's laughing in the end.");
            OverShoulder(player, marlon.transform);
            yield return Narrate("Marlon smiles, but his expression turns a little more serious.");
            marlon.Gesture("Nod");
            yield return Say("Marlon", "Alright. I'll remember that.");
            SetFlag("rivalry_marlon", "started");
            Relationship("marlon_rivalry", 1);
            Relationship("dave", 1);

            marlon.LookAt(null);
            marlon.WalkTo(clubRally.transform.position + new Vector3(-1.5f, 0f, -4.5f));
            TwoShot(player, dave.transform, 1f, 3.6f);
            yield return Wait(1.2f);
            yield return Say("Dave", "Don't let him get to you. He's like that with everyone. Well… mostly.");

            // Steve saw it. A first hint that Marlon has his reasons.
            steve.LookAt(player);
            yield return WalkTo(steve, BenchSpot + new Vector3(0.6f, 0f, 1.8f));
            steve.LookAt(player);
            yield return Say("Steve", "Marlon takes this more seriously than anyone here. He's got his reasons. Maybe one day he'll tell you.");
            SetFlag("marlon_hint", "reasons");
            marlon.transform.position = clubRally.transform.position + new Vector3(-1.5f, 0f, -4.5f);
            marlon.transform.rotation = Quaternion.identity;
            clubRally.StartLoop();
        }

        // ---------- Scene 6 ----------

        IEnumerator OneSmallSuccess()
        {
            yield return FadeOut(0.8f);
            yield return ui.TitleCard("An hour later", "", 1.6f);
            SitPlayer(BenchSeat(0f), BenchFacing);
            // Steve stands at the bench beside him; he sits down when the scene asks.
            steve.transform.SetPositionAndRotation(SeatFront(BenchSeat(-0.75f), BenchFacing), BenchFacing);
            Put(dave, new Vector3(-1.2f, 0f, -2.6f), BenchSpot);
            steve.LookAt(player);
            cam.Frame(BenchSpot + new Vector3(-1.2f, 1.2f, 2.8f), BenchSpot + new Vector3(0.4f, 1.1f, 0f), 45f);
            yield return FadeIn(0.8f);

            yield return Narrate("His legs ache. Sand sticks to his clothes. His forearms are red from the ball. He sits down at the edge of the court.");
            playerActor.Gesture("Sigh");
            yield return Say("You", "I don't know how I'm ever going to learn this.");
            steve.Sit(true);
            yield return Narrate("Steve sits down beside him.");
            yield return Say("Steve", "Let me tell you the difference between you and the players you saw on television.");
            yield return Narrate("He looks up.");
            yield return Say("Steve", "They were here once, too. They stood in this same place, and they missed balls just like you did.");
            yield return Wait(0.6f);
            yield return Say("Steve", "The difference is that they came back the next day.");

            dave.LookAt(player);
            yield return Narrate("Dave picks up a ball.");
            yield return Say("Steve", "Want to try one more?");
            yield return Narrate("He hesitates.");
            yield return Choose("Stand up.  “One more.”");
            steve.Sit(false);
            yield return StandPlayer();
            yield return Say("You", "One more.");

            // The last attack: Dave sets until one goes over. The assist quietly grows after each miss.
            yield return FadeOut(0.4f);
            Put(dave, DaveNetSpot, drill.SpotFor(DrillKind.Attacking));
            Put(steve, SteveSideline, Vector3.zero);
            dave.LookAt(player);
            steve.LookAt(player);
            PlaceAt(drill.SpotFor(DrillKind.Attacking), Quaternion.identity);
            drill.Prepare(DrillKind.Attacking);
            yield return FadeIn(0.4f);
            cutscene = false;
            contact.enabled = true;
            cam.Follow();
            playerActor.EndActing();
            yield return drill.Run(DrillKind.Attacking, 1, (System.Action<int>)null, null, untilSuccess: true);
            contact.enabled = false;
            cutscene = true;
            playerActor.BeginActing();
            playerActor.Gesture("FistPump");
            dave.Gesture("Cheer");
            drill.HideMarker();

            yield return Narrate("It isn't a perfect spike. The movement is awkward, and the ball doesn't land where he meant it to. But it goes over.");
            TwoShot(player, dave.transform, 1f, 3.6f);
            yield return Narrate("For a moment, he just stands there.");
            yield return Say("Dave", "There! Did you see that?");
            yield return Say("You", "I saw it.");
            TwoShot(player, steve.transform, -1f, 4f);
            steve.Gesture("Nod");
            yield return Narrate("Steve smiles.");
            yield return Say("Steve", "Then I'll see you here the day after tomorrow.");
            Relationship("steve", 1);
            yield return Narrate("He looks across the court. For the first time, the idea of coming back doesn't feel intimidating. It feels exciting.");
            skills.EndSession();

            // On the way out: Marlon is still training, alone.
            yield return FadeOut(0.8f);
            clubRally.StopLoop();
            cam.Frame(new Vector3(8f, 2f, -12f), clubRally.transform.position + new Vector3(-1.4f, 1.4f, -1.5f), 40f);
            yield return FadeIn(0.8f);
            yield return Narrate("On his way out, almost everyone has gone home. On the far court, Marlon is still there, hitting ball after ball.");
            yield return clubRally.PowerSpike();
            yield return Wait(0.6f);
            SetFlag("marlon_trains_late", "seen");
        }
    }
}
