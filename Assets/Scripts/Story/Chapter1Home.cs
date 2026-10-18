using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Chapter 1 at home: Scene 1 (the match on TV, signing up), Scene 2 (getting ready, how to
    /// get to the club) and, coming back from the club, Scene 7 (back home and the chapter end).
    /// </summary>
    public class Chapter1Home : StoryDirector
    {
        [SerializeField] ScriptedPlay tvMatch;
        [SerializeField] GameObject tvScreen;
        [SerializeField] Transform bedSpot;
        [Tooltip("Where his hips go when he sits on the bed edge.")]
        [SerializeField] Transform bedSeat;
        [SerializeField] Transform doorSpot;
        [SerializeField] GameObject bagOnFloor;
        [SerializeField] GameObject bagByDoor;
        [SerializeField] Interactable bag, bottle, phone, tv, door;
        [SerializeField] Light ceilingLight;
        [Header("The ride to the club")]
        [SerializeField] Transform travelCar;
        [SerializeField] Transform travelBike;
        [SerializeField] StoryActor rider;

        protected override IEnumerator Run()
        {
            ui.SetBlack(true);
            if (StoryState.CheckpointName == "home_end") yield return BackHome();
            else
            {
                if (StoryState.CheckpointName != "home_ready") yield return TheBeginning();
                yield return GettingReady();
            }
        }

        /// <summary>Phone in hand, texting pose (the phone prop on the bed moves into his hand).</summary>
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

        // ---------- Scene 1 ----------

        IEnumerator TheBeginning()
        {
            // Already sitting on the bed edge when the scene fades in.
            SitPlayer(bedSeat);
            bagByDoor.SetActive(false);
            tvMatch.Score += Scoreboard;
            yield return ui.TitleCard("ACE", "The Road to the Top", 3f);
            yield return ui.TitleCard("Chapter 1", "The First Step", 2.5f);

            Cut("Room");
            yield return FadeIn(1.5f);
            yield return Narrate("An ordinary evening.");
            yield return Blend("TV", 2f);
            yield return Narrate("A professional beach volleyball match is on. It's close: the players are exhausted, the score is tight, and the crowd won't sit down.");

            // Into the broadcast for the final point.
            yield return FadeOut(0.4f);
            Cut("Broadcast");
            yield return FadeIn(0.4f);
            yield return tvMatch.WinningPoint();
            yield return Narrate("The ball hits the sand. The match is over. The crowd erupts.");
            Cut("BroadcastClose");
            yield return Wait(1.5f);
            ui.SetObjective(null);
            tvMatch.Score -= Scoreboard;

            yield return FadeOut(0.4f);
            Cut("Player");
            yield return FadeIn(0.4f);
            yield return Narrate("He watches the winner celebrate with his teammate. For a moment, he imagines standing on that court himself.");
            yield return Think("I wonder what it would feel like to be out there.");
            tvMatch.StartLoop();

            // The phone: search, the ad, how he signs up.
            PhoneUp(true);
            yield return Narrate("He picks up his phone and searches for beach volleyball training nearby.");
            ui.ShowPhone(true, "Search: beach volleyball training near me");
            ShowAd();
            yield return Wait(0.6f);
            yield return ui.PhoneChoose("Sign up now", "Read about the club first", "…Maybe another time?");
            int pick = Choice;
            if (pick == 1)
            {
                SetFlag("signup", "research");
                ui.PhoneClear("coastlinebeachclub.com");
                ui.PhoneCard("About Coastline Beach Club",
                    "Six sand courts by the water. Coach Steve runs the beginner group: basics first, games later, and nobody gets left behind.");
                ui.PhoneCard("What members say", "★★★★★  “Came knowing nothing. Three months later I'm playing tournaments with my partner.”  — Dave");
                yield return ui.PhoneChoose("Okay. Sign me up.");
            }
            else if (pick == 2)
            {
                SetFlag("signup", "hesitated");
                ui.ShowPhone(false);
                yield return Think("I've never really played. I'd just be in the way…");
                yield return Narrate("He puts the phone down. On the TV, the winners are still celebrating.");
                yield return Think("…Or I could just try it once.");
                ui.ShowPhone(true, "Search: beach volleyball training near me");
                ShowAd();
                yield return ui.PhoneChoose("Sign up");
            }
            else SetFlag("signup", "immediate");

            ui.PhoneClear("Messages");
            ui.PhoneBubble("Your first beginner training session is tomorrow at 5:00 PM.\nBring sports clothes and water. See you on the court!", false, "Coastline Beach Club");
            yield return Wait(0.8f);
            yield return ui.PhoneChoose("Close");
            ui.ShowPhone(false);
            PhoneUp(false);
            yield return Think("Okay. Let's see what happens.");

            yield return FadeOut(1.2f);
            yield return ui.TitleCard("The next day", "4:15 PM", 2.2f);
            StoryState.Checkpoint("home_ready");
        }

        void ShowAd() => ui.PhoneCard("Coastline Beach Club",
            "Beginner beach volleyball training sessions.\nNo previous experience required.\n\nLearn to play, improve your skills, and find your partner!");

        // ---------- Scene 2 ----------

        IEnumerator GettingReady()
        {
            if (rider != null) rider.Sit(true);
            PlaceAt(bedSpot);
            if (motor != null) motor.enabled = true;
            playerActor.Sit(false);
            bagByDoor.SetActive(false);
            bagOnFloor.SetActive(true);
            tvMatch.StartLoop();
            cam.Track(Shot("Room"), player);
            yield return FadeIn(1f);
            yield return Think("Training's at five. Bag, water… don't forget anything.");

            door.Available = false;
            bool familyTexted = false;
            var items = new List<Interactable> { bag, bottle, phone, tv, door };
            yield return Explore(items, it => Use(it), () => door.Used,
                "Get ready for training\n<size=16>WASD to walk  ·  E to use</size>");

            IEnumerator Use(Interactable it)
            {
                switch (it.id)
                {
                    case "bag":
                        playerActor.Gesture("PickUp");
                        yield return Narrate("Shirt, shorts, towel, sunscreen. He stuffs it all into the sports bag.");
                        bag.Available = false;
                        break;
                    case "bottle":
                        playerActor.Gesture("Drink");
                        yield return Narrate("Water bottle — filled and packed.");
                        bottle.Available = false;
                        break;
                    case "phone":
                        ui.ShowPhone(true, "Messages");
                        ui.PhoneBubble("Your first beginner training session is tomorrow at 5:00 PM.\nBring sports clothes and water. See you on the court!", false, "Coastline Beach Club");
                        yield return ui.PhoneChoose("Close");
                        ui.ShowPhone(false);
                        yield return Think("5:00 PM. Still time. Barely.");
                        break;
                    case "tv":
                        yield return Narrate("They're replaying yesterday's final. He watches the last point again.");
                        yield return Think("How does he even jump that high?");
                        break;
                    case "door":
                        break;
                }

                if (!familyTexted && bag.Used && bottle.Used)
                {
                    familyTexted = true;
                    yield return Wait(0.5f);
                    PhoneUp(true);
                    ui.ShowPhone(true, "Messages");
                    ui.PhoneBubble("Don't forget to leave on time! And don't overthink it.", false, "Mom");
                    yield return Wait(0.6f);
                    string reply = Flag("signup") == "hesitated" ? "Easy for you to say… I almost didn't sign up." : "Easy for you to say…";
                    yield return ui.PhoneChoose(reply);
                    ui.PhoneBubble(reply, true);
                    yield return Wait(1f);
                    ui.ShowPhone(false);
                    PhoneUp(false);
                    bagOnFloor.SetActive(false);
                    door.Available = true;
                    yield return Think("Okay. Time to go.");
                }
            }

            // Transportation choice.
            yield return Narrate("How is he getting to the club?");
            yield return Choose("Take the car", "Ride the bicycle");
            bool bike = Choice == 1;
            SetFlag("transport", bike ? "bike" : "car");
            yield return Think(bike ? "Bike. It's a warm-up, right?" : "Car. I'm saving my legs for the court.");

            yield return FadeOut(1f);
            yield return Ride(bike);
            yield return ui.TitleCard("4:48 PM", "Coastline Beach Club", 2.2f);
            yield return GoToScene(ClubScene, "club_arrival");
        }

        /// <summary>The short ride along the coast road: car or bike, camera gliding alongside.</summary>
        IEnumerator Ride(bool bike)
        {
            var vehicle = bike ? travelBike : travelCar;
            travelCar.gameObject.SetActive(!bike);
            travelBike.gameObject.SetActive(bike);
            const float duration = 7f;
            float speed = bike ? 6f : 13f;
            Vector3 start = vehicle.position;
            Vector3 dir = vehicle.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            StartCoroutine(Drive(vehicle, start, dir * speed, duration));
            // The camera glides along a little slower than the vehicle, so it slowly passes by.
            // Land side of the road, so the sea is behind the vehicle.
            StartCoroutine(cam.Dolly(start - dir * 6f + side * 7f + Vector3.up * 2f,
                start + dir * (speed * duration * 0.75f) + side * 7f + Vector3.up * 2.4f,
                vehicle, Vector3.up, duration, 40f));
            yield return FadeIn(0.8f);
            yield return ui.TitleCard(bike ? "Pedalling along the coast road…" : "Driving down to the coast…", "", 2.4f);
            yield return Wait(1.2f);
            yield return FadeOut(1.2f);
        }

        static IEnumerator Drive(Transform vehicle, Vector3 start, Vector3 velocity, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                vehicle.position = start + velocity * t;
                yield return null;
            }
        }

        // ---------- Scene 7 ----------

        IEnumerator BackHome()
        {
            PlaceAt(doorSpot);
            bagByDoor.SetActive(true);
            bagOnFloor.SetActive(false);
            tvMatch.StopLoop();
            tvScreen.SetActive(false);
            Cut("Room");
            yield return FadeIn(1.2f);
            playerActor.Gesture("Stretch");
            yield return Narrate("He gets home exhausted. Every muscle aches. He drops the sports bag by the door.");
            playerActor.Gesture("Sigh");

            PhoneUp(true);
            ui.ShowPhone(true, "Messages");
            yield return Wait(0.6f);
            string opener = StoryState.Is("first_answer", "watched")
                ? "Good first training session. Watching matches is one thing — now you know how it feels."
                : "Good first training session.";
            ui.PhoneBubble(opener + " You've still got a lot to learn, but I saw that you didn't give up. If you're serious about this, we'll need to keep working.", false, "Steve (Coach)");
            yield return ui.PhoneChoose("I'll be there.", "See you the day after tomorrow, coach.");
            ui.PhoneBubble(Choice == 0 ? "I'll be there." : "See you the day after tomorrow, coach.", true);
            Relationship("steve", 1);
            yield return Wait(1f);
            ui.ShowPhone(false);
            PhoneUp(false);

            SitPlayer(bedSeat);
            yield return Blend("TV", 1.6f);
            tvScreen.SetActive(true);
            tvMatch.Score += Scoreboard;
            yield return Narrate("He sits down and turns on the TV. The same match from yesterday is playing again.");
            yield return FadeOut(0.4f);
            Cut("Broadcast");
            yield return FadeIn(0.4f);
            yield return tvMatch.WinningPoint();
            ui.SetObjective(null);
            tvMatch.Score -= Scoreboard;
            yield return FadeOut(0.4f);
            Cut("Player");
            yield return FadeIn(0.4f);
            yield return Narrate("But this time, something is different. He no longer sees that court as a place for other people.");
            yield return Think("One day, I want to stand there, too.");

            // The camera slowly pulls away.
            yield return Blend("PullAway", 4f);
            yield return FadeOut(2f);
            if (ceilingLight != null) ceilingLight.enabled = false;

            yield return ui.TitleCard("CHAPTER COMPLETE", "1.  THE FIRST STEP\n\nEvery professional was once a beginner.", 6f);
            yield return ui.TitleCard("COMING NEXT:  The Sand Never Forgets",
                "He returns to the court. He must find a beach volleyball partner — while Marlon begins to take him seriously as a competitor.", 7f);

            StoryState.Chapter = 2;
            yield return GoToScene(MainMenuScene, "ch2_start");
        }
    }
}
