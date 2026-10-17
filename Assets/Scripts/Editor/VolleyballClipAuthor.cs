using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BeachVolley.EditorTools
{
    /// <summary>
    /// Builds the volleyball action clips (Bump, Set, Spike, Dive) as humanoid .anim files.
    /// Moves are described as a few key poses (muscle overrides on top of the Mixamo Idle's
    /// first frame), then baked to a key every frame:
    /// - poses are joined with a monotone spline, so motion flows through keys without
    ///   stopping or overshooting;
    /// - body parts are offset in time (legs lead, arms and head trail) so motion rolls through the body;
    /// - root lean is a single pitch angle, turned into a proper rotation each frame.
    /// Every clip starts and ends on the Idle stance so blends into and out of it are invisible.
    /// Muscle signs measured on this rig: Arm Down-Up + = raise,
    /// Arm/Shoulder Front-Back − = forward, Upper Leg Front-Back − = thigh forward (standing ≈ 0.35, squat ≈ −0.2),
    /// Forearm/Lower Leg Stretch + = straight, Spine/Chest Front-Back + = bend forward.
    /// </summary>
    public static class VolleyballClipAuthor
    {
        const string ClipDir = "Assets/Animations/Player";
        const string IdlePath = ClipDir + "/Idle.fbx";
        const float FrameRate = 30f;

        // Pseudo-properties: body pitch in degrees (forward +) and normalized body height.
        const string Pitch = "@pitch";
        const string Height = "@height";

        // Overlapping action: how far ahead (+) or behind (−) each body group runs, in seconds.
        const float LegLead = 0.03f;
        const float ArmLag = -0.03f;
        const float HeadLag = -0.06f;

        class Pose : Dictionary<string, float> { }

        /// <summary>
        /// How body height is baked. Lock: soles stay on the sand every frame (crouches go down).
        /// Floor: the authored height, but never through the sand (the dive). None: as authored.
        /// </summary>
        enum Ground { None, Lock, Floor }

        /// <summary>One beat of a move. Drift pulls the pose part of the way toward the next beat, so holds never freeze.</summary>
        struct Beat
        {
            public float time;
            public Pose pose;
            public float drift;
            public Beat(float time, Pose pose, float drift = 0f) { this.time = time; this.pose = pose; this.drift = drift; }
        }

        static Pose Both(Pose p, string muscle, float value)
        {
            p["Left " + muscle] = value;
            p["Right " + muscle] = value;
            return p;
        }

        static Pose With(Pose p, Pose extra)
        {
            var r = new Pose();
            foreach (var kv in p) r[kv.Key] = kv.Value;
            foreach (var kv in extra) r[kv.Key] = kv.Value;
            return r;
        }

        [MenuItem("Beach Volleyball/Author Volleyball Clips")]
        public static void AuthorAll()
        {
            var idle = LoadIdle();
            if (idle == null) return;
            var basePose = new Dictionary<string, float>();
            foreach (var b in AnimationUtility.GetCurveBindings(idle))
            {
                string prop = b.propertyName;
                // IK goals and the root are rebuilt here; the muscles carry the pose.
                if (prop.Contains("FootT") || prop.Contains("FootQ") || prop.Contains("HandT") || prop.Contains("HandQ")) continue;
                if (prop.StartsWith("RootQ") || prop.StartsWith("RootT")) continue;
                basePose[prop] = AnimationUtility.GetEditorCurve(idle, b).Evaluate(0f);
            }
            float idleHeight = 0.95f;
            foreach (var b in AnimationUtility.GetCurveBindings(idle))
                if (b.propertyName == "RootT.y") idleHeight = AnimationUtility.GetEditorCurve(idle, b).Evaluate(0f);
            basePose[Pitch] = 0f;
            basePose[Height] = idleHeight;

            Save("Bump", basePose, BumpBeats(), Ground.Lock);
            Save("Set", basePose, SetBeats(), Ground.Lock);
            // Attack arm swings play on the upper-body layer; the root comes from the base layer.
            // The cock is entered automatically (jump near a high ball, or a serve toss); the swings
            // start from it. Spike and roll share the wind-up and differ at contact.
            Save("SpikeCock", basePose, CockBeats(), Ground.None);
            // The fast part of the whip lives within ~0.1 s, so the swings get 60 fps keys.
            Save("SpikeSwing", basePose, SpikeSwingBeats(), Ground.None, ChainLead, 60f);
            Save("RollSwing", basePose, RollSwingBeats(), Ground.None, ChainLead, 60f);
            // Block also plays on the upper-body layer: standing or mid-jump at the net.
            Save("Block", basePose, BlockBeats(), Ground.None);
            Save("Dive", basePose, DiveBeats(), Ground.Floor);
            // The motor provides the jump height, so the air poses keep the Mixamo body motion
            // but hold the body at standing height (their own rise would double the jump).
            CopyWithStandingRoot("Jump.fbx", "JumpRise", "AirRise", idleHeight);
            CopyWithStandingRoot("Jump.fbx", "JumpFall", "AirFall", idleHeight);
            AssetDatabase.SaveAssets();
            // The single-clip spike was replaced by SpikeCock + SpikeSwing.
            AssetDatabase.DeleteAsset($"{ClipDir}/Spike.anim");
            Debug.Log("[VolleyballClipAuthor] Wrote Bump, Set, SpikeCock, SpikeSwing, RollSwing, Block, Dive, AirRise, AirFall to " + ClipDir);
        }

        /// <summary>Copies a Mixamo take's muscle curves into a new clip whose body stays upright at standing height.</summary>
        static void CopyWithStandingRoot(string file, string take, string name, float height)
        {
            AnimationClip source = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath($"{ClipDir}/{file}"))
                if (o is AnimationClip c && c.name == take) source = c;
            if (source == null)
            {
                Debug.LogWarning($"[VolleyballClipAuthor] {file} has no '{take}' take; run Setup Mixamo Character first.");
                return;
            }
            string path = $"{ClipDir}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.frameRate = FrameRate;
            foreach (var b in AnimationUtility.GetCurveBindings(source))
            {
                string prop = b.propertyName;
                if (prop.StartsWith("Root") || prop.Contains("FootT") || prop.Contains("FootQ")
                    || prop.Contains("HandT") || prop.Contains("HandQ")) continue;
                AnimationUtility.SetEditorCurve(clip, b, AnimationUtility.GetEditorCurve(source, b));
            }
            float end = source.length;
            void Flat(string prop, float value) => AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Animator), prop), AnimationCurve.Constant(0f, end, value));
            Flat("RootT.x", 0f); Flat("RootT.y", height); Flat("RootT.z", 0f);
            Flat("RootQ.x", 0f); Flat("RootQ.y", 0f); Flat("RootQ.z", 0f); Flat("RootQ.w", 1f);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.stopTime = end;
            settings.loopBlendOrientation = true;
            settings.keepOriginalOrientation = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalPositionXZ = true;
            settings.loopBlendPositionY = true;
            settings.keepOriginalPositionY = true;
            settings.heightFromFeet = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        static AnimationClip LoadIdle()
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(IdlePath))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__") && c.humanMotion) return c;
            Debug.LogError($"[VolleyballClipAuthor] {IdlePath} must exist and import as Humanoid.");
            return null;
        }

        // ---------- Poses ----------

        static Pose Idle() => new Pose();

        static Pose Ready()
        {
            // Athletic stance: knees bent, weight forward, arms loose in front.
            var p = new Pose
            {
                ["Spine Front-Back"] = 0.2f, ["Chest Front-Back"] = 0.1f,
                ["Head Nod Down-Up"] = 0.2f,
                [Pitch] = 4f,
            };
            Both(p, "Upper Leg Front-Back", 0.05f);
            Both(p, "Lower Leg Stretch", 0.4f);
            Both(p, "Foot Up-Down", 0.2f);
            Both(p, "Arm Down-Up", -0.6f);
            Both(p, "Arm Front-Back", -0.1f);
            Both(p, "Forearm Stretch", 0.5f);
            return p;
        }

        static List<Beat> BumpBeats()
        {
            // Deep sit, straight arms joined into a platform out in front, eyes on the ball.
            var platform = new Pose
            {
                ["Spine Front-Back"] = 0.4f, ["Chest Front-Back"] = 0.25f, ["UpperChest Front-Back"] = 0.1f,
                ["Head Nod Down-Up"] = 0.45f, ["Neck Nod Down-Up"] = 0.3f,
                // Lean the whole body into the ball; bent knees alone leave the torso tilting back.
                [Pitch] = 15f,
            };
            Both(platform, "Upper Leg Front-Back", -0.3f);
            Both(platform, "Lower Leg Stretch", -0.05f);
            Both(platform, "Foot Up-Down", 0.4f);
            // Hands low, out front and together (searched on the rig: ~0.75 arm lengths ahead, 0.6 below the shoulder).
            Both(platform, "Shoulder Front-Back", 0.2f);
            Both(platform, "Shoulder Down-Up", -0.1f);
            Both(platform, "Arm Down-Up", -0.6f);
            Both(platform, "Arm Front-Back", -0.5f);
            Both(platform, "Arm Twist In-Out", -0.5f);
            Both(platform, "Forearm Stretch", 1f);
            Both(platform, "Forearm Twist In-Out", 0.3f);
            Both(platform, "Hand Down-Up", -0.6f);

            // Anticipation: knees give and the arms start coming together before the platform locks.
            var dip = With(Ready(), new Pose { [Pitch] = 8f, ["Spine Front-Back"] = 0.3f });
            Both(dip, "Upper Leg Front-Back", -0.1f);
            Both(dip, "Lower Leg Stretch", 0.2f);
            Both(dip, "Arm Front-Back", -0.35f);
            Both(dip, "Arm Twist In-Out", -0.3f);
            Both(dip, "Forearm Stretch", 0.85f);

            // Legs drive up through the contact; arms stay locked and rise a touch.
            var lift = With(platform, new Pose { ["Spine Front-Back"] = 0.25f, [Pitch] = 8f });
            Both(lift, "Upper Leg Front-Back", 0.1f);
            Both(lift, "Lower Leg Stretch", 0.5f);
            Both(lift, "Arm Down-Up", -0.45f);

            // Follow: platform eases off toward the ready stance.
            var follow = With(Ready(), new Pose { [Pitch] = 5f });
            Both(follow, "Arm Down-Up", -0.5f);
            Both(follow, "Arm Front-Back", -0.3f);
            Both(follow, "Forearm Stretch", 0.75f);

            return new List<Beat>
            {
                new Beat(0f, Idle()), new Beat(0.1f, dip), new Beat(0.2f, platform),
                new Beat(0.32f, platform, 0.1f), new Beat(0.44f, lift), new Beat(0.58f, follow), new Beat(0.75f, Idle()),
            };
        }

        static List<Beat> SetBeats()
        {
            // Hands up in a window above the forehead, elbows out, then push up through arms and legs.
            var load = new Pose
            {
                ["Spine Front-Back"] = -0.1f, ["Chest Front-Back"] = -0.15f,
                ["Head Nod Down-Up"] = 0.8f, ["Neck Nod Down-Up"] = 0.5f,
            };
            Both(load, "Upper Leg Front-Back", -0.1f);
            Both(load, "Lower Leg Stretch", 0.15f);
            Both(load, "Shoulder Down-Up", 0.5f);
            Both(load, "Arm Down-Up", 0.9f);
            Both(load, "Arm Front-Back", 0.1f);
            Both(load, "Arm Twist In-Out", 0.5f);
            Both(load, "Forearm Stretch", -0.45f);
            Both(load, "Hand Down-Up", 0.9f);

            // Hands on the way up while the knees start to load.
            var rise = With(Ready(), new Pose { ["Head Nod Down-Up"] = 0.6f, ["Spine Front-Back"] = 0f });
            Both(rise, "Upper Leg Front-Back", 0f);
            Both(rise, "Lower Leg Stretch", 0.3f);
            Both(rise, "Arm Down-Up", 0.3f);
            Both(rise, "Arm Front-Back", -0.3f);
            Both(rise, "Forearm Stretch", -0.2f);
            Both(rise, "Hand Down-Up", 0.5f);

            var push = new Pose
            {
                ["Spine Front-Back"] = -0.2f, ["Chest Front-Back"] = -0.2f,
                ["Head Nod Down-Up"] = 0.8f, ["Neck Nod Down-Up"] = 0.5f,
            };
            Both(push, "Upper Leg Front-Back", 0.3f);
            Both(push, "Lower Leg Stretch", 0.85f);
            Both(push, "Shoulder Down-Up", 0.7f);
            Both(push, "Arm Down-Up", 0.95f);
            Both(push, "Arm Front-Back", -0.45f);
            Both(push, "Forearm Stretch", 0.6f);
            Both(push, "Hand Down-Up", 0.3f);

            // Follow: arms finish long toward the target, up on the toes, hands relax.
            var follow = With(push, new Pose { ["Head Nod Down-Up"] = 0.5f });
            Both(follow, "Arm Down-Up", 0.8f);
            Both(follow, "Arm Front-Back", -0.65f);
            Both(follow, "Forearm Stretch", 0.85f);
            Both(follow, "Hand Down-Up", -0.1f);
            Both(follow, "Foot Up-Down", -0.3f);

            return new List<Beat>
            {
                new Beat(0f, Idle()), new Beat(0.12f, rise), new Beat(0.24f, load),
                new Beat(0.34f, load, 0.1f), new Beat(0.46f, push), new Beat(0.58f, follow), new Beat(0.8f, Idle()),
            };
        }

        // ---------- Attack arm (spike / roll / serve) ----------
        // Arm values were solved on this rig for hand/elbow positions relative to the head.

        /// <summary>
        /// Bow-and-arrow: right arm raised with the elbow fully bent so the hand sits behind the head
        /// by the ear, left arm straight up in front as a pointer at the ball, back arched and the
        /// chest turned to load the right shoulder.
        /// </summary>
        static Pose Cocked()
        {
            var p = new Pose
            {
                ["Spine Front-Back"] = -0.2f, ["Chest Front-Back"] = -0.4f, ["UpperChest Front-Back"] = -0.4f,
                // + turns the chest so the right shoulder goes back.
                ["Chest Twist Left-Right"] = 0.3f, ["UpperChest Twist Left-Right"] = 0.3f,
                ["Head Nod Down-Up"] = 0.7f, ["Neck Nod Down-Up"] = 0.3f,
                ["Right Shoulder Down-Up"] = 1f, ["Right Shoulder Front-Back"] = 1f,
                ["Right Arm Down-Up"] = 0.8f, ["Right Arm Front-Back"] = 0.5f, ["Right Arm Twist In-Out"] = 1f,
                ["Right Forearm Stretch"] = -1f, ["Right Hand Down-Up"] = 0.6f,
                ["Left Shoulder Down-Up"] = 0.5f,
                ["Left Arm Down-Up"] = 0.85f, ["Left Arm Front-Back"] = -0.7f, ["Left Forearm Stretch"] = 0.9f,
                ["Left Hand Down-Up"] = 0.2f,
            };
            return p;
        }

        /// <summary>
        /// The elbow drives forward and high while the forearm stays folded, so the hand is still
        /// behind the head; the left arm pulls down beside the body and the chest starts to unwind.
        /// </summary>
        static Pose ElbowLeads()
        {
            var p = With(Cocked(), new Pose
            {
                ["Spine Front-Back"] = -0.1f, ["Chest Front-Back"] = -0.15f, ["UpperChest Front-Back"] = -0.15f,
                ["Chest Twist Left-Right"] = -0.1f, ["UpperChest Twist Left-Right"] = -0.1f,
                ["Head Nod Down-Up"] = 0.6f,
                ["Right Shoulder Front-Back"] = 0.3f,
                ["Right Arm Down-Up"] = 0.8f, ["Right Arm Front-Back"] = -0.2f, ["Right Arm Twist In-Out"] = 1f,
                ["Right Forearm Stretch"] = -1f, ["Right Hand Down-Up"] = 0.6f,
                ["Left Shoulder Down-Up"] = 0.2f,
                ["Left Arm Down-Up"] = 0.15f, ["Left Arm Front-Back"] = -0.4f, ["Left Forearm Stretch"] = 0.8f,
                ["Left Hand Down-Up"] = 0f,
            });
            return p;
        }

        static List<Beat> CockBeats()
        {
            // Arms rise from the idle into the bow-and-arrow; the left arm (the pointer / toss arm)
            // is a touch ahead of the hitting arm. The state holds the last frame.
            var rising = With(Cocked(), new Pose
            {
                ["Chest Front-Back"] = -0.2f, ["UpperChest Front-Back"] = -0.2f,
                ["Chest Twist Left-Right"] = 0.15f, ["UpperChest Twist Left-Right"] = 0.15f,
                ["Right Arm Down-Up"] = 0.4f, ["Right Arm Front-Back"] = 0.1f, ["Right Forearm Stretch"] = -0.4f,
                ["Left Arm Down-Up"] = 0.6f,
            });
            return new List<Beat> { new Beat(0f, Idle()), new Beat(0.14f, rising), new Beat(0.3f, Cocked()) };
        }

        static List<Beat> SpikeSwingBeats()
        {
            // Contact: arm fully extended high in front of the head, the hand snapped down over the ball,
            // chest turned through so the right shoulder is forward.
            var contact = new Pose
            {
                ["Spine Front-Back"] = 0.1f, ["Chest Front-Back"] = 0.15f, ["UpperChest Front-Back"] = 0.1f,
                ["Chest Twist Left-Right"] = -0.3f, ["UpperChest Twist Left-Right"] = -0.3f,
                ["Head Nod Down-Up"] = 0.4f,
                ["Right Shoulder Down-Up"] = 1f, ["Right Shoulder Front-Back"] = 0f,
                // Solved in the jumping, uncoiling torso: hand 0.37 m above and 0.23 m in front of the head.
                ["Right Arm Down-Up"] = 0.5f, ["Right Arm Front-Back"] = -0.5f, ["Right Arm Twist In-Out"] = 0.5f,
                ["Right Forearm Stretch"] = 1f, ["Right Hand Down-Up"] = -0.9f,
                ["Left Arm Down-Up"] = -0.6f, ["Left Arm Front-Back"] = 0.1f, ["Left Forearm Stretch"] = 0.7f,
            };
            // Halfway through the elbow extension, wrist still cocked back: spreads the fastest part of
            // the swing over two beats so the forearm doesn't jump.
            var extending = With(contact, new Pose
            {
                ["Chest Twist Left-Right"] = -0.15f, ["UpperChest Twist Left-Right"] = -0.15f,
                ["Right Shoulder Front-Back"] = 0.15f,
                ["Right Arm Down-Up"] = 0.75f, ["Right Arm Front-Back"] = -0.4f, ["Right Arm Twist In-Out"] = 0.75f,
                ["Right Forearm Stretch"] = 0f, ["Right Hand Down-Up"] = 0.5f,
            });
            // Follow-through: the arm whips down and across the body and the torso crunches over.
            var follow = new Pose
            {
                ["Spine Front-Back"] = 0.5f, ["Chest Front-Back"] = 0.35f, ["UpperChest Front-Back"] = 0.2f,
                ["Chest Twist Left-Right"] = -0.35f, ["UpperChest Twist Left-Right"] = -0.35f,
                ["Head Nod Down-Up"] = -0.1f,
                ["Right Shoulder Down-Up"] = 0.2f,
                ["Right Arm Down-Up"] = -0.7f, ["Right Arm Front-Back"] = -0.6f, ["Right Arm Twist In-Out"] = -0.3f,
                ["Right Forearm Stretch"] = 0.7f, ["Right Hand Down-Up"] = -0.5f,
                ["Left Arm Down-Up"] = -0.6f, ["Left Arm Front-Back"] = 0.1f, ["Left Forearm Stretch"] = 0.6f,
            };
            // Contact at 0.16 s (BallContact's spike contact delay matches this).
            return new List<Beat>
            {
                new Beat(0f, Cocked()), new Beat(0.08f, ElbowLeads()), new Beat(0.12f, extending),
                new Beat(0.16f, contact), new Beat(0.32f, follow), new Beat(0.44f, follow, 0.3f), new Beat(0.64f, Idle()),
            };
        }

        static List<Beat> RollSwingBeats()
        {
            // Contact: the arm reaches long, forward and high without the power; only the wrist flicks
            // over the ball to send it up in a high, soft arc. The chest barely turns through.
            var contact = new Pose
            {
                ["Spine Front-Back"] = 0f, ["Chest Front-Back"] = 0.05f,
                ["Chest Twist Left-Right"] = -0.15f, ["UpperChest Twist Left-Right"] = -0.15f,
                ["Head Nod Down-Up"] = 0.55f,
                ["Right Shoulder Down-Up"] = 0.9f, ["Right Shoulder Front-Back"] = -0.1f,
                ["Right Arm Down-Up"] = 0.7f, ["Right Arm Front-Back"] = -0.6f, ["Right Arm Twist In-Out"] = 0f,
                ["Right Forearm Stretch"] = 1f, ["Right Hand Down-Up"] = -0.6f,
                ["Left Arm Down-Up"] = -0.5f, ["Left Arm Front-Back"] = 0f, ["Left Forearm Stretch"] = 0.7f,
            };
            var extending = With(contact, new Pose
            {
                ["Chest Twist Left-Right"] = -0.05f, ["UpperChest Twist Left-Right"] = -0.05f,
                ["Right Arm Down-Up"] = 0.75f, ["Right Arm Front-Back"] = -0.45f, ["Right Arm Twist In-Out"] = 0.25f,
                ["Right Forearm Stretch"] = 0.1f, ["Right Hand Down-Up"] = 0.5f,
            });
            // The arm stays up and forward after the touch, hand open toward the target: no whip down.
            var reachHold = With(contact, new Pose
            {
                ["Right Hand Down-Up"] = -0.4f, ["Right Arm Down-Up"] = 0.65f,
                ["Head Nod Down-Up"] = 0.45f,
            });
            // Contact at 0.18 s (BallContact's roll contact delay matches this).
            return new List<Beat>
            {
                new Beat(0f, Cocked()), new Beat(0.09f, ElbowLeads()), new Beat(0.135f, extending),
                new Beat(0.18f, contact), new Beat(0.36f, reachHold, 0.2f), new Beat(0.5f, reachHold), new Beat(0.7f, Idle()),
            };
        }

        /// <summary>
        /// Kinetic chain for the attack swings: the torso unwinds first, then the upper arm, then the
        /// forearm, then the hand, so the motion whips out through the arm instead of every joint
        /// snapping on the same frame. Positive = runs ahead.
        /// </summary>
        static float ChainLead(string prop)
        {
            if (prop == Pitch || prop == Height) return 0f;
            if (prop.Contains("Head") || prop.Contains("Neck")) return HeadLag;
            if (prop.Contains("Hand")) return -0.01f;
            if (prop.Contains("Forearm")) return -0.01f;
            if (prop.Contains("Shoulder") || prop.Contains("Arm")) return 0f;
            if (prop.Contains("Spine") || prop.Contains("Chest")) return 0.04f;
            return Lead(prop);
        }

        static List<Beat> BlockBeats()
        {
            // A wall over the net: shoulders shrug up to the ears, arms locked straight overhead and
            // slightly forward, wrists bent over to push the ball down, eyes on the hitter.
            var wall = new Pose
            {
                ["Spine Front-Back"] = -0.05f, ["Chest Front-Back"] = 0.05f,
                ["Head Nod Down-Up"] = 0.35f, ["Neck Nod Down-Up"] = 0.2f,
            };
            Both(wall, "Shoulder Down-Up", 0.9f);
            Both(wall, "Shoulder Front-Back", -0.2f);
            Both(wall, "Arm Down-Up", 1f);
            Both(wall, "Arm Front-Back", -0.3f);
            Both(wall, "Arm Twist In-Out", 0.3f);
            Both(wall, "Forearm Stretch", 1f);
            Both(wall, "Hand Down-Up", -0.5f);

            // On the way up: hands rise in front of the face, elbows still bent.
            var reach = new Pose { ["Head Nod Down-Up"] = 0.3f };
            Both(reach, "Shoulder Down-Up", 0.4f);
            Both(reach, "Arm Down-Up", 0.5f);
            Both(reach, "Arm Front-Back", -0.5f);
            Both(reach, "Forearm Stretch", 0.2f);
            Both(reach, "Hand Down-Up", 0.3f);

            // Coming down: arms drop back toward the sides.
            var settle = With(reach, new Pose { ["Head Nod Down-Up"] = 0.1f });
            Both(settle, "Shoulder Down-Up", 0.2f);
            Both(settle, "Arm Down-Up", 0f);
            Both(settle, "Forearm Stretch", 0.5f);

            return new List<Beat>
            {
                new Beat(0f, Idle()), new Beat(0.1f, reach), new Beat(0.18f, wall),
                new Beat(0.36f, wall, 0.1f), new Beat(0.46f, settle), new Beat(0.6f, Idle()),
            };
        }

        static List<Beat> DiveBeats()
        {
            // Launch low, then lay out flat on the sand with both arms reaching ahead.
            var launch = With(Ready(), new Pose { [Pitch] = 20f, [Height] = 0.75f, ["Spine Front-Back"] = 0.5f });
            Both(launch, "Upper Leg Front-Back", -0.3f);
            Both(launch, "Lower Leg Stretch", 0f);
            Both(launch, "Arm Down-Up", 0.1f);
            Both(launch, "Arm Front-Back", -0.9f);
            Both(launch, "Forearm Stretch", 0.9f);

            var layout = new Pose
            {
                [Pitch] = 78f, [Height] = 0.32f,
                ["Spine Front-Back"] = -0.25f, ["Chest Front-Back"] = -0.2f,
                ["Head Nod Down-Up"] = 0.9f, ["Neck Nod Down-Up"] = 0.6f,
            };
            Both(layout, "Upper Leg Front-Back", 0.55f);
            Both(layout, "Lower Leg Stretch", 0.85f);
            Both(layout, "Foot Up-Down", -0.6f);
            Both(layout, "Shoulder Down-Up", 0.6f);
            Both(layout, "Arm Down-Up", 0.7f);
            Both(layout, "Arm Front-Back", -0.85f);
            Both(layout, "Forearm Stretch", 1f);
            Both(layout, "Hand Down-Up", -0.3f);

            // Settled on the sand: arms come down a little, head drops, ready for the get-up.
            var settled = With(layout, new Pose { ["Head Nod Down-Up"] = 0.5f, ["Neck Nod Down-Up"] = 0.3f });
            Both(settled, "Arm Down-Up", 0.5f);
            Both(settled, "Forearm Stretch", 0.8f);

            return new List<Beat>
            {
                new Beat(0f, Idle()), new Beat(0.14f, launch), new Beat(0.34f, layout),
                new Beat(0.47f, layout, 0.3f), new Beat(0.65f, settled),
            };
        }

        // ---------- Baking ----------

        static float Lead(string prop)
        {
            if (prop == Pitch || prop == Height) return 0f;
            if (prop.Contains("Leg") || prop.Contains("Foot") || prop.Contains("Toes")) return LegLead;
            if (prop.Contains("Head") || prop.Contains("Neck")) return HeadLag;
            if (prop.Contains("Arm") || prop.Contains("Hand") || prop.Contains("Shoulder")) return ArmLag;
            return 0f;
        }

        static void Save(string name, Dictionary<string, float> basePose, List<Beat> beats, Ground ground,
            System.Func<string, float> lead = null, float fps = FrameRate)
        {
            // Every property any beat touches, plus everything the idle frame has.
            var props = new HashSet<string>(basePose.Keys);
            foreach (var b in beats) foreach (var k in b.pose.Keys) props.Add(k);

            // Resolve each beat to full values, then apply drift toward the following beat.
            int n = beats.Count;
            var times = new float[n];
            var resolved = new Dictionary<string, float>[n];
            for (int i = 0; i < n; i++)
            {
                times[i] = beats[i].time;
                resolved[i] = new Dictionary<string, float>();
                foreach (var p in props)
                    resolved[i][p] = beats[i].pose.TryGetValue(p, out float v) ? v : basePose.TryGetValue(p, out float bv) ? bv : 0f;
            }
            for (int i = n - 2; i >= 0; i--)
            {
                if (beats[i].drift <= 0f) continue;
                foreach (var p in props)
                    resolved[i][p] = Mathf.Lerp(resolved[i][p], resolved[i + 1][p], beats[i].drift);
            }

            float length = times[n - 1];
            int frames = Mathf.RoundToInt(length * fps);
            var curves = new Dictionary<string, AnimationCurve>();
            foreach (var p in props)
            {
                var values = new float[n];
                for (int i = 0; i < n; i++) values[i] = resolved[i][p];
                var curve = new AnimationCurve();
                float offset = (lead ?? Lead)(p);
                for (int f = 0; f <= frames; f++)
                {
                    float t = f / fps;
                    curve.AddKey(t, Spline(times, values, Mathf.Clamp(t + offset, 0f, length)));
                }
                curves[p] = curve;
            }

            string path = $"{ClipDir}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.frameRate = fps;

            foreach (var kv in curves)
            {
                if (kv.Key == Pitch || kv.Key == Height) continue;
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), kv.Key), Smooth(kv.Value));
            }

            // Root: pitch becomes a real rotation every frame; height is normalized body height.
            float[] heights = ground == Ground.None ? null : GroundedHeights(curves, frames, fps, basePose[Height], ground);
            var qx = new AnimationCurve(); var qy = new AnimationCurve(); var qz = new AnimationCurve(); var qw = new AnimationCurve();
            var ty = new AnimationCurve(); var zero = new AnimationCurve();
            for (int f = 0; f <= frames; f++)
            {
                float t = f / fps;
                var q = Quaternion.Euler(curves[Pitch].Evaluate(t), 0f, 0f);
                qx.AddKey(t, q.x); qy.AddKey(t, q.y); qz.AddKey(t, q.z); qw.AddKey(t, q.w);
                ty.AddKey(t, heights != null ? heights[f] : curves[Height].Evaluate(t));
                zero.AddKey(t, 0f);
            }
            void Root(string prop, AnimationCurve c) =>
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), prop), Smooth(c));
            Root("RootQ.x", qx); Root("RootQ.y", qy); Root("RootQ.z", qz); Root("RootQ.w", qw);
            Root("RootT.x", zero); Root("RootT.y", ty); Root("RootT.z", zero);

            // Root stays in the pose: the motor owns position and facing. Height is used as baked
            // ("Original"): the ground lock already put the soles on the sand.
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.stopTime = length;
            settings.loopBlendOrientation = true;
            settings.keepOriginalOrientation = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalPositionXZ = true;
            settings.loopBlendPositionY = true;
            settings.keepOriginalPositionY = true;
            settings.heightFromFeet = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        /// <summary>
        /// Poses the real rig at every frame and finds the body height that keeps the lowest foot
        /// where it is in the Idle stance. Body height scales linearly with the clip's RootT.y.
        /// </summary>
        static float[] GroundedHeights(Dictionary<string, AnimationCurve> curves, int frames, float fps, float idleHeight, Ground ground)
        {
            var heights = new float[frames + 1];
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(MixamoSetup.RiggedModelPath);
            var go = (GameObject)Object.Instantiate(source);
            HumanPoseHandler handler = null;
            try
            {
                var animator = go.GetComponent<Animator>();
                LoadIdle().SampleAnimation(go, 0f);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                handler = new HumanPoseHandler(animator.avatar, go.transform);
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                float y0 = pose.bodyPosition.y;
                float idleFoot = Lowest(animator, Feet);
                var idleMuscles = (float[])pose.muscles.Clone();
                var names = HumanTrait.MuscleName;
                // Lying down, hands, shins, chest and head can be the lowest point too.
                var contacts = ground == Ground.Floor ? FloorContacts : Feet;

                for (int f = 0; f <= frames; f++)
                {
                    float t = f / fps;
                    for (int i = 0; i < names.Length; i++)
                        pose.muscles[i] = curves.TryGetValue(names[i], out var c) ? c.Evaluate(t) : idleMuscles[i];
                    pose.bodyRotation = Quaternion.Euler(curves[Pitch].Evaluate(t), 0f, 0f);
                    pose.bodyPosition = new Vector3(0f, y0, 0f);
                    handler.SetHumanPose(ref pose);
                    float shift = idleFoot - Lowest(animator, contacts);
                    float locked = idleHeight * (y0 + shift) / y0;
                    heights[f] = ground == Ground.Lock ? locked : Mathf.Max(curves[Height].Evaluate(t), locked);
                }
            }
            finally
            {
                handler?.Dispose();
                Object.DestroyImmediate(go);
            }
            return heights;
        }

        static readonly HumanBodyBones[] Feet =
            { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };

        static readonly HumanBodyBones[] FloorContacts =
        {
            HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.Chest, HumanBodyBones.Head,
        };

        static float Lowest(Animator animator, HumanBodyBones[] bones)
        {
            float lowest = float.MaxValue;
            foreach (var b in bones)
            {
                var bone = animator.GetBoneTransform(b);
                if (bone != null) lowest = Mathf.Min(lowest, bone.position.y);
            }
            return lowest;
        }

        /// <summary>
        /// Monotone cubic (Fritsch–Carlson) through the beats: velocity carries through a key
        /// instead of stopping there, but never overshoots. The first and last beats ease in and out.
        /// </summary>
        static float Spline(float[] t, float[] v, float x)
        {
            int n = t.Length;
            if (x <= t[0]) return v[0];
            if (x >= t[n - 1]) return v[n - 1];
            int i = 0;
            while (i < n - 2 && x > t[i + 1]) i++;

            float Slope(int k) => (v[k + 1] - v[k]) / Mathf.Max(t[k + 1] - t[k], 1e-4f);
            float Tangent(int k)
            {
                if (k == 0 || k == n - 1) return 0f;
                float a = Slope(k - 1), b = Slope(k);
                if (a * b <= 0f) return 0f;
                float m = (v[k + 1] - v[k - 1]) / (t[k + 1] - t[k - 1]);
                float limit = 3f * Mathf.Min(Mathf.Abs(a), Mathf.Abs(b));
                return Mathf.Sign(m) * Mathf.Min(Mathf.Abs(m), limit);
            }

            float h = t[i + 1] - t[i];
            float u = (x - t[i]) / h;
            float u2 = u * u, u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * v[i] + (u3 - 2f * u2 + u) * h * Tangent(i)
                 + (-2f * u3 + 3f * u2) * v[i + 1] + (u3 - u2) * h * Tangent(i + 1);
        }

        static AnimationCurve Smooth(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Auto);
            }
            return c;
        }
    }
}
