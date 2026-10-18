using System;
using System.Collections;
using System.Collections.Generic;
using BeachVolley.Core;
using BeachVolley.Player;
using BeachVolley.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BeachVolley.Story
{
    /// <summary>
    /// Base for a chapter's scene script. Subclasses write the story as one coroutine
    /// (<see cref="Run"/>) out of these helpers: lines, thoughts, choices, camera shots, fades,
    /// free exploration with interactables, and scene changes. Keeps the player frozen whenever
    /// dialogue is up or a cutscene holds the camera.
    /// </summary>
    public abstract class StoryDirector : MonoBehaviour
    {
        public const string HomeScene = "StoryHome";
        public const string ClubScene = "StoryClub";
        public const string TownScene = "StoryTown";
        public const string MainMenuScene = "MainMenu";

        [SerializeField] protected StoryUI ui;
        [SerializeField] protected CinematicCamera cam;
        [SerializeField] protected Transform player;

        protected PlayerMotor motor;
        protected PlayerSkills skills;
        protected StoryActor playerActor;
        readonly Dictionary<string, StoryActor> cast = new Dictionary<string, StoryActor>();
        protected bool cutscene = true;

        readonly Dictionary<string, CameraShot> shots = new Dictionary<string, CameraShot>();
        InputAction interactAction;

        /// <summary>The chapter's script, from where the current checkpoint says to start.</summary>
        protected abstract IEnumerator Run();

        /// <summary>
        /// The chapter this director plays. A scene holds the directors of every chapter that uses
        /// it; only the one matching the save's chapter runs, the others stand down.
        /// </summary>
        protected virtual int ChapterNumber => 1;

        protected virtual void Awake()
        {
            if (ui == null) ui = FindAnyObjectByType<StoryUI>();
            if (cam == null) cam = FindAnyObjectByType<CinematicCamera>();
            if (player != null)
            {
                motor = player.GetComponent<PlayerMotor>();
                skills = player.GetComponent<PlayerSkills>();
                playerActor = player.GetComponent<StoryActor>();
            }
            // Inactive people too: some only step in on later days.
            foreach (var a in FindObjectsByType<StoryActor>(FindObjectsInactive.Include))
                if (!string.IsNullOrEmpty(a.displayName)) cast[a.displayName] = a;
            foreach (var s in FindObjectsByType<CameraShot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                shots[s.name.StartsWith("Shot_") ? s.name.Substring(5) : s.name] = s;
            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/buttonSouth");
        }

        protected virtual void OnEnable() => interactAction.Enable();
        protected virtual void OnDisable() => interactAction.Disable();
        protected virtual void OnDestroy() => interactAction.Dispose();

        protected virtual IEnumerator Start()
        {
            if (StoryState.Chapter != ChapterNumber)
            {
                enabled = false;
                yield break;
            }
            GameMode.Current = GameModeKind.Story;
            Time.timeScale = 1f;
            // Skills carry across scenes and sessions through the story save.
            StoryState.ApplySkills(skills);
            // Cutscenes are the default; gameplay sections switch the player back (EndActing).
            if (playerActor != null) playerActor.BeginActing();
            yield return null;
            yield return Run();
        }

        protected virtual void Update()
        {
            if (ui == null) return;
            if (motor != null) motor.InputLocked = cutscene || ui.IsBusy;
        }

        // ---------- Script helpers ----------

        /// <summary>A spoken line; the speaker (if they're in the scene) talks while the box is up.</summary>
        protected IEnumerator Say(string speaker, string text)
        {
            var actor = Actor(speaker);
            if (actor != null) actor.Talk(true);
            yield return ui.Say(speaker, text);
            if (actor != null) actor.Talk(false);
        }

        /// <summary>The actor for a dialogue name ("Coach Steve" → Steve, "You" → the player).</summary>
        protected StoryActor Actor(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return null;
            if (cast.TryGetValue(speaker, out var a)) return a;
            foreach (var kv in cast)
                if (speaker.EndsWith(kv.Key)) return kv.Value;
            return null;
        }
        protected IEnumerator Think(string text) => ui.Think(text);
        protected IEnumerator Narrate(string text) => ui.Narrate(text);
        protected IEnumerator Choose(params string[] options) => ui.Choose(options);
        protected int Choice => ui.LastChoice;
        protected IEnumerator Wait(float seconds) { yield return new WaitForSeconds(seconds); }
        protected IEnumerator FadeOut(float duration = 0.8f) => ui.Fade(1f, duration);
        protected IEnumerator FadeIn(float duration = 0.8f) => ui.Fade(0f, duration);

        protected CameraShot Shot(string name)
        {
            if (shots.TryGetValue(name, out var s)) return s;
            Debug.LogWarning("[Story] Missing camera shot " + name);
            return null;
        }

        protected void Cut(string shot) => cam.Cut(Shot(shot));
        protected IEnumerator Blend(string shot, float duration = 1.2f) => cam.Blend(Shot(shot), duration);

        /// <summary>
        /// Free roam: the player walks around; standing next to an available interactable shows
        /// its prompt and E uses it, running <paramref name="onUse"/>. Ends once <paramref name="done"/> is true.
        /// </summary>
        protected IEnumerator Explore(IList<Interactable> items, Func<Interactable, IEnumerator> onUse, Func<bool> done, string objective = null)
        {
            cutscene = false;
            while (!done())
            {
                ui.SetObjective(objective);
                Interactable near = null;
                float best = float.MaxValue;
                foreach (var it in items)
                {
                    if (it == null || !it.InRange(player.position)) continue;
                    float d = (it.transform.position - player.position).sqrMagnitude;
                    if (d < best) { best = d; near = it; }
                }
                ui.SetPrompt(near != null ? "E  —  " + near.prompt : null);
                if (near != null && !ui.IsBusy && Time.timeScale > 0f && interactAction.WasPressedThisFrame())
                {
                    ui.SetPrompt(null);
                    cutscene = true;
                    if (motor != null) motor.FaceTowards(near.transform.position, 0.3f);
                    near.Use();
                    yield return onUse(near);
                    cutscene = false;
                }
                yield return null;
            }
            ui.SetPrompt(null);
            ui.SetObjective(null);
            cutscene = true;
        }

        /// <summary>Saves and goes to another story scene (or the main menu) behind a fade.</summary>
        protected IEnumerator GoToScene(string scene, string checkpoint)
        {
            yield return FadeOut();
            StoryState.StoreSkills(skills);
            if (checkpoint != null) StoryState.Checkpoint(checkpoint);
            else StoryState.Save();
            UiPointerGate.MenuOpen = false;
            SceneLoader.Load(scene);
            // Hold here until the scene goes away (the loading screen covers the switch).
            while (true) yield return null;
        }

        protected void PlaceAt(Vector3 position, Quaternion rotation)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.SetPositionAndRotation(position, rotation);
            if (cc != null) cc.enabled = true;
        }

        protected void PlaceAt(Transform spot) => PlaceAt(spot.position, spot.rotation);

        /// <summary>
        /// Sits the player down with his hips on <paramref name="seat"/> (facing away from it): he
        /// stands just in front of it and the clip sits him back onto it. The controller and motor
        /// switch off so the bed or bench can't push him away.
        /// </summary>
        protected void SitPlayer(Vector3 seat, Quaternion facing)
        {
            if (motor != null) motor.enabled = false;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.SetPositionAndRotation(SeatFront(seat, facing), facing);
            if (playerActor != null) playerActor.Sit(true);
        }

        /// <summary>Where someone stands so that sitting down puts their hips on <paramref name="seat"/>.</summary>
        protected static Vector3 SeatFront(Vector3 seat, Quaternion facing) =>
            seat + facing * Vector3.forward * StoryActor.SitTravel;

        /// <summary>Sits a cast member down with their hips on <paramref name="seat"/>.</summary>
        protected static void SitNpc(StoryActor npc, Vector3 seat, Quaternion facing)
        {
            npc.transform.SetPositionAndRotation(SeatFront(seat, facing), facing);
            npc.LookAt(null);
            npc.Sit(true);
        }

        // The club bench (see StoryBuilder): centred here, turned to face the training court.
        protected static readonly Vector3 BenchCentre = new Vector3(-6.2f, 0f, -3.9f);
        protected static readonly Vector3 BenchForward = new Vector3(5.6f, 0f, 3.5f).normalized;
        protected static Quaternion BenchFacing => Quaternion.LookRotation(BenchForward);

        /// <summary>
        /// A spot on the club bench for someone's hips, <paramref name="along"/> metres along it
        /// (positive = to the left as they sit facing the court).
        /// </summary>
        protected static Vector3 BenchSeat(float along) =>
            BenchCentre + Vector3.Cross(Vector3.up, BenchForward) * -along;

        protected void SitPlayer(Transform seat) => SitPlayer(seat.position, seat.rotation);

        /// <summary>Stands back up and steps forward off the seat, back under the motor's control.</summary>
        protected IEnumerator StandPlayer(float stepForward = 0.05f)
        {
            if (playerActor != null) playerActor.Sit(false);
            yield return new WaitForSeconds(1f);
            Vector3 p = player.position + player.forward * stepForward;
            p.y = 0f;
            PlaceAt(p, player.rotation);
            if (motor != null) motor.enabled = true;
        }

        // ---------- Staging (people and camera) ----------

        protected static Quaternion Facing(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d) : Quaternion.identity;
        }

        protected static void Put(StoryActor npc, Vector3 at, Vector3 faceTowards)
        {
            npc.transform.SetPositionAndRotation(at, Facing(at, faceTowards));
            npc.LookAt(null);
        }

        protected static IEnumerator WalkTo(StoryActor npc, Vector3 to)
        {
            npc.WalkTo(to);
            while (npc.IsWalking) yield return null;
        }

        /// <summary>Over-the-shoulder shot: behind <paramref name="from"/>, looking at <paramref name="at"/>'s face.</summary>
        protected void OverShoulder(Transform from, Transform at, float side = 1f)
        {
            Vector3 dir = at.position - from.position;
            dir.y = 0f;
            dir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 pos = from.position + Vector3.up * 1.75f - dir * 1.4f + right * (0.55f * side);
            cam.FrameOn(pos, at, Vector3.up * 1.55f, 42f);
        }

        /// <summary>Side-on two-shot of two people talking.</summary>
        protected void TwoShot(Transform a, Transform b, float side = 1f, float distance = 3.4f)
        {
            Vector3 mid = (a.position + b.position) * 0.5f + Vector3.up * 1.45f;
            Vector3 dir = b.position - a.position;
            dir.y = 0f;
            dir.Normalize();
            Vector3 n = Vector3.Cross(Vector3.up, dir) * side;
            cam.Frame(mid + n * distance + Vector3.up * 0.15f, mid, 45f);
        }

        /// <summary>Three-tier verdict: two thirds good, one third good, or less.</summary>
        protected static string Grade(int good, int reps, string great, string ok, string rough) =>
            good * 3 >= reps * 2 ? great : good * 3 >= reps ? ok : rough;

        protected void SetFlag(string key, string value) => StoryState.SetFlag(key, value);
        protected string Flag(string key) => StoryState.Flag(key);
        protected void Relationship(string who, int amount) => StoryState.AddRelationship(who, amount);

        /// <summary>The scene a save at this checkpoint should load (used by Continue).</summary>
        public static string SceneFor(string checkpoint) =>
            checkpoint != null && checkpoint.StartsWith("club") ? ClubScene
            : checkpoint != null && checkpoint.StartsWith("town") ? TownScene : HomeScene;
    }
}
