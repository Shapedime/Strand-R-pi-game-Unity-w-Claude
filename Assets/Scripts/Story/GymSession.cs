using System;
using System.Collections;
using System.Collections.Generic;
using BeachVolley.Player;
using BeachVolley.Skills;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeachVolley.Story
{
    /// <summary>Which session: the Day 2 morning gym, Day 3's heavier conditioning, or Day 4's wall passes.</summary>
    public enum GymKind { Morning, Conditioning, WallPasses }

    public struct GymResult
    {
        public int reps, good, points;
        public float Ratio => reps > 0 ? good / (float)reps : 0f;
    }

    /// <summary>
    /// The gym mini-game: back squats, bicep curls and power cleans (the Mixamo clips), a set of
    /// timed reps each, with a barbell or dumbbells that follow the hands. A marker sweeps along
    /// a bar; pressing while it is in the green zone is a clean rep (the rep plays either way). The zone narrows as fatigue builds, so a tired
    /// body really is less forgiving. Gym sessions pay gym points; wall passes pay a little
    /// development credit. Ends with a result card.
    /// </summary>
    public class GymSession : MonoBehaviour
    {
        [SerializeField] StoryUI ui;
        [SerializeField] PlayerSkills skills;
        [Header("Equipment (optional): put in the hands for the lifts")]
        [SerializeField] Transform barbell;
        [SerializeField] Transform dumbbellLeft, dumbbellRight;

        enum Equipment { None, Barbell, Dumbbells }

        struct Exercise
        {
            public string name, gesture;
            public int reps;
            public float sweep;      // seconds for the marker to cross the bar
            public float repTime;    // how long one rep's clip plays before the next one
            public Equipment gear;
            public Exercise(string name, string gesture, int reps, float sweep, float repTime, Equipment gear)
            { this.name = name; this.gesture = gesture; this.reps = reps; this.sweep = sweep; this.repTime = repTime; this.gear = gear; }
        }

        InputAction press;
        Animator body;
        Equipment holding;

        void Awake()
        {
            press = new InputAction("GymPress", InputActionType.Button, "<Keyboard>/space");
            press.AddBinding("<Keyboard>/enter");
            press.AddBinding("<Mouse>/leftButton");
            press.AddBinding("<Gamepad>/buttonSouth");
            Hold(Equipment.None);
        }

        void OnEnable() => press.Enable();
        void OnDisable() => press.Disable();
        void OnDestroy() => press.Dispose();

        // Rep times match the Mixamo clips as played (one rep each): Back Squat 2.3 s at 1×,
        // Bicep Curl 5.9 s at 1.6×, Power Clean 6 s at 1.5× (see CharacterBuilder.ClipSpeed).
        static Exercise[] Plan(GymKind kind) => kind switch
        {
            GymKind.Conditioning => new[]
            {
                new Exercise("Back squats", "GymSquat", 6, 1.15f, SquatRep, Equipment.Barbell),
                new Exercise("Bicep curls", "GymCurl", 5, 1.0f, CurlRep, Equipment.Dumbbells),
                new Exercise("Power cleans", "GymClean", 4, 0.9f, CleanRep, Equipment.Barbell),
            },
            GymKind.WallPasses => new[]
            {
                new Exercise("Wall passes", "Bump", 8, 1.0f, 1f, Equipment.None),
            },
            _ => new[]
            {
                new Exercise("Back squats", "GymSquat", 5, 1.3f, SquatRep, Equipment.Barbell),
                new Exercise("Bicep curls", "GymCurl", 4, 1.15f, CurlRep, Equipment.Dumbbells),
                new Exercise("Power cleans", "GymClean", 3, 1.05f, CleanRep, Equipment.Barbell),
            },
        };

        const float SquatRep = 2.4f, CurlRep = 3.7f, CleanRep = 4.0f;

        // ---------- Equipment in the hands ----------

        void Hold(Equipment gear)
        {
            holding = gear;
            if (barbell != null) barbell.gameObject.SetActive(gear == Equipment.Barbell);
            if (dumbbellLeft != null) dumbbellLeft.gameObject.SetActive(gear == Equipment.Dumbbells);
            if (dumbbellRight != null) dumbbellRight.gameObject.SetActive(gear == Equipment.Dumbbells);
            LateUpdate();
        }

        /// <summary>The grip: between the wrist and the middle finger's base.</summary>
        Vector3 Grip(bool left)
        {
            var hand = body.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var finger = body.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            return finger != null ? Vector3.Lerp(hand.position, finger.position, 0.6f) : hand.position;
        }

        void LateUpdate()
        {
            if (body == null || holding == Equipment.None) return;
            Vector3 l = Grip(true), r = Grip(false);
            if (holding == Equipment.Barbell && barbell != null)
            {
                // The bar runs through both hands (its local X is the bar).
                Vector3 axis = r - l;
                if (axis.sqrMagnitude < 1e-4f) return;
                barbell.SetPositionAndRotation((l + r) * 0.5f, Quaternion.FromToRotation(Vector3.right, axis.normalized));
            }
            else if (holding == Equipment.Dumbbells)
            {
                var fwd = body.transform.forward;
                // Handles across the palm: local X points out of the body's side.
                if (dumbbellLeft != null) dumbbellLeft.SetPositionAndRotation(l, Quaternion.LookRotation(fwd, Vector3.up));
                if (dumbbellRight != null) dumbbellRight.SetPositionAndRotation(r, Quaternion.LookRotation(fwd, Vector3.up));
            }
        }

        /// <summary>Runs the whole session with <paramref name="actor"/> doing the reps, then shows the card.</summary>
        public IEnumerator Run(GymKind kind, StoryActor actor, Action<GymResult> done)
        {
            var plan = Plan(kind);
            int totalReps = 0;
            foreach (var e in plan) totalReps += e.reps;
            float workload = kind switch
            {
                GymKind.Conditioning => Chapter3Rules.GymConditioning,
                GymKind.WallPasses => Chapter3Rules.ExtraPractice,
                _ => Chapter3Rules.GymMorning,
            };
            float effortPerRep = workload / totalReps;

            var lines = new List<TrainingLine>();
            var result = new GymResult();
            body = actor != null ? actor.GetComponentInChildren<Animator>() : null;
            ui.SetEnergy(skills.Fatigue);
            foreach (var ex in plan)
            {
                int good = 0;
                Hold(ex.gear);
                for (int rep = 0; rep < ex.reps; rep++)
                {
                    // The zone shrinks with fatigue and a little as the set goes on.
                    float width = Mathf.Lerp(0.26f, 0.12f, skills.Fatigue) * (1f - 0.02f * rep);
                    float centre = UnityEngine.Random.Range(0.35f, 0.85f);
                    bool hit = false, pressed = false;
                    float marker = 0f;
                    string info = $"Rep {rep + 1} / {ex.reps}   ·   clean reps {good}";
                    ui.ShowTiming(true);
                    yield return null;
                    for (float t = 0f; t < ex.sweep; t += Time.deltaTime)
                    {
                        marker = t / ex.sweep;
                        ui.SetTiming(ex.name, info, centre, width, marker);
                        if (Time.timeScale > 0f && press.WasPressedThisFrame())
                        {
                            pressed = true;
                            hit = Mathf.Abs(marker - centre) <= width * 0.5f;
                            break;
                        }
                        yield return null;
                    }
                    if (hit) good++;
                    ui.SetTiming(ex.name, $"Rep {rep + 1} / {ex.reps}   ·   clean reps {good}", centre, width, marker,
                        hit ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.55f, 0.4f));
                    ui.Toast(hit ? Praise(ex.gesture) : pressed ? "A bit off. Reset." : "Too slow.",
                        hit ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.7f, 0.45f), 0.8f);
                    if (actor != null)
                    {
                        if (ex.gesture == "Bump") actor.Volley(HitType.Bump);
                        else actor.Gesture(ex.gesture);
                    }
                    skills.AddFatigue(effortPerRep);
                    ui.SetEnergy(skills.Fatigue);
                    // Let the rep play out (a clean one moves on a touch quicker).
                    yield return new WaitForSeconds(Mathf.Max(0.9f, ex.repTime - (hit ? 0.2f : 0f)));
                }
                result.reps += ex.reps;
                result.good += good;
                lines.Add(new TrainingLine(ex.name, $"{good}/{ex.reps} clean", 0));
                ui.ShowTiming(false);
                Hold(Equipment.None);
                // Rest between sets.
                yield return new WaitForSeconds(1.2f);
            }
            ui.ShowTiming(false);
            Hold(Equipment.None);

            float ratio = result.Ratio;
            if (kind == GymKind.WallPasses)
            {
                // A small practice reward: a point for a solid set, otherwise credit toward one.
                if (ratio >= 0.5f) { skills.AwardPoints(1, "Extra practice"); result.points = 1; }
                else skills.AddCredit(0.5f, "Extra practice");
                lines.Add(new TrainingLine("Extra practice", ratio >= 0.5f ? "solid" : "scrappy", result.points));
                yield return ui.TrainingCard("EXTRA PRACTICE", lines, "Practice adds fatigue. Tomorrow you'll feel it.");
            }
            else
            {
                result.points = 1 + (ratio >= 0.5f ? 1 : 0) + (ratio >= 0.8f ? 1 : 0);
                skills.AwardGymPoints(result.points, "Gym");
                lines.Add(new TrainingLine("Showed up", kind == GymKind.Conditioning ? "conditioning" : "first gym session", 1));
                // Points beyond the showing-up one go on the exercise lines, in order.
                int extra = result.points - 1;
                for (int i = 0; i < lines.Count - 1 && extra > 0; i++, extra--)
                    lines[i] = new TrainingLine(lines[i].label, lines[i].grade, 1);
                yield return ui.TrainingCard(kind == GymKind.Conditioning ? "CONDITIONING COMPLETE" : "GYM SESSION COMPLETE",
                    lines, "Gym points go on the Gym tab at home: Strength, Leg Power, Explosiveness, Core, Endurance. They never change volleyball skills.",
                    "Gym points earned");
            }
            done?.Invoke(result);
        }

        static string Praise(string gesture) => gesture switch
        {
            "GymSquat" => BarkPool.Once("Clean squat.", "Knees out. Good.", "Deep enough.") ?? "Good rep.",
            "GymCurl" => BarkPool.Once("Elbows still. Good.", "Slow on the way down.") ?? "Good rep.",
            "GymClean" => BarkPool.Once("Fast elbows!", "Clean catch.", "Hips through.") ?? "Good rep.",
            _ => BarkPool.Once("Clean pass.", "Right back to the spot.") ?? "Good rep.",
        };
    }
}
