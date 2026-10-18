using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Chapter 3 at home. The chapter opening, the mornings that set up a choice (Day 2: Marlon's
    /// gym post and how to answer it; Day 3: Dave's invitation), the evenings after each day
    /// (what the day cost, messages that depend on the route and on Dave's band, the laptop, sleep)
    /// and the chapter ending on Day 5. Fatigue is carried in the save: sleep and rest bring it
    /// down, and every choice shows where it would leave him before he picks.
    /// </summary>
    public class Chapter3Home : StoryDirector
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
        [SerializeField] GymSession gym;

        protected override int ChapterNumber => 3;

        int day;

        protected override IEnumerator Run()
        {
            ui.SetBlack(true);
            if (rider != null) rider.Sit(true);
            if (skills != null) skills.GymUnlocked = true;
            string cp = StoryState.CheckpointName ?? "";
            if (cp.StartsWith("home3_m") && int.TryParse(cp.Substring(7), out day))
            {
                if (day == 2) yield return Morning2();
                else yield return Morning3();
            }
            else if (cp.StartsWith("home3_d") && int.TryParse(cp.Substring(7), out day))
            {
                day = Mathf.Clamp(day, 1, 5);
                if (day == 5) yield return Ending();
                else yield return Evening();
            }
            else yield return Opening();
        }

        // ---------- Helpers ----------

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

        IEnumerator ClosePhone()
        {
            yield return Wait(0.6f);
            yield return ui.PhoneChoose("Close");
            ui.ShowPhone(false);
            PhoneUp(false);
        }

        float Fatigue => skills.Fatigue;

        void Recover(float amount)
        {
            skills.Rest(amount);
            StoryState.StoreSkills(skills);
        }

        IEnumerator Ride()
        {
            bool bike = StoryState.Is("transport", "bike");
            var vehicle = bike ? travelBike : travelCar;
            if (vehicle == null) yield break;
            travelCar.gameObject.SetActive(!bike);
            travelBike.gameObject.SetActive(bike);
            const float duration = 5f;
            float speed = bike ? 6.5f : 12f;
            Vector3 start = vehicle.position;
            Vector3 dir = vehicle.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            StartCoroutine(Drive(vehicle, start, dir * speed, duration));
            StartCoroutine(cam.Dolly(start - side * 4f + Vector3.up * 1.4f,
                start + dir * (speed * duration * 0.8f) - side * 5f + Vector3.up * 2f,
                vehicle, Vector3.up, duration, 46f));
            yield return FadeIn(0.8f);
            yield return Wait(duration - 1.6f);
            yield return FadeOut(0.8f);
        }

        static IEnumerator Drive(Transform vehicle, Vector3 start, Vector3 velocity, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                vehicle.position = start + velocity * t;
                yield return null;
            }
        }

        void RoomEvening(bool lightOn)
        {
            PlaceAt(doorSpot);
            bagByDoor.SetActive(true);
            bagOnFloor.SetActive(false);
            tvMatch.StopLoop();
            tvScreen.SetActive(false);
            if (ceilingLight != null) ceilingLight.enabled = lightOn;
            Cut("Room");
        }

        // ---------- Chapter opening ----------

        IEnumerator Opening()
        {
            yield return ui.TitleCard("Chapter 3", "The Price", 2.8f);
            yield return ui.TitleCard("", "“You can train harder. You can get stronger. But you can't give everything to everything.”", 3.5f);
            // A new chapter starts rested; the gym opens.
            skills.SetFatigue(0f);
            skills.GymUnlocked = true;
            // A replayed chapter starts its choices over.
            foreach (var key in new[] { "ch3_cafe", "ch3_cafe_talk", "ch3_d2", "ch3_challenge", "ch3_d3", "ch3_d3_reply",
                "ch3_missed_invites", "ch3_d4", "ch3_studied", "ch3_eval", "ch3_eval_condition", "marlon_limits" })
                SetFlag(key, "");
            StoryState.StoreSkills(skills);
            StoryState.Save();

            yield return ui.TitleCard("A week later", "3:50 PM", 2f);
            SitPlayer(bedSeat);
            bagByDoor.SetActive(false);
            bagOnFloor.SetActive(true);
            tvScreen.SetActive(false);
            if (ceilingLight != null) ceilingLight.enabled = false;
            Cut("Room");
            yield return FadeIn(1.2f);
            yield return Narrate("The bruises on his forearms have faded to yellow. The calendar on his phone has started filling up.");
            PhoneUp(true);
            ui.ShowPhone(true, "Messages");
            ui.PhoneBubble("New this week: conditioning is part of the program. The gym down the road gives club members a discount.", false, "Steve (Coach)");
            yield return Wait(0.6f);
            ui.PhoneBubble("Ask me before you overdo it. I mean that.", false, "Steve (Coach)");
            yield return Wait(0.4f);
            yield return ui.PhoneChoose("“Got it, coach.”", "“How much is overdoing it?”");
            ui.PhoneBubble(Choice == 0 ? "Got it, coach." : "How much is overdoing it?", true);
            yield return Wait(0.8f);
            ui.PhoneBubble(Choice == 0 ? "Good. See you at five." : "Usually a bit less than you think. See you at five.", false, "Steve (Coach)");
            yield return ClosePhone();
            yield return Think("Volleyball, the gym, actual friends. When did my week get this full?");

            yield return StandPlayer();
            cam.Track(Shot("Room"), player);
            bag.prompt = "Pack the bag";
            door.prompt = "Head to the club";
            door.Available = false;
            bottle.Available = tv.Available = laptop.Available = bed.Available = phone.Available = false;
            var items = new List<Interactable> { bag, door };
            yield return Explore(items, Use, () => door.Used, "Get ready\n<size=16>WASD to walk  ·  E to use</size>");

            IEnumerator Use(Interactable it)
            {
                if (it == bag)
                {
                    playerActor.Gesture("PickUp");
                    yield return Narrate("Shoes, two bottles, a towel. And at the bottom, the gym card he hasn't used yet.");
                    bag.Available = false;
                    bagOnFloor.SetActive(false);
                    door.Available = true;
                }
            }

            yield return FadeOut(1f);
            yield return Ride();
            yield return ui.TitleCard("4:55 PM", "Coastline Beach Club", 2f);
            yield return GoToScene(ClubScene, "club3_d1");
        }

        // ---------- Day 2 morning: Marlon's post ----------

        IEnumerator Morning2()
        {
            yield return ui.TitleCard("The next morning", "7:15 AM", 2f);
            SitPlayer(bedSeat);
            bagByDoor.SetActive(true);
            bagOnFloor.SetActive(false);
            tvScreen.SetActive(false);
            if (ceilingLight != null) ceilingLight.enabled = false;
            Cut("Player");
            yield return FadeIn(1f);
            ui.SetEnergy(Fatigue);
            yield return Narrate("His phone buzzes before the alarm does.");
            PhoneUp(true);
            ui.ShowPhone(true, "Feed");
            ui.PhoneCard("marlon.trains  ·  posted 6:02 AM", "A photo from the gym, weights everywhere, a volleyball tucked under one arm.\n\n\"Nobody is coming to save you. Put in the work.\"");
            yield return Wait(0.8f);
            yield return ui.PhoneChoose("Open messages");
            ui.PhoneClear("Messages");
            ui.PhoneBubble("please tell me you're not at the gym at this hour", false, "Dave");
            yield return ui.PhoneChoose("“No. Why?”");
            ui.PhoneBubble("No. Why?", true);
            yield return Wait(0.6f);
            ui.PhoneBubble("marlon posted another one of his motivational speeches. wanted to make sure you hadn't joined his cult", false, "Dave");
            yield return ui.PhoneChoose("“Maybe he's right. Maybe I should be training more.”");
            ui.PhoneBubble("Maybe he's right. Maybe I should be training more.", true);
            yield return Wait(0.7f);
            ui.PhoneBubble("maybe. or maybe you should eat breakfast first", false, "Dave");
            yield return ClosePhone();
            yield return Think("He's already training?");
            yield return Narrate("Steve's session is at five. There's time for something before it — or for nothing at all.");

            float f = Fatigue;
            yield return Choose(
                $"Extra gym session before practice   (gym points · {Chapter3Rules.Preview(f, Chapter3Rules.GymMorning + 0.15f)})",
                $"Stick to the planned volleyball session   (steady · {Chapter3Rules.Preview(f, 0f)})",
                $"Take the morning to recover   (fresher for practice · {Chapter3Rules.Preview(f, -Chapter3Rules.RestDay)})");
            switch (Choice)
            {
                case 0:
                    SetFlag("ch3_d2", "gym");
                    yield return Think("If he's putting in that much work, I can't just sit around.");
                    playerActor.Gesture("PickUp");
                    yield return Narrate("He grabs the gym bag.");
                    yield return FadeOut(1f);
                    yield return GoToScene(TownScene, "town3_d2");
                    yield break;
                case 1:
                    SetFlag("ch3_d2", "normal");
                    yield return Think("Steve has a plan. Maybe the plan is the point.");
                    yield return Narrate("Breakfast. A walk. The usual bag, packed early.");
                    break;
                default:
                    SetFlag("ch3_d2", "rest");
                    Recover(Chapter3Rules.RestDay);
                    ui.SetEnergy(Fatigue);
                    yield return Think("Making sure I can train properly. That counts.");
                    yield return Narrate("He sleeps another hour, eats properly, stretches. By the afternoon his legs feel like his own again.");
                    break;
            }
            yield return FadeOut(1f);
            ui.SetEnergy(null);
            yield return Ride();
            yield return ui.TitleCard("4:50 PM", "Coastline Beach Club", 2f);
            yield return GoToScene(ClubScene, "club3_d2");
        }

        // ---------- Day 3 morning: the invitation ----------

        IEnumerator Morning3()
        {
            yield return ui.TitleCard("The following afternoon", "1:30 PM", 2f);
            SitPlayer(bedSeat);
            bagByDoor.SetActive(true);
            bagOnFloor.SetActive(false);
            if (ceilingLight != null) ceilingLight.enabled = false;
            Cut("Player");
            yield return FadeIn(1f);
            ui.SetEnergy(Fatigue);
            PhoneUp(true);
            ui.ShowPhone(true, "Messages");
            ui.PhoneBubble("a few of us are grabbing food later. come along?", false, "Dave");
            yield return Wait(0.6f);
            ui.PhoneBubble("Short session at 3. Optional conditioning slot at the gym after, if you want it.", false, "Steve (Coach)");
            yield return ui.PhoneChoose("“Tell you at practice.”");
            ui.PhoneBubble("Tell you at practice.", true);
            yield return Wait(0.6f);
            ui.PhoneBubble(Chapter3Rules.DaveBand() == Band.Low ? "ok" : "suspense. I respect it", false, "Dave");
            yield return ClosePhone();
            yield return Narrate("For the first time, the calendar feels crowded.");
            yield return Think("Volleyball, gym, friends… I can't fit everything into one day.");
            yield return FadeOut(1f);
            ui.SetEnergy(null);
            yield return Ride();
            yield return ui.TitleCard("2:55 PM", "Coastline Beach Club", 2f);
            yield return GoToScene(ClubScene, "club3_d3");
        }

        // ---------- Evenings ----------

        IEnumerator Evening()
        {
            string d3 = StoryState.Flag("ch3_d3");
            string d4 = StoryState.Flag("ch3_d4");
            if (day == 3 && d3 == "rest") Recover(Chapter3Rules.RestEvening);
            if (day == 4 && d4 == "rest") Recover(Chapter3Rules.RestEvening);
            if (day == 4 && d4 == "study") Recover(Chapter3Rules.StudyEvening);

            RoomEvening(true);
            yield return FadeIn(1f);
            ui.SetEnergy(Fatigue);
            var c = Chapter3Rules.ConditionOf(Fatigue);
            yield return Narrate(day switch
            {
                1 => StoryState.Is("ch3_cafe", "yes")
                    ? "Home, later than planned. His body is tired after training, but the evening has left him in a better mood."
                    : "Home early. He puts the bag down. His legs feel heavy, but his mind is still replaying the drills.",
                2 => StoryState.Is("ch3_d2", "gym")
                    ? "Home. His legs are still tired from the morning gym session and the afternoon on the sand."
                    : "Home. A normal day's tired — the kind that goes away with dinner.",
                3 => d3 == "group" ? "Home, smelling faintly of fries. He's smiling at nothing in particular."
                    : d3 == "gym" ? "Home. He drops the gym bag and sits on the floor next to it for a while."
                    : "Home before dark, for once. The quiet feels strange, then good.",
                _ => c == Condition.Fresh ? "Home. Tomorrow's evaluation is the only thing on his mind."
                    : "Home. Tomorrow is the evaluation, and his body is still arguing about today.",
            });
            ui.Toast("Condition: " + Chapter3Rules.Name(c), null, 2.4f);

            if (day == 4) yield return Prepare(d4);
            yield return EveningPhone();

            // Free roam: laptop, then bed.
            cam.Track(Shot("Room"), player);
            laptop.prompt = "Open Player Development";
            bed.prompt = "Go to sleep";
            bed.Available = false;
            door.Available = false;
            bag.Available = bottle.Available = tv.Available = false;
            phone.Available = false;
            bool developed = false;
            var items = new List<Interactable> { laptop, bed };
            yield return Explore(items, UseEvening, () => bed.Used,
                "Evening\n<size=16>Spend your points at the laptop  ·  then go to sleep</size>");

            IEnumerator UseEvening(Interactable it)
            {
                if (it != laptop) yield break;
                yield return Laptop();
                if (!developed) yield return AfterLaptop();
                developed = true;
                bed.Available = true;
                laptop.prompt = "Look at your development again";
            }

            // Sleep, then the next day.
            if (ceilingLight != null) ceilingLight.enabled = false;
            SitPlayer(bedSeat);
            yield return Narrate(day switch
            {
                1 => "He's asleep before he can decide whether that was a good day or a great one.",
                2 => "Tonight, he decides on a proper night's sleep instead of another workout.",
                3 => "He sets one alarm. Just one.",
                _ => "He goes over the evaluation in his head once, then makes himself stop.",
            });
            Recover(Chapter3Rules.Sleep);
            ui.SetEnergy(null);
            yield return FadeOut(1.4f);
            StoryState.StoreSkills(skills);
            switch (day)
            {
                case 1: yield return GoToScene(HomeScene, "home3_m2"); break;
                case 2: yield return GoToScene(HomeScene, "home3_m3"); break;
                case 3:
                    yield return ui.TitleCard("Day 4", "5:00 PM", 2f);
                    yield return GoToScene(ClubScene, "club3_d4");
                    break;
                default:
                    yield return ui.TitleCard("Day 5", "8:30 AM", 2f);
                    yield return GoToScene(ClubScene, "club3_d5");
                    break;
            }
        }

        /// <summary>Day 4: how he gets ready for the evaluation (chosen at the club).</summary>
        IEnumerator Prepare(string choice)
        {
            switch (choice)
            {
                case "study":
                    SitPlayer(bedSeat);
                    yield return Blend("TV", 1.2f);
                    tvScreen.SetActive(true);
                    yield return Narrate("A pro match, the full thing. He watches the players without the ball: where they stand, when they move, what they leave open.");
                    yield return FadeOut(0.4f);
                    Cut("Broadcast");
                    yield return FadeIn(0.4f);
                    yield return tvMatch.WinningPoint();
                    ui.SetObjective(null);
                    yield return FadeOut(0.4f);
                    Cut("Player");
                    yield return FadeIn(0.4f);
                    yield return Think("The best players aren't just reacting. They're already thinking about what happens next.");
                    skills.AddCredit(0.5f, "Match study");
                    SetFlag("ch3_studied", "yes");
                    ui.Toast("Tactical study: +½ development point", null, 2.4f);
                    tvScreen.SetActive(false);
                    yield return StandPlayer();
                    break;
                case "practice":
                    yield return Narrate("He takes a ball outside and passes against the garage wall until the streetlights come on.");
                    yield return gym.Run(GymKind.WallPasses, playerActor, null);
                    ui.SetEnergy(Fatigue);
                    StoryState.StoreSkills(skills);
                    yield return Think("I know Steve said to slow down. I just don't want to feel like I'm falling behind.");
                    break;
                default:
                    playerActor.Gesture("Drink");
                    yield return Narrate("He eats a real dinner, stretches for ten minutes, and puts the phone in a drawer.");
                    yield return Think("I wanted to do more today. Maybe the best thing I can do for tomorrow is stop here.");
                    break;
            }
            yield return Think("I thought commitment meant giving everything, every day.");
            yield return Think("Maybe it means knowing what I need to give each day.");
        }

        IEnumerator EveningPhone()
        {
            var band = Chapter3Rules.DaveBand();
            switch (day)
            {
                case 1:
                    if (!StoryState.Is("ch3_cafe", "yes")) yield break;
                    PhoneUp(true);
                    ui.ShowPhone(true, "Dave");
                    ui.PhoneBubble("Next session. Five clean attacks. Don't forget.", false, "Dave");
                    yield return ui.PhoneChoose("“I'll make you regret challenging me.”");
                    ui.PhoneBubble("I'll make you regret challenging me.", true);
                    yield return Wait(0.6f);
                    ui.PhoneBubble("that's the spirit haha", false, "Dave");
                    yield return ClosePhone();
                    break;
                case 2:
                    PhoneUp(true);
                    ui.ShowPhone(true, "Dave");
                    ui.PhoneBubble("Don't forget to eat and get some sleep. Steve will know if you show up looking like a zombie.", false, "Dave");
                    yield return ui.PhoneChoose("“You worry too much.”");
                    ui.PhoneBubble("You worry too much.", true);
                    yield return Wait(0.6f);
                    ui.PhoneBubble("someone has to", false, "Dave");
                    yield return ClosePhone();
                    break;
                case 3:
                    string d3 = StoryState.Flag("ch3_d3");
                    PhoneUp(true);
                    ui.ShowPhone(true, "Dave");
                    if (d3 == "gym")
                    {
                        ui.PhoneBubble(band == Band.Low ? "hope training went well" : "Hope training went well. We missed you today.", false, "Dave");
                        yield return ui.PhoneChoose("“Sorry I couldn't make it. Let's catch up soon.”", "“I'll try to make the next one.”", "“You guys survived without me, right?”");
                        string[] replies = { "Sorry I couldn't make it. Let's catch up soon.", "I'll try to make the next one.", "You guys survived without me, right?" };
                        ui.PhoneBubble(replies[Choice], true);
                        SetFlag("ch3_d3_reply", Choice.ToString());
                        yield return Wait(0.7f);
                        ui.PhoneBubble(Choice switch
                        {
                            0 => "deal",
                            1 => "holding you to that",
                            _ => "barely. alex tried to tell a joke",
                        }, false, "Dave");
                    }
                    else if (d3 == "rest")
                    {
                        ui.PhoneBubble("All good. Rest up. We'll see you next time.", false, "Dave");
                        yield return ui.PhoneChoose("“Next time. Promise.”");
                        ui.PhoneBubble("Next time. Promise.", true);
                    }
                    else
                    {
                        ui.PhoneCard("Dave sent a photo", "The empty picnic table, one fry left on it.\n\n\"the net sends its regards\"");
                        yield return ui.PhoneChoose("“Tell the net I'm sorry.”");
                        ui.PhoneBubble("Tell the net I'm sorry.", true);
                    }
                    yield return ClosePhone();
                    break;
            }
        }

        IEnumerator Laptop()
        {
            ui.SetObjective(null);
            // BarkPool.Next returns null once every line has been used: fall back to a plain one.
            yield return Narrate(BarkPool.Next(
                "He opens the training app. Today's points are waiting — and, for the first time, a Gym tab that isn't locked.",
                "The training app again. Today's points are waiting.",
                "One more look at the development screen.")
                ?? "He opens the laptop. Today's points are there.");
            var c = Chapter3Rules.ConditionOf(Fatigue);
            string note = $"Condition: {Chapter3Rules.Name(c)} — {Chapter3Rules.Effect(c)} It never lowers a skill level.";
            if (skills.GymPoints > 0) note += $"\nGym points waiting: {skills.GymPoints} (Gym tab — Q / E).";
            string dev = Flag("dev_note");
            if (!string.IsNullOrEmpty(dev)) note += "\n" + dev;
            yield return ui.Development(skills, note, true, null, skills.GymPoints > 0 && skills.DevPoints == 0 ? 1 : 0);
            StoryState.StoreSkills(skills);
            StoryState.Save();
        }

        IEnumerator AfterLaptop()
        {
            switch (day)
            {
                case 1:
                    if (StoryState.Is("ch3_cafe", "yes"))
                    {
                        yield return Think("I'm still nowhere near where I want to be. But maybe getting better isn't just about how many hours I spend training.");
                        yield return Think("Maybe it's also about the people who make me want to come back.");
                    }
                    else
                    {
                        yield return Think("I wanted to get better at volleyball. I didn't realize how much of my life it would start taking up.");
                        yield return Think("Still… today felt worth it.");
                    }
                    break;
                case 2:
                    if (StoryState.Is("ch3_d2", "gym"))
                    {
                        yield return Think("I wanted to do more today. I did more today.");
                        yield return Think("But did I actually get more out of it?");
                    }
                    else yield return Think("Steady. Maybe steady is allowed.");
                    break;
                case 3:
                    yield return Think(StoryState.Is("ch3_d3", "group")
                        ? "Alex is right. The hard part is finding a rhythm I can keep."
                        : "Every extra session has a cost. I'm starting to feel where it gets paid.");
                    break;
            }
        }

        // ---------- Chapter ending (Day 5 evening) ----------

        IEnumerator Ending()
        {
            RoomEvening(true);
            yield return FadeIn(1f);
            ui.SetEnergy(Fatigue);
            yield return Narrate("Evening. He comes home, puts the sports bag beside the desk, and sits down.");
            string eval = StoryState.Flag("ch3_eval", "average");
            yield return Narrate(eval switch
            {
                "strong" => "He keeps replaying the dig that turned into Dave's point. And Marlon's small, almost-smile.",
                "poor" => "He keeps replaying the points he didn't get to. And Steve: one session doesn't define your progress.",
                _ => "He keeps replaying the rallies that worked — and the ones that didn't, but almost did.",
            });

            cam.Track(Shot("Room"), player);
            laptop.prompt = "Open Player Development";
            bag.Available = bottle.Available = tv.Available = phone.Available = door.Available = bed.Available = false;
            var items = new List<Interactable> { laptop };
            yield return Explore(items, it => Laptop(), () => laptop.Used, "Open the laptop");

            SitPlayer(bedSeat);
            Cut("Player");
            yield return Narrate("He looks over the week's schedule, pinned by the desk. Full of sessions, reminders, crossed-out plans.");
            yield return Think("I thought getting better meant doing more. More training. More gym. More time on the court.");
            yield return Think("But I can't do everything every day. And I don't have to.");

            var band = Chapter3Rules.DaveBand();
            string ch = StoryState.Flag("ch3_challenge");
            bool challenge = ch == "won" || ch == "lost" || ch == "tie" || ch == "set";
            PhoneUp(true);
            ui.ShowPhone(true, "Dave");
            if (band == Band.Low)
            {
                ui.PhoneBubble("good session today. feels like we've barely had time to talk lately", false, "Dave");
                yield return Wait(0.6f);
                ui.PhoneBubble("food sometime?", false, "Dave");
                yield return ui.PhoneChoose("“Yeah. Let's figure out a day.”", "“You're right. My fault. This week, for sure.”");
                ui.PhoneBubble(Choice == 0 ? "Yeah. Let's figure out a day." : "You're right. My fault. This week, for sure.", true);
                yield return Wait(0.7f);
                ui.PhoneBubble("good. I'll hold you to it", false, "Dave");
            }
            else
            {
                ui.PhoneBubble(challenge
                    ? "Food sometime this week? And don't think I've forgotten our five-attack challenge."
                    : "Food sometime this week? You still owe me a café trip.", false, "Dave");
                string reply = challenge ? "I haven't forgotten. You're still going down." : "Yeah. Let's figure out a day.";
                yield return ui.PhoneChoose("“" + reply + "”");
                ui.PhoneBubble(reply, true);
                yield return Wait(0.7f);
                ui.PhoneBubble(challenge ? "Confidence. I respect it." : "good. you're buying", false, "Dave");
            }
            yield return Wait(0.8f);
            ui.PhoneClear("Feed");
            ui.PhoneCard("marlon.trains  ·  just now", "The court at sunrise, a ball on the line.\n\n\"Work in progress.\"");
            yield return Wait(0.8f);
            yield return ui.PhoneChoose("Put the phone down.");
            ui.ShowPhone(false);
            PhoneUp(false);
            yield return Think("Marlon has his way of doing things. I've got to find mine.");
            yield return Narrate("He sets an alarm for the next session, switches off the light, and goes to bed.");
            if (ceilingLight != null) ceilingLight.enabled = false;
            Recover(Chapter3Rules.Sleep);

            yield return Blend("PullAway", 4f);
            yield return FadeOut(2f);
            ui.SetEnergy(null);
            yield return ui.TitleCard("CHAPTER 3 COMPLETE", "3.  THE PRICE\n\n“The harder you chase your dream, the more carefully you must choose what you're willing to give up.”", 6.5f);
            yield return GoToScene(ClubScene, "club3_teaser");
        }
    }
}
