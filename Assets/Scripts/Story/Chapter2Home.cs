using System.Collections;
using System.Collections.Generic;
using BeachVolley.Skills;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Chapter 2 at home: the morning before the first session (Dave's text, the ride), and the
    /// evening after each of the five training days. Every evening is different (a call from Mom,
    /// the final on TV seen with new eyes, sore muscles, reading up on signals) and each one
    /// ends at the laptop, where the day's development points are spent. Day 5's evening is the
    /// chapter ending.
    /// </summary>
    public class Chapter2Home : StoryDirector
    {
        [SerializeField] ScriptedPlay tvMatch;
        [SerializeField] GameObject tvScreen;
        [SerializeField] Transform bedSpot, bedSeat, doorSpot;
        [SerializeField] GameObject bagOnFloor, bagByDoor;
        [SerializeField] Interactable bag, bottle, phone, tv, door, laptop, bed;
        [SerializeField] Light ceilingLight;
        [Header("The ride to the club")]
        [SerializeField] Transform travelCar, travelBike;
        [SerializeField] StoryActor rider;

        protected override int ChapterNumber => 2;

        int day;

        protected override IEnumerator Run()
        {
            ui.SetBlack(true);
            if (rider != null) rider.Sit(true);
            string cp = StoryState.CheckpointName;
            if (cp != null && cp.StartsWith("home2_d") && int.TryParse(cp.Substring(7), out day))
            {
                day = Mathf.Clamp(day, 1, 5);
                yield return Evening();
            }
            else yield return Morning();
        }

        void PhoneUp(bool up)
        {
            if (playerActor == null) return;
            if (up)
            {
                playerActor.Hold(phone.transform, new Vector3(0.05f, 0.08f, 0.03f), new Vector3(0f, 90f, 0f));
                playerActor.Gesture("Texting");
            }
            else
            {
                playerActor.EndPose("Texting");
                playerActor.Release();
            }
        }

        void Scoreboard(string score) => ui.SetObjective("COASTAL OPEN  ·  FINAL\n" + score);

        // ---------- The morning before Day 1 ----------

        IEnumerator Morning()
        {
            yield return ui.TitleCard("Chapter 2", "The Sand Never Forgets", 2.8f);
            yield return ui.TitleCard("Two days later", "3:40 PM", 2f);
            SitPlayer(bedSeat);
            bagByDoor.SetActive(false);
            bagOnFloor.SetActive(true);
            tvScreen.SetActive(false);
            if (ceilingLight != null) ceilingLight.enabled = false;
            Cut("Room");
            yield return FadeIn(1.2f);
            yield return Narrate("His calves still ache from the first session. He's been walking down stairs sideways for two days.");

            PhoneUp(true);
            ui.ShowPhone(true, "Messages");
            ui.PhoneBubble("you alive?", false, "Dave");
            yield return Wait(0.7f);
            ui.PhoneBubble("training at 5. bring your legs. both of them", false, "Dave");
            yield return Wait(0.5f);
            yield return ui.PhoneChoose("“Barely. But yes.”", "“Wouldn't miss it.”", "“Which legs? I have none left.”");
            string[] replies = { "Barely. But yes.", "Wouldn't miss it.", "Which legs? I have none left." };
            ui.PhoneBubble(replies[Choice], true);
            SetFlag("ch2_morning_reply", Choice.ToString());
            yield return Wait(0.8f);
            ui.PhoneBubble(Choice == 1 ? "that's the spirit. the spirit is all you'll have by 7pm" : "perfect. steve loves a challenge", false, "Dave");
            yield return Wait(0.8f);
            yield return ui.PhoneChoose("Close");
            ui.ShowPhone(false);
            PhoneUp(false);

            // Getting ready, quicker than the first time.
            yield return StandPlayer();
            cam.Track(Shot("Room"), player);
            bag.prompt = "Pack the bag";
            bottle.prompt = "Fill the water bottle";
            tv.prompt = "Check the TV";
            door.prompt = "Head to the club";
            door.Available = false;
            var items = new List<Interactable> { bag, bottle, tv, door };
            yield return Explore(items, Use, () => door.Used, "Get ready\n<size=16>WASD to walk  ·  E to use</size>");

            IEnumerator Use(Interactable it)
            {
                switch (it.id)
                {
                    case "bag":
                        playerActor.Gesture("PickUp");
                        yield return Narrate("He opens the bag. Sand from last time pours out of every pocket. Of course it does.");
                        yield return Think("The sand never forgets.");
                        bag.Available = false;
                        bagOnFloor.SetActive(false);
                        break;
                    case "bottle":
                        playerActor.Gesture("Drink");
                        yield return Narrate("Two bottles this time. He learned that lesson.");
                        bottle.Available = false;
                        break;
                    case "tv":
                        yield return Narrate("A cooking show. He turns it off. Not today.");
                        tv.Available = false;
                        break;
                }
                if (bag.Used && bottle.Used) door.Available = true;
            }

            bool bike = StoryState.Is("transport", "bike");
            yield return Think(bike ? "Bike again. My legs will hate me. They hate me anyway." : "Car. The legs need every bit of help they can get.");
            yield return FadeOut(1f);
            yield return Ride(bike);
            yield return ui.TitleCard("4:52 PM", "Coastline Beach Club", 2f);
            yield return GoToScene(ClubScene, "club2_d1");
        }

        IEnumerator Ride(bool bike)
        {
            var vehicle = bike ? travelBike : travelCar;
            if (vehicle == null) yield break;
            travelCar.gameObject.SetActive(!bike);
            travelBike.gameObject.SetActive(bike);
            const float duration = 6f;
            float speed = bike ? 6.5f : 12f;
            Vector3 start = vehicle.position;
            Vector3 dir = vehicle.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            StartCoroutine(Drive(vehicle, start, dir * speed, duration));
            // From the other side of the road this time, low, looking back along it.
            StartCoroutine(cam.Dolly(start + dir * 18f - side * 5f + Vector3.up * 1.2f,
                start + dir * (speed * duration * 0.9f) - side * 6f + Vector3.up * 1.6f,
                vehicle, Vector3.up, duration, 44f));
            yield return FadeIn(0.8f);
            yield return ui.TitleCard(bike ? "Same road. Heavier legs." : "Same road. More nerves than last time — the good kind.", "", 2.2f);
            yield return Wait(1f);
            yield return FadeOut(1f);
        }

        static IEnumerator Drive(Transform vehicle, Vector3 start, Vector3 velocity, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                vehicle.position = start + velocity * t;
                yield return null;
            }
        }

        // ---------- Evenings ----------

        static readonly HashSet<SkillType>[] NewSkills =
        {
            new HashSet<SkillType> { SkillType.Bump, SkillType.CourtAwareness, SkillType.Athleticism },
            new HashSet<SkillType> { SkillType.Set, SkillType.Spike },
            new HashSet<SkillType> { SkillType.Serve, SkillType.Roll, SkillType.Dive, SkillType.Stamina },
            new HashSet<SkillType> { SkillType.Block, SkillType.CourtAwareness },
            new HashSet<SkillType>(),
        };

        IEnumerator Evening()
        {
            PlaceAt(doorSpot);
            bagByDoor.SetActive(true);
            bagOnFloor.SetActive(false);
            tvMatch.StopLoop();
            tvScreen.SetActive(false);
            if (ceilingLight != null) ceilingLight.enabled = true;
            Cut("Room");
            yield return FadeIn(1f);

            yield return Narrate(day switch
            {
                1 => "Home. He drops the bag by the door and his forearms remind him, again, what a hundred passes feel like.",
                2 => "Home. His shoulder is warm from swinging, and for once his head is louder than his legs.",
                3 => "Home. He gets through the door and leans on it for a while. Everything hurts in a new place.",
                4 => "Home. The wind rattles the window. He can still see Dave's hand behind his back — one finger, two fingers.",
                _ => "Home. It's dark. He sits on the floor by the door for a long minute before he can make himself get up.",
            });
            if (day == 5) { yield return Ending(); yield break; }
            playerActor.Gesture(day == 3 ? "Tired_Idle" : "Stretch");
            if (day == 3) { yield return Wait(1.2f); playerActor.EndPose("Tired_Idle"); }

            // Free roam: things to do tonight; bed once the points are spent.
            cam.Track(Shot("Room"), player);
            bool developed = false;
            laptop.prompt = "Open Player Development";
            bed.prompt = "Go to sleep";
            bed.Available = false;
            door.Available = false;
            SetupEvening();
            var items = new List<Interactable> { laptop, phone, tv, bag, bottle, bed };
            yield return Explore(items, UseEvening, () => bed.Used,
                "Evening\n<size=16>Spend your development points at the laptop  ·  then go to sleep</size>");

            IEnumerator UseEvening(Interactable it)
            {
                if (it == laptop)
                {
                    yield return Laptop();
                    developed = true;
                    bed.Available = true;
                    laptop.prompt = "Look at your development again";
                }
                else if (it == bed) yield break;
                else yield return EveningUse(it);
                if (developed && !lateText) yield return LateText();
            }

            // Sleep, then the next session.
            if (ceilingLight != null) ceilingLight.enabled = false;
            SitPlayer(bedSeat);
            yield return Narrate(day switch
            {
                1 => "He's asleep before he finishes thinking about the next session.",
                2 => "In the dark he goes through the approach in his head: left, right-left, up. Left, right-left, up.",
                3 => "He sets an alarm, then sets a second one. His legs vote against both.",
                _ => "He falls asleep with one finger, two fingers going round in his head.",
            });
            yield return FadeOut(1.4f);
            yield return ui.TitleCard(NextDayTitle(day + 1), NextDaySub(day + 1), 2.2f);
            yield return GoToScene(ClubScene, "club2_d" + (day + 1));
        }

        static string NextDayTitle(int d) => d switch { 2 => "Two days later", 3 => "Saturday", 4 => "Tuesday", _ => "Thursday" };
        static string NextDaySub(int d) => d switch { 2 => "8:30 AM", 3 => "12:00 PM", 4 => "6:15 PM", _ => "5:30 PM" };

        bool lateText;

        /// <summary>Per-evening props: what the TV, phone, bag and bottle offer tonight.</summary>
        void SetupEvening()
        {
            switch (day)
            {
                case 1:
                    tv.prompt = "Turn on the TV";
                    phone.prompt = "Phone (missed call)";
                    bag.Available = bottle.Available = false;
                    break;
                case 2:
                    tv.prompt = "Watch the final again";
                    phone.prompt = "Phone";
                    bag.Available = bottle.Available = false;
                    break;
                case 3:
                    tv.Available = false;
                    phone.prompt = "Phone";
                    bag.prompt = "Ice pack from the bag";
                    bottle.prompt = "Drink some water";
                    break;
                default:
                    tv.Available = false;
                    phone.prompt = "Read up on blocking signals";
                    bag.Available = false;
                    bottle.prompt = "Drink some water";
                    break;
            }
        }

        IEnumerator EveningUse(Interactable it)
        {
            switch (day)
            {
                case 1:
                    if (it == phone) yield return MomCall();
                    else if (it == tv)
                    {
                        tvScreen.SetActive(true);
                        yield return Narrate("A highlights show. The commentator keeps saying \"what a platform\".");
                        yield return Think("I know what that means now. I've got the bruises to prove it.");
                        tv.Available = false;
                    }
                    break;
                case 2:
                    if (it == tv) yield return TvWithNewEyes();
                    else if (it == phone)
                    {
                        PhoneUp(true);
                        ui.ShowPhone(true, "Messages");
                        ui.PhoneBubble("practised setting against my bedroom wall", false, "Dave");
                        ui.PhoneBubble("the neighbour knocked. not on the door. on the wall", false, "Dave");
                        string reply = StoryState.Is("blame_style", "blame") ? "Was it glued to the wall too?" : "Did the wall set it back?";
                        yield return ui.PhoneChoose(reply, "“Tell your neighbour it's for the team.”");
                        ui.PhoneBubble(Choice == 0 ? reply : "Tell your neighbour it's for the team.", true);
                        yield return Wait(0.8f);
                        ui.PhoneBubble(Choice == 0 ? "rude. accurate, but rude" : "\"the team\". look at you", false, "Dave");
                        yield return Wait(0.8f);
                        yield return ui.PhoneChoose("Close");
                        ui.ShowPhone(false);
                        PhoneUp(false);
                        phone.Available = false;
                    }
                    break;
                case 3:
                    if (it == bag)
                    {
                        playerActor.Gesture("PickUp");
                        yield return Narrate("He holds an ice pack on one shoulder, then the other, then gives up and lies on the floor with it on his stomach.");
                        yield return Think("Recovery. Steve said recovery.");
                        bag.Available = false;
                    }
                    else if (it == bottle)
                    {
                        playerActor.Gesture("Drink");
                        yield return Narrate("A whole bottle, in one go. He didn't drink nearly enough today.");
                        bottle.Available = false;
                    }
                    else if (it == phone)
                    {
                        PhoneUp(true);
                        ui.ShowPhone(true, "Messages");
                        bool pancake = StoryState.Is("ch2_pancake", "yes");
                        ui.PhoneCard("Dave sent a photo", pancake
                            ? "A stack of pancakes with a volleyball balanced on top.\n\n\"in honour of today. you owe me a real one\""
                            : "A stack of pancakes with a volleyball balanced on top.\n\n\"next time. the sand will remember\"");
                        yield return Wait(0.5f);
                        yield return ui.PhoneChoose(pancake ? "“Breakfast on me. Next session.”" : "“The sand remembers ME. My mouth is full of it.”");
                        ui.PhoneBubble(pancake ? "Breakfast on me. Next session." : "The sand remembers ME. My mouth is full of it.", true);
                        yield return Wait(0.8f);
                        yield return ui.PhoneChoose("Close");
                        ui.ShowPhone(false);
                        PhoneUp(false);
                        phone.Available = false;
                    }
                    break;
                default:
                    if (it == phone)
                    {
                        PhoneUp(true);
                        ui.ShowPhone(true, "Search: beach volleyball block signals");
                        ui.PhoneCard("Hand signals, explained", "Signals are given behind the back by the player at the net, one hand per side.\n1 finger: block the line.  2 fingers: block the angle (cross).\nA fist: no block — both players defend.");
                        ui.PhoneCard("Comment", "\"Took me a whole season to stop looking at my partner's hand DURING the rally lol\"");
                        yield return Wait(0.5f);
                        yield return ui.PhoneChoose("Close");
                        ui.ShowPhone(false);
                        PhoneUp(false);
                        yield return Think("A fist means no block. Steve didn't tell us that one. Saving it for later, probably.");
                        phone.Available = false;
                    }
                    else if (it == bottle)
                    {
                        playerActor.Gesture("Drink");
                        yield return Narrate("He drinks, and watches the palm outside bend in the wind.");
                        bottle.Available = false;
                    }
                    break;
            }
        }

        IEnumerator MomCall()
        {
            PhoneUp(true);
            ui.ShowPhone(true, "Mom");
            ui.PhoneCard("Missed call", "Mom (2)");
            yield return ui.PhoneChoose("Call back");
            ui.PhoneClear("Mom  ·  00:04");
            yield return Wait(0.5f);
            ui.ShowPhone(false);
            yield return Say("Mom", "There you are! So? Did you break anything?");
            yield return Choose("“Only my pride.”", "“It was actually really good.”");
            if (Choice == 0)
            {
                yield return Say("Mom", "Pride grows back. Did you eat?");
                yield return Say("You", "…Define \"eat\".");
                yield return Say("Mom", "I'm sending you leftovers.");
            }
            else
            {
                SetFlag("told_mom", "good");
                yield return Say("Mom", "Really? You sound different. Happy-different.");
                yield return Say("You", "There's this guy, Dave. And the coach. They make it feel… possible.");
                yield return Say("Mom", "Then keep going. And eat something.");
            }
            PhoneUp(false);
            phone.Available = false;
        }

        /// <summary>The same final he watched in Chapter 1, but now he watches the setter.</summary>
        IEnumerator TvWithNewEyes()
        {
            SitPlayer(bedSeat);
            yield return Blend("TV", 1.4f);
            tvScreen.SetActive(true);
            tvMatch.Score += Scoreboard;
            yield return Narrate("The final again. He knows every second of the last point by now.");
            yield return FadeOut(0.4f);
            Cut("Broadcast");
            yield return FadeIn(0.4f);
            yield return tvMatch.WinningPoint();
            ui.SetObjective(null);
            tvMatch.Score -= Scoreboard;
            yield return FadeOut(0.4f);
            Cut("Player");
            yield return FadeIn(0.4f);
            yield return Narrate("But this time he doesn't watch the spike. He watches the set.");
            yield return Think("Off the net. Right in front of him. He didn't even have to look. That's why the spike looked easy.");
            SetFlag("noticed_setter", "yes");
            tv.Available = false;
            yield return StandPlayer();
            cam.Track(Shot("Room"), player);
        }

        IEnumerator Laptop()
        {
            ui.SetObjective(null);
            yield return Narrate(BarkPool.Next(
                "He opens the club's training app. The session is already logged — every drill, every rep.",
                "The training app again. Today's points are waiting.",
                "He opens the laptop. The fan whirs. Today's points are there.",
                "One more look at the development screen."));
            if (day == 1)
                yield return Think("Points earned on the court, spent here. And the court decides if I chose right.");
            string note = Flag("dev_note");
            yield return ui.Development(skills, note, true, NewSkills[Mathf.Clamp(day - 1, 0, 4)]);
            StoryState.StoreSkills(skills);
            StoryState.Save();
        }

        IEnumerator LateText()
        {
            lateText = true;
            yield return Wait(0.6f);
            PhoneUp(true);
            ui.ShowPhone(true, "Messages");
            switch (day)
            {
                case 1:
                    ui.PhoneBubble("same time next session? my legs said no but I overruled them", false, "Dave");
                    yield return ui.PhoneChoose("“Overrule mine too.”");
                    ui.PhoneBubble("Overrule mine too.", true);
                    break;
                case 2:
                    ui.PhoneBubble("Good session. Next one is about keeping the ball alive. Bring water. More than you think.", false, "Steve (Coach)");
                    yield return ui.PhoneChoose("“Will do, coach.”");
                    ui.PhoneBubble("Will do, coach.", true);
                    break;
                case 3:
                    ui.PhoneBubble("Alex says you've got good hands for someone who's been here a week. That's a compliment. He doesn't give many.", false, "Steve (Coach)");
                    yield return ui.PhoneChoose("“Tell him thanks.”", "“He's a week too late, I needed that at noon.”");
                    ui.PhoneBubble(Choice == 0 ? "Tell him thanks." : "He's a week too late, I needed that at noon.", true);
                    break;
                default:
                    ui.PhoneBubble("Good session. You're starting to see the game, not just the ball. Rest your legs.", false, "Steve (Coach)");
                    yield return Wait(0.8f);
                    ui.PhoneBubble("I showed my sister the signals. she showed me a fist. i don't think that's a signal", false, "Dave");
                    yield return ui.PhoneChoose("“A fist means no block. I read it.”", "“I think that one's just for you.”");
                    ui.PhoneBubble(Choice == 0 ? "A fist means no block. I read it." : "I think that one's just for you.", true);
                    break;
            }
            yield return Wait(0.8f);
            yield return ui.PhoneChoose("Close");
            ui.ShowPhone(false);
            PhoneUp(false);
        }

        // ---------- Chapter 2 ending ----------

        IEnumerator Ending()
        {
            yield return Narrate("He's thinking about the passing drills. The missed calls. The signals. The game with Dave.");
            // The final allocation, at the desk.
            cam.Track(Shot("Room"), player);
            laptop.prompt = "Open Player Development";
            var items = new List<Interactable> { laptop };
            yield return Explore(items, it => Laptop(), () => laptop.Used, "Open the laptop");

            SitPlayer(bedSeat);
            Cut("Player");
            yield return Narrate("He looks at the skill screen one last time. Then he closes the laptop and sits on the edge of the bed.");
            yield return Narrate("He remembers Marlon's words, and how small he felt in that game. For a moment he wonders whether he really belongs at the club.");

            // What he actually remembers depends on what happened.
            int chain = int.TryParse(Flag("ch2_best_chain"), out int c) ? c : 0;
            yield return Narrate(chain >= 5
                ? $"Then he remembers the rally he and Dave kept alive — {chain} times over the net, laughing by the end."
                : "Then he remembers the rally he and Dave kept alive. It wasn't long. It didn't matter.");
            yield return Narrate(StoryState.Is("ch2_pancake", "yes")
                ? "He remembers the pancake — the ball bouncing off the back of his hand, Dave's face."
                : "He remembers the pancake he didn't get, and how close the last one was.");
            yield return Narrate(StoryState.Is("ch2_dave_match", "won")
                ? "He remembers the last point against Marlon and Alex, and Dave grabbing his shoulders."
                : "He remembers losing to Marlon and Alex with Dave, and how it didn't feel like losing alone.");
            yield return Narrate("He remembers how much easier it felt to move once he started reading the ball instead of chasing it.");
            yield return Think("I'm nowhere near as good as them.");
            yield return Wait(0.4f);
            yield return Think("But I'm better than I was five days ago.");

            PhoneUp(true);
            ui.ShowPhone(true, "Messages");
            ui.PhoneBubble("Next session. Don't be late.", false, "Dave");
            yield return Wait(0.6f);
            yield return ui.PhoneChoose("Smile.");
            ui.ShowPhone(false);
            PhoneUp(false);
            playerActor.Gesture("Happy");
            yield return Think("I'll be there.");

            yield return Blend("PullAway", 4f);
            yield return FadeOut(2f);
            if (ceilingLight != null) ceilingLight.enabled = false;
            yield return ui.TitleCard("CHAPTER COMPLETE", "2.  THE SAND NEVER FORGETS\n\nLearning to play is one thing. Learning to play together is another.", 6f);
            yield return ui.TitleCard("COMING NEXT:  3. The Price",
                "Training gets harder. Gym sessions, conditioning, recovery, and a life outside the court all compete for his time. His partnership with Dave takes shape — and Marlon's standards become harder to ignore.", 7f);
            StoryState.Chapter = 3;
            yield return GoToScene(MainMenuScene, "ch3_start");
        }
    }
}
