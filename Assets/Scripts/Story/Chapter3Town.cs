using System.Collections;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Chapter 3 away from the club and the bedroom: the beachside café with Dave on Day 1 (and the
    /// walk home along the promenade), the gym on the Day 2 morning, and on Day 3 either the
    /// conditioning session or food with Dave, Alex and Nina. How Dave talks depends on his
    /// friendship band (see <see cref="Chapter3Rules.DaveBand"/>).
    /// </summary>
    public class Chapter3Town : StoryDirector
    {
        [SerializeField] StoryActor dave, alex, nina;
        [SerializeField] GymSession gym;
        [SerializeField] Light foodLight;
        [SerializeField] Transform cafeArrive, cafeSeatYou, cafeSeatDave, promenadeStart, junction;
        [SerializeField] Transform foodSeatYou, foodSeatDave, foodSeatAlex, foodSeatNina;
        [SerializeField] Transform gymSpot, gymDoor;

        protected override int ChapterNumber => 3;

        int day;

        protected override IEnumerator Run()
        {
            ui.SetBlack(true);
            string cp = StoryState.CheckpointName;
            day = cp != null && cp.StartsWith("town3_d") && int.TryParse(cp.Substring(7), out int d) ? d : 1;
            alex.gameObject.SetActive(false);
            nina.gameObject.SetActive(false);
            if (foodLight != null) foodLight.gameObject.SetActive(false);

            switch (day)
            {
                case 1:
                    yield return Cafe();
                    yield return GoToScene(HomeScene, "home3_d1");
                    break;
                case 2:
                    yield return MorningGym();
                    yield return ui.TitleCard("4:45 PM", "Coastline Beach Club", 2f);
                    yield return GoToScene(ClubScene, "club3_d2");
                    break;
                default:
                    if (StoryState.Is("ch3_d3", "group")) yield return Group();
                    else yield return Conditioning();
                    yield return GoToScene(HomeScene, "home3_d3");
                    break;
            }
        }

        // ---------- Light ----------

        void Sun(Vector3 euler, Color color, float intensity, Color fog)
        {
            var sun = RenderSettings.sun;
            if (sun == null) return;
            sun.transform.rotation = Quaternion.Euler(euler);
            sun.color = color;
            sun.intensity = intensity;
            RenderSettings.fogColor = fog;
            DynamicGI.UpdateEnvironment();
        }

        // ---------- Moving the player in cutscenes ----------

        /// <summary>Walks the player (whose motor normally moves him) to a point, at a stroll.</summary>
        IEnumerator WalkPlayer(Vector3 to, float speed = 1.5f)
        {
            if (motor != null) motor.enabled = false;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            to.y = player.position.y;
            while ((to - player.position).sqrMagnitude > 0.0025f)
            {
                Vector3 step = Vector3.MoveTowards(player.position, to, speed * Time.deltaTime);
                Vector3 dir = step - player.position;
                if (dir.sqrMagnitude > 1e-6f)
                    player.rotation = Quaternion.RotateTowards(player.rotation, Quaternion.LookRotation(dir), 360f * Time.deltaTime);
                player.position = step;
                yield return null;
            }
            if (cc != null) cc.enabled = true;
            if (motor != null) motor.enabled = true;
        }

        // ---------- Day 1: the café ----------

        IEnumerator Cafe()
        {
            Sun(new Vector3(12f, 262f, 0f), new Color(1f, 0.68f, 0.42f), 1.2f, new Color(0.95f, 0.72f, 0.56f));
            var band = Chapter3Rules.DaveBand();
            PlaceAt(cafeArrive.position, cafeArrive.rotation);
            Put(dave, cafeArrive.position + new Vector3(0.9f, 0f, 0.3f), cafeSeatDave.position);
            Cut("CafeWide");
            yield return FadeIn(1.2f);
            yield return Narrate("The café is a short walk down the beach. A few people sit outside watching the waves. Dave finds an empty table facing the sea.");
            dave.WalkTo(cafeSeatDave.position + new Vector3(0.6f, 0f, -0.6f));
            yield return WalkPlayer(cafeSeatYou.position + new Vector3(-0.6f, 0f, -0.6f));
            while (dave.IsWalking) yield return null;
            // Sitting down at a café table clips through it for a moment: do it behind a fade.
            yield return FadeOut(0.5f);
            SitNpc(dave, cafeSeatDave.position, cafeSeatDave.rotation);
            SitPlayer(cafeSeatYou.position, cafeSeatYou.rotation);
            yield return Wait(1.3f);
            Cut("Cafe");
            yield return FadeIn(0.6f);
            dave.LookAt(player);
            yield return Narrate("Drinks first, then something to eat. Both of them sink into the chairs like they've been carrying sandbags.");

            yield return Say("Dave", "So, how are you feeling?");
            yield return Say("You", "Physically? Terrible.");
            yield return Say("Dave", "Good. That means Steve did his job.");
            yield return Say("You", "I think my legs stopped working halfway through the last drill.");
            yield return Say("Dave", "You were still moving.");
            yield return Say("You", "Barely.");

            if (band == Band.Low)
            {
                // Less familiar: Dave carries the conversation, the answers stay short.
                yield return Say("Dave", "Hey, you did better than your first session. Way better.");
                yield return Say("You", "Thanks.");
                yield return Narrate("A pause. Dave tries a joke about Steve's whistle. He gets a smile, but not much more.");
                yield return Say("Dave", "Marlon's not always like that, you know. Okay — he's mostly like that. He just wants bigger competitions really badly.");
                yield return Say("You", "Doesn't give him the right to treat everyone like they're wasting his time.");
                yield return Say("Dave", "No. It doesn't.");
                yield return Narrate("The food arrives, and for a while it's easier to eat than to talk. It's not a bad silence. Just a new one.");
            }
            else
            {
                yield return Say("Dave", "Hey, you did better than your first session.");
                yield return Say("You", "Sometimes it doesn't feel like I'm improving fast enough.");
                yield return Say("Dave", "You've been training for what, a few sessions?");
                yield return Say("You", "When you say it like that, it sounds ridiculous.");
                yield return Say("Dave", "Because it is. You can't expect to walk onto a beach court and suddenly play like a professional.");
                yield return Say("You", "Tell that to Marlon.");
                dave.Gesture("Laugh");
                yield return Say("Dave", "Marlon's different. He's been here a long time. He knows what he's doing, and he's got big plans.");
                yield return Say("You", "You make him sound like he's preparing for the Olympics.");
                yield return Say("Dave", "Wouldn't surprise me.");
                playerActor.Gesture("Laugh");
                yield return Say("Dave", "Seriously, though. He's been trying to get into bigger competitions. He puts a lot of pressure on himself.");
                yield return Say("You", "That doesn't give him the right to treat everyone else like they're wasting his time.");
                yield return Say("Dave", "No. It doesn't.");
                yield return Say("Dave", "But I think sometimes he forgets that everyone has their own pace. He sees what he wants to become, and anything that slows him down annoys him.");
                yield return Say("You", "Sounds exhausting.");
                yield return Say("Dave", "It probably is.");
                Cut("CafeSea");
                yield return Narrate("For a moment, neither says anything. The sound of the waves fills the silence.");
                Cut("Cafe");
                yield return Say("You", "Why did you start playing?");
                yield return Say("Dave", "Honestly? My friend dragged me along. Said we needed another person for a game.");
                yield return Say("You", "And you just stayed?");
                yield return Say("Dave", "At first, I was terrible. Couldn't pass properly, kept sending the ball everywhere except where it needed to go. I showed up to my first session in running shoes. On sand.");
                yield return Say("You", "So nothing has changed.");
                dave.Gesture("Happy");
                yield return Say("Dave", "Exactly. I've built my entire career on consistency.");
                yield return Say("Dave", "But I liked it. Not just the game. The people, too. When you get a good rally going, when everyone moves together without having to think about it… it's a great feeling.");
                yield return Say("You", "Even when your teammate messes up?");
                yield return Say("Dave", "Especially then. Anyone can celebrate a perfect point. The real test is what you do after a mistake.");
                yield return Think("Move your feet instead of reaching for the ball.");
                yield return Say("You", "I guess I've got a lot to learn.");
                yield return Say("Dave", "We both do.");

                if (band == Band.High)
                {
                    // Trusted: Dave says the thing he usually jokes around.
                    yield return Narrate("Dave turns his cup around on the table, slowly.");
                    yield return Say("Dave", "Can I tell you something stupid? Sometimes I think I'll never be good enough for a higher level. Like I've already found my ceiling, and it's… here.");
                    yield return Choose("“You're better than you think. I've watched you.”", "“Your ceiling is a café table? Bold.”");
                    if (Choice == 0)
                    {
                        SetFlag("ch3_cafe_talk", "reassure");
                        yield return Say("You", "You're better than you think. I've watched you make that read on the cross shot ten times.");
                        dave.Gesture("Nod");
                        yield return Say("Dave", "…Yeah. Okay. Thanks. Don't make it weird.");
                    }
                    else
                    {
                        SetFlag("ch3_cafe_talk", "joke");
                        yield return Say("You", "Your ceiling is a café table? Bold.");
                        dave.Gesture("Laugh");
                        yield return Say("Dave", "It's a very good table. Load-bearing.");
                        yield return Narrate("He laughs, and the worry goes back wherever he keeps it. A little lighter, though.");
                    }
                }
            }

            // Scene 4: a little competition.
            yield return Narrate("The food arrives. Dave reaches for his before the plate touches the table.");
            yield return Say("You", "You talk a lot about teamwork for someone who nearly stole my last ball during practice.");
            yield return Say("Dave", "That ball was clearly coming to me.");
            yield return Say("You", "It was on my side.");
            yield return Say("Dave", "Court awareness. Look it up.");
            yield return Say("You", "I thought you were the one who needed to work on that.");
            yield return Say("Dave", "Wow. One decent training session and suddenly you're an expert.");
            yield return Say("Dave", "Tell you what. Next session, we run that three-touch drill again. First person to complete five clean attacks wins.");
            yield return Say("You", "And what does the winner get?");
            yield return Say("Dave", "The loser buys the winner a drink.");
            yield return Say("You", "You just invited me here and now you're trying to make me pay again?");
            yield return Say("Dave", "I'm teaching you an important lesson about the professional world. Never miss an opportunity.");
            yield return Narrate("They shake hands across the table as if signing a professional contract.");
            yield return Say("You", "You're on.");
            SetFlag("ch3_challenge", "set");

            // Scene 5: the walk home.
            yield return FadeOut(0.8f);
            dave.Sit(false);
            playerActor.Sit(false);
            Sun(new Vector3(4f, 265f, 0f), new Color(0.95f, 0.55f, 0.4f), 0.8f, new Color(0.6f, 0.5f, 0.55f));
            PlaceAt(promenadeStart.position, promenadeStart.rotation);
            if (motor != null) motor.enabled = true;
            Put(dave, promenadeStart.position + new Vector3(0f, 0f, -0.9f), junction.position);
            Cut("Promenade");
            yield return FadeIn(0.8f);
            Vector3 meet = junction.position + new Vector3(-0.6f, 0f, 0f);
            dave.WalkTo(meet + new Vector3(0f, 0f, -0.9f));
            StartCoroutine(WalkPlayer(meet, 1.55f));
            yield return Say("Dave", "You know, you're taking this seriously.");
            yield return Say("You", "Is that a bad thing?");
            yield return Say("Dave", "No. I'm just saying, when you first showed up, I thought you'd come for a few sessions and disappear.");
            yield return Say("You", "What changed your mind?");
            yield return Say("Dave", "You came back.");
            yield return Say("You", "I don't want to be someone who quits just because something is difficult.");
            yield return Say("Dave", "Then don't. Just remember, you don't have to spend every waking hour on the court to prove you care about it.");
            yield return Say("You", "You sound like Steve now.");
            yield return Say("Dave", "Please don't insult me like that.");
            while (dave.IsWalking) yield return null;
            Cut("Junction");
            dave.LookAt(player);
            yield return Say("Dave", "Same time next session?");
            yield return Say("You", "I'll be there.");
            yield return Say("Dave", "Good. Don't forget our little competition.");
            yield return Say("You", "I was hoping you'd forget.");
            yield return Say("Dave", "Not a chance.");
            dave.Gesture("Wave");
            dave.WalkTo(junction.position + new Vector3(0f, 0f, -9f));
            yield return Narrate("He heads off, waving without looking back.");
            yield return Wait(1f);
            yield return FadeOut(1f);
        }

        // ---------- Day 2: the morning gym ----------

        IEnumerator MorningGym()
        {
            dave.gameObject.SetActive(false);
            yield return ui.TitleCard("7:40 AM", "Northside Fitness", 2f);
            PlaceAt(gymDoor.position, gymDoor.rotation);
            Cut("GymWide");
            yield return FadeIn(0.9f);
            yield return Narrate("Rubber floors, a mirror wall, someone grunting at a squat rack. It smells like metal and other people's effort.");
            yield return Think("If he's putting in that much work, I can't just sit around.");
            yield return WalkPlayer(gymSpot.position);
            player.rotation = gymSpot.rotation;
            Cut("Gym");
            yield return Narrate("Back squats, curls, power cleans. Moving a barbell feels nothing like moving on sand.");
            yield return Narrate("Every exercise is a timed rep: press when the marker is in the green. Tired legs make the window smaller.");
            GymResult res = default;
            yield return gym.Run(GymKind.Morning, playerActor, r => res = r);
            SetFlag("ch3_gym_d2", res.points.ToString());
            Cut("GymMirror");
            playerActor.Gesture("Sigh");
            yield return Narrate("By the end, his legs feel heavy.");
            yield return Say("You", "Okay. Maybe I underestimated that.");
            yield return Narrate("He checks the time. There's still a volleyball session with Steve later today.");
            yield return Think("I can still make it. I just need to push through.");
            ui.SetEnergy(null);
            yield return FadeOut(1f);
        }

        // ---------- Day 3: conditioning instead of the gathering ----------

        IEnumerator Conditioning()
        {
            dave.gameObject.SetActive(false);
            yield return ui.TitleCard("6:30 PM", "Northside Fitness", 2f);
            PlaceAt(gymDoor.position, gymDoor.rotation);
            Cut("GymWide");
            yield return FadeIn(0.9f);
            yield return Narrate("The gym again. Busier in the evening. Somewhere down the beach, Dave and the others are getting food.");
            yield return Think("I said I'd do the work. This is the work.");
            yield return WalkPlayer(gymSpot.position);
            player.rotation = gymSpot.rotation;
            Cut("Gym");
            GymResult res = default;
            yield return gym.Run(GymKind.Conditioning, playerActor, r => res = r);
            SetFlag("ch3_gym_d3", res.points.ToString());
            Cut("GymMirror");
            playerActor.Gesture("Sigh");
            yield return Narrate(skills.Fatigue >= Chapter3Rules.ExhaustedAt
                ? "His legs are shaking on the walk to the door. That's new. He's not sure it's the good kind of new."
                : "Harder than the morning session. He leaves with his legs heavy and his phone face down in the bag.");
            ui.SetEnergy(null);
            yield return FadeOut(1f);
        }

        // ---------- Day 3: food with Dave, Alex and Nina ----------

        IEnumerator Group()
        {
            Sun(new Vector3(5f, 262f, 0f), new Color(1f, 0.58f, 0.38f), 0.9f, new Color(0.7f, 0.55f, 0.55f));
            if (foodLight != null) foodLight.gameObject.SetActive(true);
            alex.gameObject.SetActive(true);
            nina.gameObject.SetActive(true);
            SitNpc(dave, foodSeatDave.position, foodSeatDave.rotation);
            SitNpc(alex, foodSeatAlex.position, foodSeatAlex.rotation);
            SitNpc(nina, foodSeatNina.position, foodSeatNina.rotation);
            SitPlayer(foodSeatYou.position, foodSeatYou.rotation);
            yield return ui.TitleCard("7:15 PM", "The food truck by the promenade", 2f);
            Cut("FoodWide");
            yield return FadeIn(1f);
            yield return Narrate("Fries in paper boats, string lights coming on, the sea going grey-blue. Marlon isn't here. For once, nobody is counting anything.");
            Cut("Food");
            dave.LookAt(alex.transform);
            yield return Say("Dave", "So the ball was going one way, Alex went the other way, and somehow I ended up apologizing to the net.");
            yield return Say("Alex", "You hit the net.");
            yield return Say("Dave", "I was apologizing for its suffering.");
            nina.Gesture("Laugh");
            playerActor.Gesture("Laugh");
            yield return Say("Nina", "He did. Out loud. Steve had to sit down.");
            yield return Say("Dave", "My first week I wore running shoes on sand. That's the embarrassing story. The net thing is just Tuesday.");
            yield return Say("Alex", "He still doesn't follow his own advice, by the way.");
            yield return Say("Dave", "I give excellent advice. Receiving it is a different skill.");

            alex.LookAt(player);
            yield return Say("Alex", "How are you finding the club?");
            yield return Say("You", "It's harder than I expected.");
            yield return Say("Alex", "That's normal. The hard part is finding a rhythm you can keep.");
            yield return Say("Dave", "Exactly. Some of us are still working on the 'rhythm' part.");
            yield return Say("Alex", "We know.");
            yield return Say("Alex", "People assume you have to be the best player to be useful on a team.");
            yield return Say("You", "And you don't?");
            yield return Say("Alex", "You have to know what the team needs. Sometimes that's the best attack. Sometimes it's covering a teammate who's struggling.");
            yield return Say("Dave", "Sometimes it's stopping me from trying something stupid.");
            yield return Say("Alex", "That's actually a full-time job.");
            Relationship("alex", 1);

            // Dave walks him to the promenade.
            yield return FadeOut(0.8f);
            foreach (var a in new[] { dave, alex, nina }) a.Sit(false);
            playerActor.Sit(false);
            alex.gameObject.SetActive(false);
            nina.gameObject.SetActive(false);
            if (motor != null) motor.enabled = true;
            PlaceAt(junction.position + new Vector3(-6f, 0f, 0f), junction.rotation);
            Put(dave, junction.position + new Vector3(-6f, 0f, -0.9f), junction.position);
            Cut("Junction");
            yield return FadeIn(0.8f);
            dave.WalkTo(junction.position + new Vector3(-0.6f, 0f, -0.9f));
            yield return WalkPlayer(junction.position + new Vector3(-0.6f, 0f, 0f), 1.4f);
            while (dave.IsWalking) yield return null;
            dave.LookAt(player);
            yield return Say("Dave", "I'm glad you came.");
            yield return Say("You", "I almost didn't.");
            yield return Say("Dave", "I figured. Just don't disappear completely, okay? We're friends outside the court, too.");
            yield return Say("You", "I know. I'll try to make time.");
            yield return Say("Dave", "That's all I'm asking.");
            dave.Gesture("Wave");
            dave.WalkTo(junction.position + new Vector3(0f, 0f, -9f));
            yield return Wait(1.6f);
            yield return FadeOut(1f);
        }
    }
}
