using System.Collections.Generic;
using System.IO;
using BeachVolley.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BeachVolley.EditorTools
{
    /// <summary>
    /// Turns the Mixamo downloads into a playable humanoid: imports the rigged character and
    /// the clips as Humanoid (clips share the character's avatar), builds the player's
    /// Animator controller, and swaps the rigged character into PlayerRookie.
    /// Root motion stays off; PlayerMotor moves the body. Clips that crouch, lie down or get
    /// up bake their height into the pose so the body actually goes down; Jump does not,
    /// because the motor already lifts the player. The attack arm (cock, spike swing, roll swing)
    /// and the block play on an upper-body layer so the legs keep whatever the base layer is doing.
    /// </summary>
    public static class MixamoSetup
    {
        public const string RiggedModelPath = "Assets/Models/Mixamo/KareszPlayer_Rigged.fbx";
        const string ClipDir = "Assets/Animations/Player";
        const string ControllerPath = ClipDir + "/PlayerAnimator.controller";
        const string UpperBodyMaskPath = ClipDir + "/UpperBody.mask";
        const string PlayerPrefabPath = "Assets/Prefabs/PlayerRookie.prefab";
        const float TargetHeight = 1.8f;

        // How long each action should take on screen (seconds); the state speed stretches the clip to fit.
        const float BumpTime = 0.7f;
        const float SetTime = 0.75f;
        // Must match PlayerMotor's dive: dash + layout, then get-up.
        const float DiveTime = 0.6f;
        const float GetUpTime = 1f;

        // Air blend: vertical speed (m/s) at which the rise / fall poses are fully in.
        const float RiseSpeed = 1.5f;
        const float FallSpeed = -1.5f;

        static readonly string[] LoopingClips = { "Idle", "Walk", "Run" };
        static readonly string[] HeightInPoseClips = { "Idle", "Walk", "Run", "Bump", "Set", "Dive", "GetUp" };

        // Frame ranges (30 fps) cut out of the Mixamo takes, found from the clips' root-height curves.
        // Jump: 0–14 wind-up crouch, 14–24 rise to the apex, 24–33 fall, then the landing crouch.
        // The motor already handles takeoff and landing, so only the airborne part is used.
        static readonly (string source, string name, int first, int last)[] Takes =
        {
            ("Jump", "JumpRise", 14, 24),
            ("Jump", "JumpFall", 24, 33),
            // Stand Up lies still until frame 10 and is standing by 52; the rest is idle.
            ("GetUp", "GetUp", 10, 52),
        };

        [MenuItem("Beach Volleyball/Setup Mixamo Character")]
        public static void Setup()
        {
            var avatar = ImportCharacter();
            if (avatar == null) return;
            var clips = ImportClips(avatar);
            if (!clips.ContainsKey("Idle"))
            {
                Debug.LogError($"[MixamoSetup] {ClipDir}/Idle.fbx is required.");
                return;
            }
            var cycleSpeeds = MeasureCycleSpeeds(clips, MeasureWorldScale());
            var controller = BuildController(clips, cycleSpeeds);
            ApplyToPlayerPrefab(controller, cycleSpeeds.TryGetValue("Run", out float run) ? run : 5.5f);
            Debug.Log($"[MixamoSetup] Done: {clips.Count} clips, controller {ControllerPath}, PlayerRookie updated.");
        }

        static Avatar ImportCharacter()
        {
            var importer = AssetImporter.GetAtPath(RiggedModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[MixamoSetup] Missing {RiggedModelPath}. Download the character from Mixamo (FBX, With Skin) and save it there.");
                return null;
            }
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.SaveAndReimport();

            Avatar avatar = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(RiggedModelPath))
                if (o is Avatar a) avatar = a;
            if (avatar == null || !avatar.isHuman)
            {
                Debug.LogError("[MixamoSetup] Character imported but the humanoid avatar is invalid. Check the Rig tab of " + RiggedModelPath);
                return null;
            }
            return avatar;
        }

        static Dictionary<string, AnimationClip> ImportClips(Avatar avatar)
        {
            var clips = new Dictionary<string, AnimationClip>();
            if (!Directory.Exists(ClipDir)) return clips;

            foreach (string file in Directory.GetFiles(ClipDir, "*.fbx"))
            {
                string path = file.Replace('\\', '/');
                string name = Path.GetFileNameWithoutExtension(path);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar;
                importer.importAnimation = true;

                var settings = importer.defaultClipAnimations;
                if (settings.Length == 0)
                {
                    Debug.LogWarning($"[MixamoSetup] {path} has no animation, skipped.");
                    continue;
                }

                // One clip per take range for this file, or the whole take.
                var outClips = new List<ModelImporterClipAnimation>();
                foreach (var take in Takes)
                {
                    if (take.source != name) continue;
                    var cut = Configure(settings[0], take.name);
                    cut.firstFrame = take.first;
                    cut.lastFrame = take.last;
                    outClips.Add(cut);
                }
                if (outClips.Count == 0) outClips.Add(Configure(settings[0], name));
                importer.clipAnimations = outClips.ToArray();
                importer.SaveAndReimport();

                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                        clips[c.name] = c;
            }

            // Hand-authored humanoid clips sit next to the FBXs; some are built from the takes above.
            VolleyballClipAuthor.AuthorAll();
            foreach (string file in Directory.GetFiles(ClipDir, "*.anim"))
            {
                string path = file.Replace('\\', '/');
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null) clips[Path.GetFileNameWithoutExtension(path)] = clip;
            }
            return clips;
        }

        static ModelImporterClipAnimation Configure(ModelImporterClipAnimation source, string name)
        {
            var clip = new ModelImporterClipAnimation
            {
                name = name,
                takeName = source.takeName,
                firstFrame = source.firstFrame,
                lastFrame = source.lastFrame,
            };
            clip.loopTime = System.Array.IndexOf(LoopingClips, name) >= 0;
            clip.loopPose = clip.loopTime;
            // Facing and ground position come from the motor, not the clip.
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            // Walk/Run travel forward and Stand Up steps back; that travel must stay root motion
            // (ignored, since the motor moves the player) or the body drifts and snaps back.
            bool travels = name == "Walk" || name == "Run" || name == "GetUp";
            clip.lockRootPositionXZ = !travels;
            clip.keepOriginalPositionXZ = true;
            bool heightInPose = System.Array.IndexOf(HeightInPoseClips, name) >= 0;
            clip.lockRootHeightY = heightInPose;
            clip.keepOriginalPositionY = heightInPose;
            return clip;
        }

        /// <summary>
        /// Metres per normalized humanoid unit on the placed player (avatar human scale × model scale),
        /// so clip travel speeds can be compared with PlayerMotor speeds.
        /// </summary>
        static float MeasureWorldScale()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedModelPath);
            var model = new GameObject("__MeasureModel").transform;
            var mesh = (GameObject)Object.Instantiate(source, model);
            float placedScale = PlaceRigged(model, mesh);
            float scale = mesh.GetComponent<Animator>().humanScale * placedScale;
            Object.DestroyImmediate(model.gameObject);
            return scale;
        }

        /// <summary>How fast (m/s on the placed player) each locomotion cycle really travels, from its root curve.</summary>
        static Dictionary<string, float> MeasureCycleSpeeds(Dictionary<string, AnimationClip> clips, float worldScale)
        {
            var speeds = new Dictionary<string, float>();
            foreach (var name in new[] { "Walk", "Run" })
            {
                if (!clips.TryGetValue(name, out var clip) || clip.length <= 0f) continue;
                AnimationCurve x = null, z = null;
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                {
                    if (b.propertyName == "RootT.x") x = AnimationUtility.GetEditorCurve(clip, b);
                    if (b.propertyName == "RootT.z") z = AnimationUtility.GetEditorCurve(clip, b);
                }
                if (x == null || z == null) continue;
                var travel = new Vector2(x.Evaluate(clip.length) - x.Evaluate(0f), z.Evaluate(clip.length) - z.Evaluate(0f));
                speeds[name] = travel.magnitude / clip.length * worldScale;
            }
            return speeds;
        }

        static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, Dictionary<string, float> cycleSpeeds)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Diving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("LocoSpeedMul", AnimatorControllerParameterType.Float);
            controller.AddParameter("Cocked", AnimatorControllerParameterType.Bool);
            foreach (var t in new[] { "Bump", "Set", "Spike", "Roll", "Dive", "Block" })
                controller.AddParameter(t, AnimatorControllerParameterType.Trigger);
            // Start grounded so the first frame isn't a fall; cycles play at their own speed by default.
            var ps = controller.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].name == "Grounded") ps[i].defaultBool = true;
                if (ps[i].name == "LocoSpeedMul") ps[i].defaultFloat = 1f;
            }
            controller.parameters = ps;

            var sm = controller.layers[0].stateMachine;

            // Locomotion thresholds are the speeds the cycles really travel at, so the feet don't
            // slide; above the run speed the driver speeds the cycle up via LocoSpeedMul.
            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips["Idle"], 0f);
            if (clips.TryGetValue("Walk", out var walk))
                tree.AddChild(walk, cycleSpeeds.TryGetValue("Walk", out float ws) ? ws : 1.6f);
            if (clips.TryGetValue("Run", out var run))
                tree.AddChild(run, cycleSpeeds.TryGetValue("Run", out float rs) ? rs : 5.5f);
            locomotion.speedParameterActive = true;
            locomotion.speedParameter = "LocoSpeedMul";
            sm.defaultState = locomotion;

            // AirRise/AirFall are the Mixamo jump takes re-rooted at standing height (VolleyballClipAuthor).
            if (clips.TryGetValue("AirRise", out var rise) && clips.TryGetValue("AirFall", out var fall))
            {
                // Rising pose while going up, falling pose on the way down, blended through the apex.
                var air = controller.CreateBlendTreeInController("Air", out var airTree, 0);
                airTree.blendType = BlendTreeType.Simple1D;
                airTree.blendParameter = "VerticalSpeed";
                airTree.useAutomaticThresholds = false;
                airTree.AddChild(fall, FallSpeed);
                airTree.AddChild(rise, RiseSpeed);
                Transition(locomotion, air, 0.15f).AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
                Transition(air, locomotion, 0.15f).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            }

            AddAction(sm, locomotion, clips, "Bump", "Bump", BumpTime);
            AddAction(sm, locomotion, clips, "Set", "Set", SetTime);

            if (clips.TryGetValue("Dive", out var diveClip))
            {
                var dive = sm.AddState("Dive");
                dive.motion = diveClip;
                dive.speed = diveClip.length / DiveTime;
                Entry(sm, dive, "Dive", 0.1f);

                AnimatorState after = locomotion;
                if (clips.TryGetValue("GetUp", out var getUpClip))
                {
                    var getUp = sm.AddState("GetUp");
                    getUp.motion = getUpClip;
                    getUp.speed = getUpClip.length / GetUpTime;
                    var toGetUp = Transition(dive, getUp, 0.15f);
                    toGetUp.hasExitTime = true;
                    toGetUp.exitTime = 0.95f;
                    after = getUp;
                }
                // Hold the get-up until the motor says the dive is over.
                var done = Transition(after, locomotion, 0.25f);
                done.AddCondition(AnimatorConditionMode.IfNot, 0, "Diving");
                if (after == locomotion) done.hasExitTime = true;
            }

            BuildUpperBodyLayer(controller, clips);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>
        /// Attack arm and block on an upper-body layer, weight driven by PlayerAnimatorDriver.
        /// Cocked (jumping near a high ball, or a live serve toss) raises the arms into the
        /// bow-and-arrow and holds it; Spike / Roll swing forward from there at native speed, so the
        /// clip's contact beat lines up with BallContact's contact delay. After a swing the arm goes
        /// back to the cock if still cocked, otherwise the layer empties.
        /// </summary>
        static void BuildUpperBodyLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            if (!clips.TryGetValue("SpikeCock", out var cockClip)) return;

            controller.AddLayer("UpperBody");
            var layers = controller.layers;
            int index = layers.Length - 1;
            layers[index].avatarMask = UpperBodyMask();
            layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[index].defaultWeight = 0f;
            controller.layers = layers;

            var sm = controller.layers[index].stateMachine;
            var empty = sm.AddState("Empty");
            empty.writeDefaultValues = false;
            sm.defaultState = empty;

            var cock = sm.AddState("Cock");
            cock.motion = cockClip;
            Transition(empty, cock, 0.15f).AddCondition(AnimatorConditionMode.If, 0, "Cocked");
            Transition(cock, empty, 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0, "Cocked");

            var swings = new List<(string state, string trigger, AnimationClip clip, float blend, bool backToCock)>();
            if (clips.TryGetValue("SpikeSwing", out var spike)) swings.Add(("SpikeSwing", "Spike", spike, 0.06f, true));
            if (clips.TryGetValue("RollSwing", out var roll)) swings.Add(("RollSwing", "Roll", roll, 0.06f, true));
            if (clips.TryGetValue("Block", out var blockClip)) swings.Add(("Block", "Block", blockClip, 0.1f, false));
            foreach (var (stateName, trigger, clip, blend, backToCock) in swings)
            {
                var state = sm.AddState(stateName);
                state.motion = clip;
                Entry(sm, state, trigger, blend);
                if (backToCock)
                {
                    var again = Transition(state, cock, 0.12f);
                    again.hasExitTime = true;
                    again.exitTime = 1f;
                    again.AddCondition(AnimatorConditionMode.If, 0, "Cocked");
                }
                // The driver has faded the layer out by now; this just resets for the next swing.
                var back = Transition(state, empty, 0f);
                back.hasExitTime = true;
                back.exitTime = 1f;
                if (backToCock) back.AddCondition(AnimatorConditionMode.IfNot, 0, "Cocked");
            }
        }

        static AvatarMask UpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, false);
            foreach (var part in new[]
            {
                AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
                AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
                AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
            })
                mask.SetHumanoidBodyPartActive(part, true);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        static void AddAction(AnimatorStateMachine sm, AnimatorState back, Dictionary<string, AnimationClip> clips,
            string trigger, string clipName, float time)
        {
            if (!clips.TryGetValue(clipName, out var clip)) return;
            var state = sm.AddState(trigger);
            state.motion = clip;
            state.speed = clip.length / time;
            Entry(sm, state, trigger, 0.15f);
            // Clips end on the idle stance, so a long, late blend back is seamless.
            var exit = Transition(state, back, 0.25f);
            exit.hasExitTime = true;
            exit.exitTime = 0.75f;
        }

        static AnimatorStateTransition Entry(AnimatorStateMachine sm, AnimatorState state, string trigger, float duration)
        {
            var enter = sm.AddAnyStateTransition(state);
            enter.duration = duration;
            enter.hasFixedDuration = true;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0, trigger);
            return enter;
        }

        static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = duration;
            return t;
        }

        /// <summary>
        /// Replaces the static mesh under PlayerRookie/Model with the rigged character, standing on
        /// the sand, wearing its own material, driven by the new controller.
        /// </summary>
        static void ApplyToPlayerPrefab(AnimatorController controller, float runCycleSpeed)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            var cc = root.GetComponent<CharacterController>();
            var oldModel = root.transform.Find("Model");
            if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedModelPath);
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localPosition = new Vector3(0f, -cc.skinWidth, 0f);
            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(source);
            mesh.transform.SetParent(model, false);
            ConfigureCharacter(mesh, controller);
            PlaceRigged(model, mesh);

            var driver = root.GetComponent<PlayerAnimatorDriver>();
            if (driver == null) driver = root.AddComponent<PlayerAnimatorDriver>();
            var driverSo = new SerializedObject(driver);
            driverSo.FindProperty("runCycleSpeed").floatValue = runCycleSpeed;
            driverSo.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(root.GetComponent<BallContact>());
            so.FindProperty("leftArmPivot").objectReferenceValue = null;
            so.FindProperty("rightArmPivot").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>Animator + original material on a rigged character instance. Shared with FoundationBuilder.</summary>
        public static void ConfigureCharacter(GameObject mesh, RuntimeAnimatorController controller)
        {
            var animator = mesh.GetComponent<Animator>();
            if (animator == null) animator = mesh.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller != null
                ? controller
                : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(RiggedModelPath))
                if (o is Avatar a) animator.avatar = a;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // The rigged FBX brings its own textured material (embedded textures extracted next to it).
            foreach (var r in mesh.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
        }

        /// <summary>
        /// Sizes and places a rigged character under <paramref name="model"/> the way the humanoid
        /// Animator will actually show it. The Animator rebuilds the body from the clip relative to
        /// the character's own transform, ignoring the rest pose, so fitting to rest-pose bounds
        /// leaves the player floating. The height comes from the rest pose, but the offset is
        /// measured on the Idle pose: hips over the controller, soles on the sand. Returns the scale.
        /// Shared with FoundationBuilder.
        /// </summary>
        public static float PlaceRigged(Transform model, GameObject mesh)
        {
            var t = mesh.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            var animator = mesh.GetComponent<Animator>();

            // Rest pose: overall height, and how far the lowest foot bone sits above the sole.
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in mesh.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            float scale = bounds.size.y > 0.001f ? TargetHeight / bounds.size.y : 1f;
            float sole = LowestFootBone(animator, model) - model.InverseTransformPoint(bounds.min).y;
            t.localScale = Vector3.one * scale;

            AnimationClip idle = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ClipDir + "/Idle.fbx"))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) idle = c;
            if (idle != null)
            {
                idle.SampleAnimation(mesh, 0f);
                // Sampling a humanoid clip can move the character's own transform; undo that.
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
            }

            Vector3 hips = model.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position);
            float lowest = LowestFootBone(animator, model);
            t.localPosition = new Vector3(-hips.x, sole * scale - lowest, -hips.z);
            return scale;
        }

        static float LowestFootBone(Animator animator, Transform space)
        {
            float lowest = float.MaxValue;
            foreach (var b in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes })
            {
                var bone = animator.GetBoneTransform(b);
                if (bone != null) lowest = Mathf.Min(lowest, space.InverseTransformPoint(bone.position).y);
            }
            return lowest;
        }
    }
}
