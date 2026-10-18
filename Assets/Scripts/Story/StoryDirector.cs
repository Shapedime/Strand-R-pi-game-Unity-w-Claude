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
            foreach (var a in FindObjectsByType<StoryActor>(FindObjectsSortMode.None))
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
            SceneManager.LoadScene(scene);
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
        /// Sits the player down with his hips on <paramref name="seat"/> (the seat surface, facing
        /// away from it). The controller and motor switch off so the bed or bench can't push him away.
        /// </summary>
        protected void SitPlayer(Vector3 seat, Quaternion facing)
        {
            if (motor != null) motor.enabled = false;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.SetPositionAndRotation(seat, facing);
            if (playerActor != null) playerActor.Sit(true);
        }

        protected void SitPlayer(Transform seat) => SitPlayer(seat.position, seat.rotation);

        /// <summary>Stands back up and steps forward off the seat, back under the motor's control.</summary>
        protected IEnumerator StandPlayer(float stepForward = 0.45f)
        {
            if (playerActor != null) playerActor.Sit(false);
            yield return new WaitForSeconds(1f);
            Vector3 p = player.position + player.forward * stepForward;
            p.y = 0f;
            PlaceAt(p, player.rotation);
            if (motor != null) motor.enabled = true;
        }

        protected void SetFlag(string key, string value) => StoryState.SetFlag(key, value);
        protected string Flag(string key) => StoryState.Flag(key);
        protected void Relationship(string who, int amount) => StoryState.AddRelationship(who, amount);

        /// <summary>The scene a save at this checkpoint should load (used by Continue).</summary>
        public static string SceneFor(string checkpoint) =>
            checkpoint != null && checkpoint.StartsWith("club") ? ClubScene : HomeScene;
    }
}
