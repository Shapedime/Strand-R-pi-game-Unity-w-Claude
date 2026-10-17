using BeachVolley.Ball;
using BeachVolley.CameraRig;
using BeachVolley.Court;
using BeachVolley.Menu;
using BeachVolley.Player;
using BeachVolley.Practice;
using BeachVolley.Skills;
using BeachVolley.Story;
using BeachVolley.Training;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BeachVolley.EditorTools
{
    /// <summary>
    /// Builds the training-court foundation: materials, Court / Net / PlayerRookie prefabs,
    /// and lays them out under World/ in SampleScene. Safe to re-run; it rebuilds everything.
    /// </summary>
    public static class FoundationBuilder
    {
        public const string ScenePath = "Assets/Scenes/Beach.unity";
        const string OldScenePath = "Assets/Scenes/SampleScene.unity";
        const string PrefabDir = "Assets/Prefabs";
        const string MaterialDir = "Assets/Materials";
        const string TextureDir = "Assets/Textures";
        const string NetTexturePath = TextureDir + "/NetGrid.png";
        const string PhysicsDir = "Assets/Physics";
        const string DataDir = "Assets/Data";
        const string RookieProfilePath = DataDir + "/RookieSkills.asset";

        const string PlayerModelPath = "Assets/Models/KareszPlayer.glb";
        const float PlayerHeight = 1.8f;
        // Rotation that turns the imported mesh to face +Z.
        const float PlayerModelYaw = 0f;

        const float SandSize = 60f;
        const float LineThickness = 0.01f;

        [MenuItem("Beach Volleyball/Build Foundation")]
        public static void Build()
        {
            EnsureFolder(PrefabDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(TextureDir);

            EnsureFolder(PhysicsDir);
            EnsureFolder(DataDir);

            var mats = new Materials();
            var phys = new PhysicsMaterials();
            GameObject courtPrefab = SavePrefab(BuildCourt(mats, phys), "Court");
            GameObject netPrefab = SavePrefab(BuildNet(mats, phys), "Net");
            GameObject playerPrefab = SavePrefab(BuildPlayer(mats, phys), "PlayerRookie");
            GameObject ballPrefab = SavePrefab(BuildBall(mats, phys), "Volleyball");
            // Training prefabs are still saved so the coach-led training can come back later.
            GameObject coachPrefab = SavePrefab(BuildCoach(mats), "Coach");
            GameObject markerPrefab = SavePrefab(BuildMarker(mats), "TrainingMarker");
            GameObject signPrefab = SavePrefab(BuildSign(mats), "TrainingSign");

            // The court scene used to be SampleScene; it's the Beach scene now.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null && AssetDatabase.LoadAssetAtPath<SceneAsset>(OldScenePath) != null)
                AssetDatabase.MoveAsset(OldScenePath, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var oldWorld = GameObject.Find("World");
            if (oldWorld != null) Object.DestroyImmediate(oldWorld);

            var world = new GameObject("World");
            Instantiate(courtPrefab, world.transform, Vector3.zero, Quaternion.identity);
            Instantiate(netPrefab, world.transform, Vector3.zero, Quaternion.identity);
            var player = Instantiate(playerPrefab, world.transform,
                new Vector3(0f, 0f, -CourtDimensions.SideLength * 0.75f), Quaternion.identity);
            var ball = Instantiate(ballPrefab, world.transform,
                player.transform.position + new Vector3(0f, VolleyBall.Radius + 0.01f, 1.2f), Quaternion.identity);

            var contact = new SerializedObject(player.GetComponent<BallContact>());
            contact.FindProperty("ball").objectReferenceValue = ball.GetComponent<VolleyBall>();
            contact.ApplyModifiedPropertiesWithoutUndo();

            var serveSo = new SerializedObject(player.GetComponent<PlayerServe>());
            serveSo.FindProperty("ball").objectReferenceValue = ball.GetComponent<VolleyBall>();
            serveSo.ApplyModifiedPropertiesWithoutUndo();

            // The coach-led training (coach, TrainingSession/TrainingStory, sign) is switched off for
            // now; its prefabs and BuildTraining stay for when it comes back. Free practice instead.
            BuildPractice(world.transform, player, ball, mats);
            BuildScenery(world.transform, mats);
            ConfigureLight(world.transform);
            ConfigureCamera(player.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            MenuBuilder.ApplyBuildScenes();
            Debug.Log("[FoundationBuilder] Court, Net, PlayerRookie, ball and practice built into " + ScenePath);
        }

        // ---------- Practice ----------

        /// <summary>
        /// Free practice: the ball machine (with its launcher model), target zones, and the Tab
        /// practice options + Esc pause menus.
        /// </summary>
        static void BuildPractice(Transform world, GameObject player, GameObject ball, Materials mats)
        {
            var practice = new GameObject("Practice").transform;
            practice.SetParent(world, false);

            var launcher = new GameObject("BallMachine").transform;
            launcher.SetParent(practice, false);
            Part(PrimitiveType.Cylinder, "Stand", launcher, new Vector3(0f, 0.4f, 0f), new Vector3(0.3f, 0.4f, 0.3f), mats.MachineDark);
            Part(PrimitiveType.Cube, "Body", launcher, new Vector3(0f, 0.95f, 0f), new Vector3(0.62f, 0.42f, 0.62f), mats.Machine);
            var barrel = Part(PrimitiveType.Cylinder, "Barrel", launcher, new Vector3(0f, 1.12f, 0.36f), new Vector3(0.24f, 0.22f, 0.24f), mats.MachineDark);
            barrel.localRotation = Quaternion.Euler(70f, 0f, 0f);
            foreach (var c in launcher.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            var machine = practice.gameObject.AddComponent<BallMachine>();
            var so = new SerializedObject(machine);
            so.FindProperty("ball").objectReferenceValue = ball.GetComponent<VolleyBall>();
            so.FindProperty("player").objectReferenceValue = player.transform;
            so.FindProperty("contact").objectReferenceValue = player.GetComponent<BallContact>();
            so.FindProperty("serve").objectReferenceValue = player.GetComponent<PlayerServe>();
            so.FindProperty("visual").objectReferenceValue = launcher;
            so.ApplyModifiedPropertiesWithoutUndo();

            var zonesGo = new GameObject("TargetZones");
            zonesGo.transform.SetParent(practice, false);
            var zones = zonesGo.AddComponent<TargetZones>();
            var zso = new SerializedObject(zones);
            zso.FindProperty("ball").objectReferenceValue = ball.GetComponent<VolleyBall>();
            zso.FindProperty("player").objectReferenceValue = player.transform;
            zso.FindProperty("contact").objectReferenceValue = player.GetComponent<BallContact>();
            zso.FindProperty("serve").objectReferenceValue = player.GetComponent<PlayerServe>();
            zso.ApplyModifiedPropertiesWithoutUndo();

            var panel = MenuBuilder.MenuPanelSettings();
            var practiceUi = new GameObject("PracticeMenu");
            practiceUi.transform.SetParent(practice, false);
            var practiceDoc = practiceUi.AddComponent<UnityEngine.UIElements.UIDocument>();
            practiceDoc.panelSettings = panel;
            practiceDoc.sortingOrder = 0;
            var practiceMenu = practiceUi.AddComponent<PracticeMenu>();
            var pmso = new SerializedObject(practiceMenu);
            pmso.FindProperty("machine").objectReferenceValue = machine;
            pmso.FindProperty("zones").objectReferenceValue = zones;
            pmso.ApplyModifiedPropertiesWithoutUndo();

            var pauseUi = new GameObject("PauseMenu");
            pauseUi.transform.SetParent(practice, false);
            var pauseDoc = pauseUi.AddComponent<UnityEngine.UIElements.UIDocument>();
            pauseDoc.panelSettings = panel;
            pauseDoc.sortingOrder = 10;
            var pause = pauseUi.AddComponent<PauseMenu>();
            var pso = new SerializedObject(pause);
            pso.FindProperty("practiceMenu").objectReferenceValue = practiceMenu;
            pso.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The beach without gameplay: court, net, scenery, sun and sky. Used behind the main menu.
        /// Needs the Court and Net prefabs from Build Foundation.
        /// </summary>
        public static void BuildBackdrop(Transform world)
        {
            var mats = new Materials();
            var court = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Court.prefab");
            var net = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Net.prefab");
            if (court != null) Instantiate(court, world, Vector3.zero, Quaternion.identity);
            if (net != null) Instantiate(net, world, Vector3.zero, Quaternion.identity);
            BuildScenery(world, mats);
            ConfigureLight(world);
        }

        // ---------- Court ----------

        static GameObject BuildCourt(Materials mats, PhysicsMaterials phys)
        {
            var root = new GameObject("Court");
            // Soft bounds slow the player well before the hard walls below.
            root.AddComponent<SoftBounds>();

            var sand = Box("Sand", root.transform, new Vector3(0f, -0.1f, 0f), new Vector3(SandSize, 0.2f, SandSize), mats.Sand, keepCollider: true);
            sand.GetComponent<Collider>().sharedMaterial = phys.Sand;
            sand.isStatic = true;

            // Invisible safety walls at the sand's edge; SoftBounds normally stops the player first.
            var bounds = new GameObject("SandBounds").transform;
            bounds.SetParent(root.transform, false);
            float edge = SandSize * 0.5f;
            Wall(bounds, "North", new Vector3(0f, 1f, edge), new Vector3(SandSize, 2f, 0.5f));
            Wall(bounds, "South", new Vector3(0f, 1f, -edge), new Vector3(SandSize, 2f, 0.5f));
            Wall(bounds, "East", new Vector3(edge, 1f, 0f), new Vector3(0.5f, 2f, SandSize));
            Wall(bounds, "West", new Vector3(-edge, 1f, 0f), new Vector3(0.5f, 2f, SandSize));

            // Lines sit inside the 16 x 8 m footprint, as on a real court.
            var lines = new GameObject("Lines").transform;
            lines.SetParent(root.transform, false);
            float lw = CourtDimensions.LineWidth;
            float hw = CourtDimensions.HalfWidth;
            float hl = CourtDimensions.HalfLength;
            float y = LineThickness * 0.5f;
            Box("Sideline_L", lines, new Vector3(-hw + lw * 0.5f, y, 0f), new Vector3(lw, LineThickness, CourtDimensions.TotalLength), mats.Line);
            Box("Sideline_R", lines, new Vector3(hw - lw * 0.5f, y, 0f), new Vector3(lw, LineThickness, CourtDimensions.TotalLength), mats.Line);
            Box("Baseline_Near", lines, new Vector3(0f, y, -hl + lw * 0.5f), new Vector3(CourtDimensions.Width, LineThickness, lw), mats.Line);
            Box("Baseline_Far", lines, new Vector3(0f, y, hl - lw * 0.5f), new Vector3(CourtDimensions.Width, LineThickness, lw), mats.Line);
            Box("CenterLine", lines, new Vector3(0f, y, 0f), new Vector3(CourtDimensions.Width, LineThickness, lw), mats.Line);

            return root;
        }

        static void Wall(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
        }

        // ---------- Net ----------

        static GameObject BuildNet(Materials mats, PhysicsMaterials phys)
        {
            var root = new GameObject("Net");
            float netTop = CourtDimensions.NetHeight;
            float netBottom = netTop - CourtDimensions.NetDepth;
            float netMid = (netTop + netBottom) * 0.5f;
            float width = CourtDimensions.Width;
            float postX = CourtDimensions.HalfWidth + CourtDimensions.PostOffset;
            float postH = CourtDimensions.PostHeight;

            foreach (float side in new[] { -1f, 1f })
            {
                string suffix = side < 0 ? "L" : "R";
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Post_" + suffix;
                post.transform.SetParent(root.transform, false);
                post.transform.localPosition = new Vector3(side * postX, postH * 0.5f, 0f);
                post.transform.localScale = new Vector3(0.1f, postH * 0.5f, 0.1f);
                post.GetComponent<Renderer>().sharedMaterial = mats.Post;
                post.GetComponent<Collider>().sharedMaterial = phys.Post;

                // Cables from the net edge out to the post, at top and bottom tape.
                float cableLen = postX - width * 0.5f;
                float cableX = side * (width * 0.5f + cableLen * 0.5f);
                Cable("CableTop_" + suffix, root.transform, new Vector3(cableX, netTop - 0.02f, 0f), cableLen, mats.Rope);
                Cable("CableBottom_" + suffix, root.transform, new Vector3(cableX, netBottom + 0.02f, 0f), cableLen, mats.Rope);

                // Vertical side bands over each sideline.
                Box("SideBand_" + suffix, root.transform, new Vector3(side * (width * 0.5f - 0.025f), netMid, 0f),
                    new Vector3(0.05f, CourtDimensions.NetDepth, 0.022f), mats.TapeBlue);

                // Red/white antenna on the outside edge of each side band, 0.8 m above the tape.
                var antenna = new GameObject("Antenna_" + suffix).transform;
                antenna.SetParent(root.transform, false);
                antenna.localPosition = new Vector3(side * width * 0.5f, netBottom, 0.03f);
                const int segments = 18;
                const float segment = 0.1f;
                for (int i = 0; i < segments; i++)
                {
                    var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    seg.name = "Seg" + i;
                    Object.DestroyImmediate(seg.GetComponent<Collider>());
                    seg.transform.SetParent(antenna, false);
                    seg.transform.localPosition = new Vector3(0f, segment * (i + 0.5f), 0f);
                    seg.transform.localScale = new Vector3(0.012f, segment * 0.5f, 0.012f);
                    seg.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? mats.AntennaRed : mats.Tape;
                }
            }

            var mesh = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mesh.name = "NetMesh";
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(0f, netMid, 0f);
            mesh.transform.localScale = new Vector3(width, CourtDimensions.NetDepth, 1f);
            var netRenderer = mesh.GetComponent<Renderer>();
            netRenderer.sharedMaterial = mats.Net;
            netRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // Blue 8 cm top tape with white piping on both edges; matching blue bottom tape.
            Box("TopTape", root.transform, new Vector3(0f, netTop - 0.04f, 0f), new Vector3(width, 0.08f, 0.03f), mats.TapeBlue);
            Box("TopPiping", root.transform, new Vector3(0f, netTop - 0.004f, 0f), new Vector3(width, 0.008f, 0.034f), mats.Tape);
            Box("TopPipingLow", root.transform, new Vector3(0f, netTop - 0.076f, 0f), new Vector3(width, 0.008f, 0.034f), mats.Tape);
            Box("BottomTape", root.transform, new Vector3(0f, netBottom + 0.025f, 0f), new Vector3(width, 0.05f, 0.022f), mats.TapeBlue);

            // One solid blocker from sand to tape so the player walks around the posts, not under the net.
            var blocker = root.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, netTop * 0.5f, 0f);
            blocker.size = new Vector3(postX * 2f, netTop, 0.1f);
            blocker.sharedMaterial = phys.Net;

            return root;
        }

        static void Cable(string name, Transform parent, Vector3 pos, float length, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            go.transform.localScale = new Vector3(0.012f, length * 0.5f, 0.012f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // ---------- Player ----------

        static GameObject BuildPlayer(Materials mats, PhysicsMaterials phys)
        {
            var root = new GameObject("PlayerRookie");
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.skinWidth = 0.03f;
            // Flat sand: keep the step low so the controller can't climb onto the ball (0.32 m).
            cc.stepOffset = 0.1f;
            cc.sharedMaterial = phys.Player;
            // Rookie stats live on the player for now; persistence comes later.
            var skills = root.AddComponent<PlayerSkills>();
            var skillsSo = new SerializedObject(skills);
            skillsSo.FindProperty("startingProfile").objectReferenceValue = RookieProfile();
            skillsSo.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<SkillDebugHud>();
            root.AddComponent<PlayerMotor>();
            var contact = root.AddComponent<BallContact>();
            root.AddComponent<PlayerServe>();

            // The controller rests skinWidth above the sand; drop the mesh so the feet touch it.
            // Prefer the Mixamo-rigged character once it exists; the static GLB is the fallback.
            var riggedModel = AssetDatabase.LoadAssetAtPath<GameObject>(MixamoSetup.RiggedModelPath);
            var characterModel = riggedModel != null ? riggedModel : AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            if (characterModel != null)
            {
                AttachCharacterModel(root.transform, -cc.skinWidth, characterModel);
                if (riggedModel != null)
                {
                    // A humanoid Animator ignores the rest pose, so re-place it from the Idle pose.
                    var model = root.transform.Find("Model");
                    var mesh = model.GetChild(0).gameObject;
                    MixamoSetup.ConfigureCharacter(mesh, null);
                    MixamoSetup.PlaceRigged(model, mesh);
                }
            }
            else
            {
                var armPivots = BuildBody(root.transform, -cc.skinWidth, mats.Tank, mats.Shorts, mats);
                var so = new SerializedObject(contact);
                so.FindProperty("leftArmPivot").objectReferenceValue = armPivots[0];
                so.FindProperty("rightArmPivot").objectReferenceValue = armPivots[1];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            root.AddComponent<PlayerModelAnimator>();
            root.AddComponent<PlayerAnimatorDriver>();

            return root;
        }

        static SkillProfile RookieProfile()
        {
            // Only created once, so tuning done in the Inspector survives rebuilds.
            var profile = AssetDatabase.LoadAssetAtPath<SkillProfile>(RookieProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<SkillProfile>();
            AssetDatabase.CreateAsset(profile, RookieProfilePath);
            return profile;
        }

        /// <summary>
        /// Puts the imported character mesh under a "Model" child, scaled to PlayerHeight with
        /// feet at yOffset and facing +Z. The mesh is one piece, so PlayerModelAnimator moves it as a whole.
        /// </summary>
        static void AttachCharacterModel(Transform root, float yOffset, GameObject source)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(root, false);
            model.localPosition = new Vector3(0f, yOffset, 0f);

            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(source);
            mesh.transform.SetParent(model, false);
            mesh.transform.localRotation = Quaternion.Euler(0f, PlayerModelYaw, 0f);

            var bounds = new Bounds();
            bool first = true;
            foreach (var r in mesh.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            float scale = bounds.size.y > 0.001f ? PlayerHeight / bounds.size.y : 1f;
            mesh.transform.localScale = Vector3.one * scale;
            // Centre over the controller with the soles on the model origin.
            Vector3 c = model.InverseTransformPoint(bounds.center);
            float bottom = model.InverseTransformPoint(bounds.min).y;
            mesh.transform.localPosition = new Vector3(-c.x * scale, -bottom * scale, -c.z * scale);
        }

        [MenuItem("Beach Volleyball/Apply Player Model")]
        public static void ApplyPlayerModel()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
            if (source == null) { Debug.LogError("[FoundationBuilder] Missing " + PlayerModelPath); return; }
            string prefabPath = PrefabDir + "/PlayerRookie.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var oldModel = root.transform.Find("Model");
            if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);
            var cc = root.GetComponent<CharacterController>();
            AttachCharacterModel(root.transform, -cc.skinWidth, source);
            // Old primitive arm pivots went with the old model.
            var so = new SerializedObject(root.GetComponent<BallContact>());
            so.FindProperty("leftArmPivot").objectReferenceValue = null;
            so.FindProperty("rightArmPivot").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (root.GetComponent<PlayerModelAnimator>() == null) root.AddComponent<PlayerModelAnimator>();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[FoundationBuilder] PlayerRookie now uses " + PlayerModelPath);
        }

        /// <summary>Placeholder body, ~1.80 m tall, from primitives. Feet at y = 0, facing +Z. Returns arm pivots (L, R).</summary>
        internal static Transform[] BuildBody(Transform root, float yOffset, Material tank, Material shorts, Materials mats)
        {
            var armPivots = new Transform[2];
            var model = new GameObject("Model").transform;
            model.SetParent(root, false);
            model.localPosition = new Vector3(0f, yOffset, 0f);

            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0 ? "L" : "R";
                Part(PrimitiveType.Cube, "Foot_" + side, model, new Vector3(s * 0.11f, 0.03f, 0.04f), new Vector3(0.1f, 0.06f, 0.24f), mats.Skin);
                Part(PrimitiveType.Capsule, "Leg_" + side, model, new Vector3(s * 0.11f, 0.47f, 0f), new Vector3(0.15f, 0.42f, 0.15f), mats.Skin);
                // Arms hang from a shoulder pivot so BallContact can swing them into a platform.
                var pivot = new GameObject("ArmPivot_" + side).transform;
                pivot.SetParent(model, false);
                pivot.localPosition = new Vector3(s * 0.25f, 1.45f, 0f);
                pivot.localRotation = Quaternion.Euler(0f, 0f, s * 8f);
                Part(PrimitiveType.Capsule, "Arm_" + side, pivot, new Vector3(0f, -0.3f, 0f), new Vector3(0.1f, 0.31f, 0.1f), mats.Skin);
                armPivots[s < 0 ? 0 : 1] = pivot;
                Part(PrimitiveType.Sphere, "Shoulder_" + side, model, new Vector3(s * 0.21f, 1.43f, 0f), new Vector3(0.13f, 0.13f, 0.13f), mats.Skin);
            }

            Part(PrimitiveType.Cube, "Shorts", model, new Vector3(0f, 0.88f, 0f), new Vector3(0.38f, 0.28f, 0.24f), shorts);
            Part(PrimitiveType.Cube, "TankTop", model, new Vector3(0f, 1.24f, 0f), new Vector3(0.36f, 0.5f, 0.22f), tank);
            // Player gets a big back number; the coach gets a plain patch.
            bool isPlayer = tank == mats.Tank;
            var number = Part(PrimitiveType.Cube, "TankNumber", model, new Vector3(0f, 1.26f, -0.112f),
                isPlayer ? new Vector3(0.22f, 0.26f, 0.005f) : new Vector3(0.12f, 0.16f, 0.005f), isPlayer ? mats.Number : mats.Line);
            // Seen on the cube's -Z face, which flips the texture 180°.
            number.localRotation = Quaternion.Euler(0f, 0f, 180f);
            foreach (float s in new[] { -1f, 1f })
                Part(PrimitiveType.Cube, "ShortsStripe", model, new Vector3(s * 0.192f, 0.88f, 0f), new Vector3(0.006f, 0.28f, 0.06f), mats.Line);
            Part(PrimitiveType.Cylinder, "Neck", model, new Vector3(0f, 1.52f, 0f), new Vector3(0.1f, 0.04f, 0.1f), mats.Skin);
            Part(PrimitiveType.Sphere, "Head", model, new Vector3(0f, 1.67f, 0f), new Vector3(0.22f, 0.25f, 0.23f), mats.Skin);
            Part(PrimitiveType.Sphere, "Hair", model, new Vector3(0f, 1.73f, -0.01f), new Vector3(0.235f, 0.15f, 0.24f), mats.Hair);
            // Visor brim makes facing direction obvious from behind the camera.
            Part(PrimitiveType.Cube, "VisorBrim", model, new Vector3(0f, 1.74f, 0.14f), new Vector3(0.2f, 0.015f, 0.1f), mats.Line);

            return armPivots;
        }

        // ---------- Training ----------

        static GameObject BuildCoach(Materials mats)
        {
            var root = new GameObject("Coach");
            var col = root.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.3f;
            col.center = new Vector3(0f, 0.9f, 0f);
            var pivots = BuildBody(root.transform, 0f, mats.CoachShirt, mats.CoachShorts, mats);
            // Coach holds one arm up, ready to toss.
            pivots[1].localRotation = Quaternion.Euler(-120f, 0f, 10f);
            root.isStatic = true;
            return root;
        }

        static GameObject BuildMarker(Materials mats)
        {
            var root = new GameObject("TrainingMarker");
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Disc";
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            disc.transform.localScale = new Vector3(1.1f, 0.01f, 1.1f);
            disc.GetComponent<Renderer>().sharedMaterial = mats.Marker;

            // A floating diamond so the marker is easy to spot from across the court.
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beacon.name = "Beacon";
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(root.transform, false);
            beacon.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            beacon.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            beacon.transform.localScale = Vector3.one * 0.22f;
            beacon.GetComponent<Renderer>().sharedMaterial = mats.Marker;
            return root;
        }

        static void BuildTraining(Transform world, Transform player, GameObject ball, GameObject coachPrefab, GameObject markerPrefab)
        {
            // Coach stands on the player's side near the net, facing the player.
            var coach = Instantiate(coachPrefab, world, new Vector3(2.6f, 0f, -0.8f), Quaternion.Euler(0f, 180f, 0f));

            var training = new GameObject("Training");
            training.transform.SetParent(world, false);
            var story = training.AddComponent<TrainingStory>();
            var session = training.AddComponent<TrainingSession>();
            var so = new SerializedObject(session);
            so.FindProperty("skills").objectReferenceValue = player.GetComponent<PlayerSkills>();
            so.FindProperty("contact").objectReferenceValue = player.GetComponent<BallContact>();
            so.FindProperty("serve").objectReferenceValue = player.GetComponent<PlayerServe>();
            so.FindProperty("story").objectReferenceValue = story;
            so.FindProperty("ball").objectReferenceValue = ball.GetComponent<VolleyBall>();
            so.FindProperty("coach").objectReferenceValue = coach.transform;
            so.FindProperty("markerPrefab").objectReferenceValue = markerPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            var toasts = training.AddComponent<SkillToasts>();
            var toastSo = new SerializedObject(toasts);
            toastSo.FindProperty("skills").objectReferenceValue = player.GetComponent<PlayerSkills>();
            toastSo.ApplyModifiedPropertiesWithoutUndo();

            var serveSo = new SerializedObject(player.GetComponent<PlayerServe>());
            serveSo.FindProperty("ball").objectReferenceValue = ball.GetComponent<VolleyBall>();
            serveSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Wooden notice board at the court edge announcing the city beginner training.</summary>
        static GameObject BuildSign(Materials mats)
        {
            var root = new GameObject("TrainingSign");
            foreach (float s in new[] { -1f, 1f })
                Box("Leg", root.transform, new Vector3(s * 0.75f, 0.75f, 0.07f), new Vector3(0.08f, 1.5f, 0.08f), mats.Wood);
            Box("Board", root.transform, new Vector3(0f, 1.45f, 0f), new Vector3(1.8f, 0.9f, 0.06f), mats.Wood);
            // The cube's -Z face shows its texture rotated 180°, so turn the poster to read upright.
            var poster = Box("Poster", root.transform, new Vector3(0f, 1.45f, -0.035f), new Vector3(1.64f, 0.82f, 0.01f), mats.Poster);
            poster.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.size = new Vector3(1.8f, 2f, 0.2f);
            root.isStatic = true;
            return root;
        }

        /// <summary>
        /// Renders the poster text once into a PNG with a throwaway camera, so the sign is an
        /// ordinary lit texture (legacy 3D text would draw through other objects).
        /// </summary>
        static Texture2D BakeSignTexture()
        {
            const int width = 1024, height = 512;
            const string path = TextureDir + "/SignPoster.png";
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var rig = new GameObject("SignBake");
            rig.transform.position = new Vector3(0f, -1000f, 0f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.SetParent(rig.transform, false);
            cam.transform.localPosition = new Vector3(0f, 0f, -5f);
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.aspect = 2f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.98f, 0.95f, 0.86f);
            cam.enabled = false;

            void Text(string s, float y, float size, Color color, FontStyle style)
            {
                var go = new GameObject("Text");
                go.transform.SetParent(rig.transform, false);
                go.transform.localPosition = new Vector3(0f, y, 0f);
                var tm = go.AddComponent<TextMesh>();
                tm.text = s;
                tm.font = font;
                tm.fontSize = 96;
                tm.fontStyle = style;
                tm.characterSize = size;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = color;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }

            Text("CITY BEACH VOLLEYBALL", 0.3f, 0.013f, new Color(0.85f, 0.3f, 0.05f), FontStyle.Bold);
            Text("BEGINNER TRAINING", 0.07f, 0.017f, new Color(0.05f, 0.2f, 0.55f), FontStyle.Bold);
            Text("All levels welcome - just show up!", -0.18f, 0.0095f, new Color(0.15f, 0.13f, 0.12f), FontStyle.Italic);
            Text("Tue & Thu  -  Court 3", -0.34f, 0.0085f, new Color(0.15f, 0.13f, 0.12f), FontStyle.Normal);

            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(rig);

            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- Ball ----------

        static GameObject BuildBall(Materials mats, PhysicsMaterials phys)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Volleyball";
            root.transform.localScale = Vector3.one * (VolleyBall.Radius * 2f);
            root.GetComponent<Renderer>().sharedMaterial = mats.Ball;
            root.GetComponent<Collider>().sharedMaterial = phys.Ball;

            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.27f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.useGravity = false;
            root.AddComponent<VolleyBall>();
            root.AddComponent<BallTrajectoryPreview>();
            root.AddComponent<BallDeadMarker>();
            return root;
        }

        internal static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        // ---------- Scene ----------

        internal static Material BeachSky()
        {
            const string path = MaterialDir + "/BeachSky.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_SkyTint", new Color(0.42f, 0.62f, 0.9f));
            mat.SetColor("_GroundColor", new Color(0.78f, 0.72f, 0.6f));
            mat.SetFloat("_AtmosphereThickness", 0.75f);
            mat.SetFloat("_Exposure", 1.35f);
            mat.SetFloat("_SunSize", 0.035f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Scenery (visual only, no colliders) ----------

        static void BuildScenery(Transform world, Materials mats)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(world, false);
            float edge = SandSize * 0.5f;

            // Beach continues past the playable patch, then wet sand, shallows and open sea to the north (+Z).
            Box("OuterSand", root, new Vector3(0f, -0.07f, 0f), new Vector3(600f, 0.1f, 600f), mats.SandFar);
            Box("WetSand", root, new Vector3(0f, -0.015f, edge + 7f), new Vector3(600f, 0.02f, 6f), mats.WetSand);
            Box("Foam", root, new Vector3(0f, -0.008f, edge + 10.2f), new Vector3(600f, 0.02f, 0.6f), mats.Foam);
            Box("Shallows", root, new Vector3(0f, -0.01f, edge + 14f), new Vector3(600f, 0.02f, 7f), mats.Shallows);
            Box("Ocean", root, new Vector3(0f, -0.012f, edge + 17.5f + 150f), new Vector3(800f, 0.02f, 300f), mats.Ocean);

            // Low dunes behind the court (south) and along both sides, half-buried so they read as hills.
            var dunes = new (Vector3 pos, Vector3 size, float yaw)[]
            {
                (new Vector3(-30f, -2.2f, -62f), new Vector3(46f, 9f, 22f), 8f),
                (new Vector3(14f, -2.6f, -70f), new Vector3(60f, 11f, 26f), -6f),
                (new Vector3(52f, -2f, -48f), new Vector3(34f, 8f, 20f), 35f),
                (new Vector3(-64f, -2.4f, -30f), new Vector3(40f, 9f, 24f), -40f),
                (new Vector3(70f, -2.4f, -6f), new Vector3(36f, 7f, 22f), 80f),
                (new Vector3(-72f, -2.6f, 8f), new Vector3(30f, 7f, 20f), 100f),
            };
            for (int i = 0; i < dunes.Length; i++)
            {
                var d = Part(PrimitiveType.Sphere, "Dune" + i, root, dunes[i].pos, dunes[i].size, mats.Dune);
                d.localRotation = Quaternion.Euler(0f, dunes[i].yaw, 0f);
                d.gameObject.isStatic = true;
            }

            // A few tufts of dune grass on the crests.
            var rng = new System.Random(7);
            for (int i = 0; i < 40; i++)
            {
                var dune = dunes[rng.Next(dunes.Length)];
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = (float)rng.NextDouble() * 0.35f;
                var local = new Vector3(Mathf.Cos(a) * r * dune.size.x, 0f, Mathf.Sin(a) * r * dune.size.z);
                local = Quaternion.Euler(0f, dune.yaw, 0f) * local;
                float h = dune.pos.y + dune.size.y * 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - 4f * r * r));
                var tuft = Part(PrimitiveType.Capsule, "Grass" + i, root, dune.pos + local + Vector3.up * (h - dune.pos.y),
                    new Vector3(0.6f, 0.5f + (float)rng.NextDouble() * 0.5f, 0.6f), mats.DuneGrass);
                tuft.gameObject.isStatic = true;
            }
        }

        static void ConfigureLight(Transform world)
        {
            var light = Object.FindAnyObjectByType<Light>();
            if (light == null)
            {
                light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
            }
            light.name = "Sun";
            light.transform.SetParent(world, false);
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            light.color = new Color(1f, 0.95f, 0.84f);
            light.intensity = 1.6f;
            light.shadowStrength = 0.75f;

            // Bright beach sky, sky-lit ambient, and a light sea haze toward the horizon.
            RenderSettings.skybox = BeachSky();
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.15f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.87f, 0.95f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 420f;
            DynamicGI.UpdateEnvironment();
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;

            var volume = GameObject.Find("Global Volume");
            if (volume != null) volume.transform.SetParent(world, false);
        }

        static void ConfigureCamera(Transform player)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            var follow = cam.GetComponent<ThirdPersonFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<ThirdPersonFollow>();
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = player;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Put the camera where it will start in Play Mode so the Scene/Game view matches.
            cam.transform.position = player.position + new Vector3(0f, 1.5f + 5f * Mathf.Sin(18f * Mathf.Deg2Rad), -5f * Mathf.Cos(18f * Mathf.Deg2Rad));
            cam.transform.LookAt(player.position + Vector3.up * 1.5f);
        }

        // ---------- Helpers ----------

        internal static GameObject Instantiate(GameObject prefab, Transform parent, Vector3 pos, Quaternion rot)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            return go;
        }

        static GameObject SavePrefab(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        internal static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        // The ball multiplies its bounce/friction with whatever it touches (Multiply outranks Average),
        // so each surface's own values decide how the ball behaves on it.
        class PhysicsMaterials
        {
            public readonly PhysicsMaterial Ball = Make("Ball", 1f, 1f, PhysicsMaterialCombine.Multiply);
            public readonly PhysicsMaterial Sand = Make("Sand", 0.25f, 0.8f);
            public readonly PhysicsMaterial Net = Make("Net", 0.1f, 1f);
            public readonly PhysicsMaterial Post = Make("Post", 0.6f, 0.3f);
            public readonly PhysicsMaterial Player = Make("Player", 0.35f, 0.5f);

            static PhysicsMaterial Make(string name, float bounciness, float friction, PhysicsMaterialCombine combine = PhysicsMaterialCombine.Average)
            {
                string path = $"{PhysicsDir}/{name}.physicMaterial";
                var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
                if (mat == null)
                {
                    mat = new PhysicsMaterial(name);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.bounciness = bounciness;
                mat.dynamicFriction = friction;
                mat.staticFriction = friction;
                mat.bounceCombine = combine;
                mat.frictionCombine = combine;
                EditorUtility.SetDirty(mat);
                return mat;
            }
        }

        internal class Materials
        {
            static readonly Color TankColor = new Color(0.97f, 0.27f, 0.18f);

            public readonly Material Sand = Textured(Lit("Sand", Color.white, 0.02f), SandTexture(), SandSize / 2f);
            public readonly Material SandFar = Textured(Lit("SandFar", Color.white, 0.02f), SandTexture(), 300f);
            public readonly Material WetSand = Lit("WetSand", new Color(0.72f, 0.62f, 0.46f), 0.55f);
            public readonly Material Foam = Lit("Foam", new Color(0.96f, 0.98f, 1f), 0.4f);
            public readonly Material Shallows = Lit("Shallows", new Color(0.22f, 0.72f, 0.74f), 0.9f);
            public readonly Material Ocean = Lit("Ocean", new Color(0.06f, 0.4f, 0.6f), 0.93f);
            public readonly Material Dune = Lit("Dune", new Color(0.9f, 0.8f, 0.6f), 0.02f);
            public readonly Material DuneGrass = Lit("DuneGrass", new Color(0.45f, 0.55f, 0.25f), 0.1f);
            public readonly Material Line = Lit("CourtLine", new Color(0.98f, 0.96f, 0.9f), 0.2f);
            public readonly Material Post = Lit("NetPost", new Color(0.75f, 0.77f, 0.8f), 0.6f, 0.8f);
            public readonly Material Tape = Lit("NetTape", new Color(0.97f, 0.97f, 0.95f), 0.3f);
            public readonly Material TapeBlue = Lit("NetTapeBlue", new Color(0.08f, 0.28f, 0.72f), 0.3f);
            public readonly Material AntennaRed = Lit("AntennaRed", new Color(0.88f, 0.1f, 0.1f), 0.4f);
            public readonly Material Rope = Lit("NetRope", new Color(0.15f, 0.15f, 0.15f), 0.2f);
            public readonly Material Net = NetMaterial();
            public readonly Material Skin = Lit("PlayerSkin", new Color(0.86f, 0.64f, 0.48f), 0.35f);
            public readonly Material Hair = Lit("PlayerHair", new Color(0.2f, 0.12f, 0.07f), 0.3f);
            public readonly Material Tank = Lit("PlayerTank", TankColor, 0.25f);
            public readonly Material Number = Textured(Lit("PlayerNumber", Color.white, 0.25f), NumberTexture(), 1f);
            public readonly Material Shorts = Lit("PlayerShorts", new Color(0.08f, 0.33f, 0.9f), 0.25f);
            public readonly Material Ball = BallMaterial();
            public readonly Material CoachShirt = Lit("CoachShirt", new Color(0.15f, 0.55f, 0.35f), 0.25f);
            public readonly Material CoachShorts = Lit("CoachShorts", new Color(0.2f, 0.2f, 0.22f), 0.25f);
            public readonly Material Marker = MarkerMaterial();
            public readonly Material Machine = Lit("BallMachine", new Color(0.12f, 0.45f, 0.62f), 0.45f);
            public readonly Material MachineDark = Lit("BallMachineDark", new Color(0.12f, 0.12f, 0.14f), 0.3f, 0.4f);
            public readonly Material Wood = Lit("SignWood", new Color(0.55f, 0.38f, 0.22f), 0.15f);
            public readonly Material Poster = Textured(Lit("SignPoster", Color.white, 0.1f), BakeSignTexture(), 1f);

            static Material Textured(Material mat, Texture2D tex, float tiling)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", Vector2.one * tiling);
                EditorUtility.SetDirty(mat);
                return mat;
            }

            static Texture2D SaveTexture(Texture2D tex, string name, bool repeat)
            {
                string path = $"{TextureDir}/{name}.png";
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            /// <summary>Warm sand with soft tileable dunes of tone plus fine grain and a few darker/lighter specks.</summary>
            static Texture2D SandTexture()
            {
                const string name = "SandGrain";
                var existing = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{name}.png");
                if (existing != null) return existing;

                const int size = 512;
                var baseColor = new Color(0.95f, 0.85f, 0.65f);
                var rng = new System.Random(11);
                var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float tone = TileableNoise(x, y, size, 0.012f) * 0.6f + TileableNoise(x, y, size, 0.05f) * 0.4f;
                    float grain = (float)rng.NextDouble() - 0.5f;
                    float k = 0.93f + (tone - 0.5f) * 0.12f + grain * 0.07f;
                    double speck = rng.NextDouble();
                    if (speck < 0.012) k *= 0.72f;
                    else if (speck > 0.992) k *= 1.08f;
                    tex.SetPixel(x, y, new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k));
                }
                tex.Apply();
                return SaveTexture(tex, name, true);
            }

            // Perlin noise blended across the edges so the texture repeats without seams.
            static float TileableNoise(int x, int y, int size, float freq)
            {
                float fx = x / (float)size, fy = y / (float)size;
                float n00 = Mathf.PerlinNoise(x * freq, y * freq);
                float n10 = Mathf.PerlinNoise((x - size) * freq, y * freq);
                float n01 = Mathf.PerlinNoise(x * freq, (y - size) * freq);
                float n11 = Mathf.PerlinNoise((x - size) * freq, (y - size) * freq);
                return Mathf.Lerp(Mathf.Lerp(n00, n10, fx), Mathf.Lerp(n01, n11, fx), fy);
            }

            /// <summary>White "7" on the tank colour for the player's back.</summary>
            static Texture2D NumberTexture()
            {
                const int w = 64, h = 76;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool top = y >= 58 && y <= 68 && x >= 12 && x <= 52;
                    // Diagonal stroke from the top-right down to bottom-centre.
                    float t = Mathf.InverseLerp(68f, 8f, y);
                    float cx = Mathf.Lerp(50f, 26f, t);
                    bool stem = y >= 8 && y <= 68 && Mathf.Abs(x - cx) <= 5.5f;
                    tex.SetPixel(x, y, top || stem ? Color.white : TankColor);
                }
                tex.Apply();
                return SaveTexture(tex, "PlayerNumber7", false);
            }

            static Material MarkerMaterial()
            {
                // Unlit so it reads bright in any light.
                const string path = MaterialDir + "/TrainingMarker.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.SetColor("_BaseColor", new Color(0.2f, 0.95f, 1f));
                EditorUtility.SetDirty(mat);
                return mat;
            }

            static Material BallMaterial()
            {
                var mat = Lit("Volleyball", Color.white, 0.35f);
                mat.SetTexture("_BaseMap", BallTexture());
                EditorUtility.SetDirty(mat);
                return mat;
            }

            // Six curved panels around the sphere's UV seam: yellow / orange / white, with dark seams.
            static Texture2D BallTexture()
            {
                const int width = 256, height = 128, bands = 6;
                var colors = new[] { new Color(1f, 0.82f, 0.08f), new Color(1f, 0.48f, 0.06f), new Color(0.98f, 0.96f, 0.9f) };
                var seam = new Color(0.25f, 0.25f, 0.25f);
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    // Bend the bands with latitude so they read as panels, not stripes.
                    float v = (float)y / height;
                    float u = (float)x / width + 0.06f * Mathf.Sin(v * Mathf.PI * 2f);
                    float band = Mathf.Repeat(u, 1f) * bands;
                    float edge = Mathf.Min(band - Mathf.Floor(band), Mathf.Ceil(band) - band);
                    tex.SetPixel(x, y, edge < 0.04f ? seam : colors[(int)band % colors.Length]);
                }
                const string path = TextureDir + "/VolleyballPanels.png";
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            internal static Material Lit(string name, Color color, float smoothness, float metallic = 0f)
            {
                string path = $"{MaterialDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Metallic", metallic);
                EditorUtility.SetDirty(mat);
                return mat;
            }

            static Material NetMaterial()
            {
                var mat = Lit("Net", Color.white, 0.1f);
                mat.SetTexture("_BaseMap", NetTexture());
                // 10 cm mesh squares over an 8 x 1 m net.
                mat.SetTextureScale("_BaseMap", new Vector2(CourtDimensions.Width / 0.1f, CourtDimensions.NetDepth / 0.1f));
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", (float)CullMode.Off);
                mat.renderQueue = (int)RenderQueue.AlphaTest;
                EditorUtility.SetDirty(mat);
                return mat;
            }

            static Texture2D NetTexture()
            {
                const int size = 32;
                const int thickness = 3;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var cord = new Color(0.08f, 0.08f, 0.08f, 1f);
                var hole = new Color(0f, 0f, 0f, 0f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, x < thickness || y < thickness ? cord : hole);
                System.IO.File.WriteAllBytes(NetTexturePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(NetTexturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(NetTexturePath);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(NetTexturePath);
            }
        }
    }
}
