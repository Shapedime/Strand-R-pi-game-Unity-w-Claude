using System.Collections.Generic;
using BeachVolley.Ball;
using BeachVolley.CameraRig;
using BeachVolley.Menu;
using BeachVolley.Player;
using BeachVolley.Skills;
using BeachVolley.Story;
using BeachVolley.Training;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace BeachVolley.EditorTools
{
    /// <summary>
    /// Builds the two Chapter 1 scenes from code: StoryHome (bedroom + a far-away "TV broadcast"
    /// court rendered onto the TV) and StoryClub (Coastline Beach Club: training court, a second
    /// court with players warming up, Steve, Dave, Marlon). Run Build Foundation first (prefabs).
    /// Safe to re-run; both scenes are rebuilt from scratch.
    /// </summary>
    public static class StoryBuilder
    {
        public const string HomeScenePath = "Assets/Scenes/StoryHome.unity";
        public const string ClubScenePath = "Assets/Scenes/StoryClub.unity";
        const string PrefabDir = "Assets/Prefabs";
        const string StoryDir = "Assets/Story";
        const string TvFeedPath = StoryDir + "/TvFeed.renderTexture";

        const string HandheldNoisePath = "Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset";
        static readonly Vector3 TravelOrigin = new Vector3(-500f, 0f, 0f);
        static readonly Vector3 BroadcastOrigin = new Vector3(500f, 0f, 0f);

        [MenuItem("Beach Volleyball/Build Story Chapter 1")]
        public static void BuildAll()
        {
            CharacterBuilder.BuildAll();
            BuildScenes();
        }

        /// <summary>Just the two scenes, from the character prefabs already built (much faster).</summary>
        [MenuItem("Beach Volleyball/Build Story Scenes Only")]
        public static void BuildScenes()
        {
            FoundationBuilder.EnsureFolder(StoryDir);
            BuildHome();
            BuildClub();
            MenuBuilder.ApplyBuildScenes();
            Debug.Log("[StoryBuilder] Built " + HomeScenePath + " and " + ClubScenePath);
        }

        // ---------- Shared ----------

        class StoryMats
        {
            public readonly FoundationBuilder.Materials Base = new FoundationBuilder.Materials();
            public readonly Material Floor = FoundationBuilder.Materials.Lit("RoomFloor", new Color(0.55f, 0.38f, 0.24f), 0.35f);
            public readonly Material Wall = FoundationBuilder.Materials.Lit("RoomWall", new Color(0.72f, 0.8f, 0.86f), 0.1f);
            public readonly Material Ceiling = FoundationBuilder.Materials.Lit("RoomCeiling", new Color(0.93f, 0.92f, 0.9f), 0.05f);
            public readonly Material Furniture = FoundationBuilder.Materials.Lit("Furniture", new Color(0.82f, 0.74f, 0.62f), 0.3f);
            public readonly Material Blanket = FoundationBuilder.Materials.Lit("Blanket", new Color(0.2f, 0.36f, 0.62f), 0.1f);
            public readonly Material Pillow = FoundationBuilder.Materials.Lit("Pillow", new Color(0.95f, 0.95f, 0.93f), 0.1f);
            public readonly Material Dark = FoundationBuilder.Materials.Lit("DarkPlastic", new Color(0.06f, 0.06f, 0.07f), 0.6f);
            public readonly Material Bag = FoundationBuilder.Materials.Lit("SportsBag", new Color(0.1f, 0.18f, 0.45f), 0.25f);
            public readonly Material Bottle = FoundationBuilder.Materials.Lit("WaterBottle", new Color(0.3f, 0.8f, 0.95f), 0.7f);
            public readonly Material Door = FoundationBuilder.Materials.Lit("Door", new Color(0.62f, 0.45f, 0.3f), 0.3f);
            public readonly Material Poster = FoundationBuilder.Materials.Lit("RoomPoster", new Color(0.98f, 0.62f, 0.2f), 0.2f);
            public readonly Material Car = FoundationBuilder.Materials.Lit("CarPaint", new Color(0.7f, 0.08f, 0.1f), 0.8f, 0.4f);
            public readonly Material Glass = FoundationBuilder.Materials.Lit("CarGlass", new Color(0.15f, 0.2f, 0.25f), 0.95f);
            public readonly Material Metal = FoundationBuilder.Materials.Lit("BikeMetal", new Color(0.2f, 0.5f, 0.8f), 0.7f, 0.6f);
            public readonly Material[] Crowd =
            {
                FoundationBuilder.Materials.Lit("Crowd0", new Color(0.9f, 0.3f, 0.25f), 0.2f),
                FoundationBuilder.Materials.Lit("Crowd1", new Color(0.95f, 0.85f, 0.3f), 0.2f),
                FoundationBuilder.Materials.Lit("Crowd2", new Color(0.25f, 0.5f, 0.9f), 0.2f),
                FoundationBuilder.Materials.Lit("Crowd3", new Color(0.95f, 0.95f, 0.95f), 0.2f),
                FoundationBuilder.Materials.Lit("Crowd4", new Color(0.3f, 0.75f, 0.4f), 0.2f),
            };
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider = false) =>
            FoundationBuilder.Box(name, parent, pos, size, mat, collider);

        static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat) =>
            FoundationBuilder.Part(type, name, parent, pos, scale, mat);

        static T Wire<T>(T component, params (string field, Object value)[] refs) where T : Object
        {
            var so = new SerializedObject(component);
            foreach (var (field, value) in refs)
            {
                var prop = so.FindProperty(field);
                if (prop == null) { Debug.LogError($"[StoryBuilder] {typeof(T).Name}.{field} not found"); continue; }
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        static void WireArray(Object component, string field, Object[] values)
        {
            var so = new SerializedObject(component);
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A cast member's prefab (rigged model or placeholder, see CharacterBuilder).</summary>
        static StoryActor Cast(string name, Transform parent, Vector3 pos, float yaw) =>
            CharacterBuilder.Spawn(name, parent, pos, yaw);

        static CameraShot Shot(string name, Transform parent, Vector3 pos, Vector3 lookAt, float fov)
        {
            var go = new GameObject("Shot_" + name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(lookAt - pos, Vector3.up));
            var shot = go.AddComponent<CameraShot>();
            shot.fieldOfView = fov;
            return shot;
        }

        static GameObject PropBall(Transform parent, StoryMats m)
        {
            var b = Part(PrimitiveType.Sphere, "PropBall", parent, new Vector3(0f, 1f, 0f), Vector3.one * (VolleyBall.Radius * 2f), m.Base.Ball);
            return b.gameObject;
        }

        /// <summary>Court lines + net without the playable sand, walls or soft bounds.</summary>
        static Transform DecorCourt(string name, Transform parent, Vector3 pos, StoryMats m, bool withSand)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            var court = FoundationBuilder.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Court.prefab"), root, Vector3.zero, Quaternion.identity);
            PrefabUtility.UnpackPrefabInstance(court, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(court.GetComponent<BeachVolley.Court.SoftBounds>());
            foreach (var n in new[] { "Sand", "SandBounds" })
            {
                var t = court.transform.Find(n);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            if (withSand) Box("Sand", root, new Vector3(0f, -0.1f, 0f), new Vector3(60f, 0.2f, 60f), m.Base.Sand);
            var net = FoundationBuilder.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Net.prefab"), root, Vector3.zero, Quaternion.identity);
            PrefabUtility.UnpackPrefabInstance(net, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var c in net.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            return root;
        }

        /// <summary>Two players a side flying a prop ball around (see ScriptedPlay).</summary>
        static ScriptedPlay Players(Transform court, Transform[] near, StoryMats m, string far1, string far2, float tempo)
        {
            var far = new[]
            {
                Cast(far1, court, new Vector3(-1.6f, 0f, 5f), 180f).transform,
                Cast(far2, court, new Vector3(1.8f, 0f, 4.2f), 180f).transform,
            };
            var play = court.gameObject.AddComponent<ScriptedPlay>();
            var ball = PropBall(court, m);
            Wire(play, ("ball", ball.transform));
            WireArray(play, "nearTeam", near);
            WireArray(play, "farTeam", far);
            var so = new SerializedObject(play);
            so.FindProperty("tempo").floatValue = tempo;
            so.ApplyModifiedPropertiesWithoutUndo();
            return play;
        }

        static StoryUI StoryUi(Transform parent)
        {
            var panel = MenuBuilder.MenuPanelSettings();
            var go = new GameObject("StoryUI");
            go.transform.SetParent(parent, false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.sortingOrder = 5;
            var ui = go.AddComponent<StoryUI>();

            var pauseGo = new GameObject("PauseMenu");
            pauseGo.transform.SetParent(parent, false);
            var pauseDoc = pauseGo.AddComponent<UIDocument>();
            pauseDoc.panelSettings = panel;
            pauseDoc.sortingOrder = 10;
            pauseGo.AddComponent<PauseMenu>();
            return ui;
        }

        static GameObject Player(Transform parent, Vector3 pos, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/PlayerRookie.prefab");
            var player = FoundationBuilder.Instantiate(prefab, parent, pos, Quaternion.Euler(0f, yaw, 0f));
            // The protagonist acts in cutscenes with the same controller as the cast.
            var actor = player.AddComponent<StoryActor>();
            actor.displayName = "You";
            var so = new SerializedObject(actor);
            so.FindProperty("animator").objectReferenceValue = player.GetComponentInChildren<Animator>();
            so.FindProperty("actingController").objectReferenceValue = CharacterBuilder.Controller;
            so.FindProperty("externallyMoved").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return player;
        }

        static Camera MainCamera(Transform parent, bool follow, Transform player)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            go.AddComponent<AudioListener>();
            if (follow) Wire(go.AddComponent<ThirdPersonFollow>(), ("target", player));
            go.AddComponent<Unity.Cinemachine.CinemachineBrain>();
            Wire(go.AddComponent<CinematicCamera>(),
                ("handheld", AssetDatabase.LoadAssetAtPath<Unity.Cinemachine.NoiseSettings>(HandheldNoisePath)));
            return cam;
        }

        static void ClearScene(out UnityEngine.SceneManagement.Scene scene, string path)
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
        }

        // ---------- StoryHome ----------

        static void BuildHome()
        {
            ClearScene(out var scene, HomeScenePath);
            var m = new StoryMats();
            var world = new GameObject("World").transform;

            // Evening light: low orange sun through the window, warm lamps inside.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(world, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(14f, 200f, 0f);
            sun.color = new Color(1f, 0.72f, 0.45f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.skybox = FoundationBuilder.BeachSky();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.55f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.4f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.2f, 0.18f);
            RenderSettings.fog = false;

            var room = BuildRoom(world, m, out var parts);
            var broadcast = BuildBroadcast(world, m, out var tvShots);
            BuildTravel(world, m, out var travelCar, out var travelBike, out var rider);

            // TV screen shows the broadcast camera's feed.
            var feed = AssetDatabase.LoadAssetAtPath<RenderTexture>(TvFeedPath);
            if (feed == null)
            {
                feed = new RenderTexture(960, 540, 24) { name = "TvFeed" };
                AssetDatabase.CreateAsset(feed, TvFeedPath);
            }
            var screenMat = UnlitMat("TvScreen", feed);
            parts["Screen"].GetComponent<Renderer>().sharedMaterial = screenMat;
            var feedCam = new GameObject("BroadcastCamera").AddComponent<Camera>();
            feedCam.transform.SetParent(broadcast, false);
            feedCam.transform.SetPositionAndRotation(tvShots[0].transform.position, tvShots[0].transform.rotation);
            feedCam.fieldOfView = tvShots[0].fieldOfView;
            feedCam.targetTexture = feed;

            var player = Player(world, new Vector3(-0.8f, 0f, 0.4f), 90f);
            DisableGameplay(player);
            var cam = MainCamera(world, false, player.transform);
            cam.transform.SetPositionAndRotation(parts["ShotRoom"].position, parts["ShotRoom"].rotation);

            var ui = StoryUi(world);
            var director = new GameObject("Chapter1Home").AddComponent<Chapter1Home>();
            director.transform.SetParent(world, false);
            Wire(director,
                ("ui", ui), ("cam", cam.GetComponent<CinematicCamera>()), ("player", player.transform),
                ("tvMatch", broadcast.GetComponent<ScriptedPlay>()), ("tvScreen", parts["Screen"].gameObject),
                ("bedSpot", parts["BedSpot"]), ("bedSeat", parts["BedSeat"]), ("doorSpot", parts["DoorSpot"]),
                ("bagOnFloor", parts["Bag"].gameObject), ("bagByDoor", parts["BagByDoor"].gameObject),
                ("bag", parts["Bag"].GetComponent<Interactable>()), ("bottle", parts["Bottle"].GetComponent<Interactable>()),
                ("phone", parts["Phone"].GetComponent<Interactable>()), ("tv", parts["TvSpot"].GetComponent<Interactable>()),
                ("door", parts["Door"].GetComponent<Interactable>()), ("ceilingLight", parts["CeilingLight"].GetComponent<Light>()),
                ("travelCar", travelCar.transform), ("travelBike", travelBike.transform), ("rider", rider));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Material UnlitMat(string name, Texture tex)
        {
            string path = $"Assets/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The player's PlayerRookie, minus volleyball: no ball, no hit HUD in the bedroom.</summary>
        static void DisableGameplay(GameObject player)
        {
            foreach (var b in new Behaviour[] { player.GetComponent<BallContact>(), player.GetComponent<PlayerServe>(), player.GetComponent<SkillDebugHud>() })
                if (b != null) b.enabled = false;
        }

        static Interactable Interact(Transform t, string id, string prompt, float radius = 1.3f, GameObject hide = null)
        {
            var it = t.gameObject.AddComponent<Interactable>();
            it.id = id;
            it.prompt = prompt;
            it.radius = radius;
            it.hideOnUse = hide;
            return it;
        }

        /// <summary>5 × 4 m bedroom, floor at y = 0, centred on the origin. TV on the east wall, bed west, door south, window north.</summary>
        static Transform BuildRoom(Transform world, StoryMats m, out Dictionary<string, Transform> parts)
        {
            parts = new Dictionary<string, Transform>();
            var room = new GameObject("Bedroom").transform;
            room.SetParent(world, false);
            const float hx = 2.5f, hz = 2f, h = 2.7f, t = 0.2f;

            Box("Floor", room, new Vector3(0f, -0.05f, 0f), new Vector3(hx * 2 + t * 2, 0.1f, hz * 2 + t * 2), m.Floor, true);
            Box("Ceiling", room, new Vector3(0f, h + 0.05f, 0f), new Vector3(hx * 2 + t * 2, 0.1f, hz * 2 + t * 2), m.Ceiling);
            Box("Wall_W", room, new Vector3(-hx - t / 2, h / 2, 0f), new Vector3(t, h, hz * 2), m.Wall, true);
            Box("Wall_E", room, new Vector3(hx + t / 2, h / 2, 0f), new Vector3(t, h, hz * 2), m.Wall, true);
            // North wall with a window (x −1.5..0.3, y 1.0..2.2).
            Box("Wall_N_L", room, new Vector3((-hx - 1.5f) / 2, h / 2, hz + t / 2), new Vector3(hx - 1.5f + t, h, t), m.Wall, true);
            Box("Wall_N_R", room, new Vector3((0.3f + hx) / 2 + t / 4, h / 2, hz + t / 2), new Vector3(hx - 0.3f + t / 2, h, t), m.Wall, true);
            Box("Wall_N_Low", room, new Vector3(-0.6f, 0.5f, hz + t / 2), new Vector3(1.8f, 1f, t), m.Wall, true);
            Box("Wall_N_High", room, new Vector3(-0.6f, (2.2f + h) / 2, hz + t / 2), new Vector3(1.8f, h - 2.2f, t), m.Wall, true);
            Box("WindowSill", room, new Vector3(-0.6f, 1f, hz - 0.02f), new Vector3(1.9f, 0.05f, 0.2f), m.Ceiling);
            Box("WindowBar", room, new Vector3(-0.6f, 1.6f, hz + t / 2), new Vector3(0.05f, 1.2f, 0.05f), m.Ceiling);
            // South wall with a door (x 1.1..2.1, up to 2.1 m).
            Box("Wall_S_L", room, new Vector3((-hx - t + 1.1f) / 2, h / 2, -hz - t / 2), new Vector3(hx + t + 1.1f, h, t), m.Wall, true);
            Box("Wall_S_R", room, new Vector3((2.1f + hx + t) / 2, h / 2, -hz - t / 2), new Vector3(hx + t - 2.1f, h, t), m.Wall, true);
            Box("Wall_S_Top", room, new Vector3(1.6f, (2.1f + h) / 2, -hz - t / 2), new Vector3(1f, h - 2.1f, t), m.Wall, true);
            var door = Box("Door", room, new Vector3(1.6f, 1.05f, -hz - t / 2), new Vector3(0.95f, 2.1f, 0.06f), m.Door, true);
            Part(PrimitiveType.Sphere, "Handle", door.transform, new Vector3(-0.38f, 0f, 0.7f), new Vector3(0.06f, 0.03f, 0.8f), m.Base.Post);
            var doorSpot = new GameObject("DoorUse").transform;
            doorSpot.SetParent(room, false);
            doorSpot.localPosition = new Vector3(1.6f, 0f, -1.75f);
            parts["Door"] = Interact(doorSpot, "door", "Head out to training", 1.1f).transform;
            var enter = new GameObject("DoorSpot").transform;
            enter.SetParent(room, false);
            enter.SetLocalPositionAndRotation(new Vector3(1.6f, 0f, -1.3f), Quaternion.identity);
            parts["DoorSpot"] = enter;

            // Bed along the west wall.
            Box("BedFrame", room, new Vector3(-1.75f, 0.2f, 0.75f), new Vector3(1.4f, 0.4f, 2.1f), m.Furniture, true);
            Box("Mattress", room, new Vector3(-1.75f, 0.47f, 0.75f), new Vector3(1.3f, 0.16f, 2f), m.Pillow);
            Box("Blanket", room, new Vector3(-1.72f, 0.56f, 0.55f), new Vector3(1.34f, 0.04f, 1.5f), m.Blanket);
            Box("Pillow", room, new Vector3(-1.75f, 0.6f, 1.5f), new Vector3(0.7f, 0.12f, 0.38f), m.Pillow);
            Box("Headboard", room, new Vector3(-1.75f, 0.65f, 1.82f), new Vector3(1.4f, 0.9f, 0.08f), m.Furniture);
            var bedSpot = new GameObject("BedSpot").transform;
            bedSpot.SetParent(room, false);
            bedSpot.SetLocalPositionAndRotation(new Vector3(-0.82f, 0f, 0.35f), Quaternion.Euler(0f, 90f, 0f));
            parts["BedSpot"] = bedSpot;
            // Hips on the mattress edge (0.55 m high, edge at x = -1.05), facing the TV.
            var bedSeat = new GameObject("BedSeat").transform;
            bedSeat.SetParent(room, false);
            bedSeat.SetLocalPositionAndRotation(new Vector3(-1.3f, 0.1f, 0.35f), Quaternion.Euler(0f, 90f, 0f));
            parts["BedSeat"] = bedSeat;
            var phone = Box("Phone", room, new Vector3(-1.55f, 0.59f, -0.15f), new Vector3(0.08f, 0.012f, 0.16f), m.Dark);
            parts["Phone"] = Interact(phone.transform, "phone", "Check the training confirmation", 1.2f).transform;

            // TV on the east wall, on a low cabinet.
            Box("TvCabinet", room, new Vector3(2.2f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 1.8f), m.Furniture, true);
            Box("TvBody", room, new Vector3(2.38f, 1.35f, 0f), new Vector3(0.08f, 0.82f, 1.42f), m.Dark);
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Screen";
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.transform.SetParent(room, false);
            screen.transform.SetLocalPositionAndRotation(new Vector3(2.335f, 1.35f, 0f), Quaternion.Euler(0f, 90f, 0f));
            screen.transform.localScale = new Vector3(1.32f, 0.74f, 1f);
            parts["Screen"] = screen.transform;
            var tvSpot = new GameObject("TvUse").transform;
            tvSpot.SetParent(room, false);
            tvSpot.localPosition = new Vector3(1.4f, 0f, 0f);
            parts["TvSpot"] = Interact(tvSpot, "tv", "Watch the replay", 1f).transform;

            // Desk by the window with the water bottle; sports bag on the floor.
            Box("Desk", room, new Vector3(1.4f, 0.74f, 1.65f), new Vector3(1.3f, 0.05f, 0.62f), m.Furniture);
            foreach (float sx in new[] { -0.6f, 0.6f })
                Box("DeskLeg", room, new Vector3(1.4f + sx, 0.36f, 1.65f), new Vector3(0.05f, 0.72f, 0.55f), m.Furniture, true);
            Box("Chair", room, new Vector3(1.3f, 0.45f, 1.1f), new Vector3(0.45f, 0.06f, 0.45f), m.Dark);
            var bottleRoot = new GameObject("Bottle").transform;
            bottleRoot.SetParent(room, false);
            bottleRoot.localPosition = new Vector3(1.0f, 0.77f, 1.6f);
            var bottleMesh = Part(PrimitiveType.Cylinder, "BottleMesh", bottleRoot, new Vector3(0f, 0.12f, 0f), new Vector3(0.08f, 0.12f, 0.08f), m.Bottle);
            parts["Bottle"] = Interact(bottleRoot, "bottle", "Pick up the water bottle", 1.2f, bottleMesh.gameObject).transform;
            var bag = Box("SportsBag", room, new Vector3(0.2f, 0.16f, 1.5f), new Vector3(0.7f, 0.32f, 0.34f), m.Bag);
            parts["Bag"] = Interact(bag.transform, "bag", "Pack the sports bag", 1.2f).transform;
            parts["BagByDoor"] = Box("SportsBagByDoor", room, new Vector3(0.7f, 0.16f, -1.7f), new Vector3(0.7f, 0.32f, 0.34f), m.Bag).transform;
            Box("Poster", room, new Vector3(-0.2f, 1.6f, -hz + 0.01f), new Vector3(0.7f, 0.95f, 0.01f), m.Poster);
            Box("Rug", room, new Vector3(0.3f, 0.005f, 0.2f), new Vector3(2f, 0.01f, 1.4f), m.Blanket);

            var lamp = new GameObject("CeilingLight").AddComponent<Light>();
            lamp.transform.SetParent(room, false);
            lamp.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.85f, 0.65f);
            lamp.intensity = 2.2f;
            lamp.range = 8f;
            lamp.shadows = LightShadows.Soft;
            parts["CeilingLight"] = lamp.transform;
            Part(PrimitiveType.Sphere, "LampShade", room, new Vector3(0f, 2.6f, 0f), new Vector3(0.4f, 0.15f, 0.4f), m.Pillow).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var tvGlow = new GameObject("TvGlow").AddComponent<Light>();
            tvGlow.transform.SetParent(room, false);
            tvGlow.transform.localPosition = new Vector3(1.9f, 1.3f, 0f);
            tvGlow.type = LightType.Point;
            tvGlow.color = new Color(0.6f, 0.8f, 1f);
            tvGlow.intensity = 0.8f;
            tvGlow.range = 3.5f;

            // Cutscene shots.
            Shot("Room", room, new Vector3(-2.25f, 2.35f, -1.8f), new Vector3(0.6f, 0.7f, 0.6f), 62f);
            parts["ShotRoom"] = room.Find("Shot_Room");
            Shot("TV", room, new Vector3(-1.75f, 1.75f, 1.1f), new Vector3(2.33f, 1.3f, 0f), 38f);
            Shot("Player", room, new Vector3(1.5f, 1.45f, 0.1f), new Vector3(-0.75f, 1.55f, 0.35f), 40f);
            Shot("PullAway", room, new Vector3(-2.3f, 2.5f, -1.85f), new Vector3(0.6f, 1.1f, 0.4f), 70f);
            return room;
        }

        /// <summary>The televised final, far away from the bedroom: court, crowd and four players.</summary>
        static Transform BuildBroadcast(Transform world, StoryMats m, out CameraShot[] shots)
        {
            var root = DecorCourt("BroadcastCourt", world, BroadcastOrigin, m, true);
            var near = new[]
            {
                Cast("TvWinner", root, new Vector3(-1.4f, 0f, -4.5f), 0f).transform,
                Cast("TvPartner", root, new Vector3(1.6f, 0f, -3.8f), 0f).transform,
            };
            Players(root, near, m, "TvFar1", "TvFar2", 1f);
            TvFans(root);

            // Grandstands along both sidelines.
            var rng = new System.Random(3);
            foreach (float side in new[] { -1f, 1f })
            {
                for (int row = 0; row < 4; row++)
                {
                    float x = side * (8f + row * 0.9f);
                    Box($"Stand{side}_{row}", root, new Vector3(x, row * 0.45f + 0.2f, 0f), new Vector3(0.9f, 0.4f, 20f), m.Base.Post);
                    for (int i = 0; i < 22; i++)
                    {
                        if (rng.NextDouble() < 0.15) continue;
                        float z = -9.5f + i * 0.9f + (float)rng.NextDouble() * 0.2f;
                        // Leave room in the front row for the real fans (TvFans).
                        if (row == 0 && System.Array.Exists(FanZ, fz => Mathf.Abs(fz - z) < 0.7f)) continue;
                        Part(PrimitiveType.Capsule, "Fan", root, new Vector3(x, row * 0.45f + 0.75f, z),
                            new Vector3(0.38f, 0.38f, 0.38f), m.Crowd[rng.Next(m.Crowd.Length)]);
                    }
                }
            }
            shots = new[]
            {
                Shot("Broadcast", root, BroadcastOrigin + new Vector3(0f, 7.5f, -17f), BroadcastOrigin + new Vector3(0f, 0.4f, 0.5f), 42f),
                Shot("BroadcastClose", root, BroadcastOrigin + new Vector3(-3.8f, 1.9f, -0.6f), BroadcastOrigin + new Vector3(-1.1f, 1.2f, -2.6f), 42f),
            };
            return root;
        }

        // ---------- StoryClub ----------

        static void BuildClub()
        {
            ClearScene(out var scene, ClubScenePath);
            var world = new GameObject("World").transform;
            FoundationBuilder.BuildBackdrop(world);
            var m = new StoryMats();

            var player = Player(world, new Vector3(0f, 0f, -5f), 0f);
            var ball = FoundationBuilder.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Volleyball.prefab"), world,
                new Vector3(0.9f, VolleyBall.Radius + 0.01f, -4.6f), Quaternion.identity);
            var vb = ball.GetComponent<VolleyBall>();
            var contact = player.GetComponent<BallContact>();
            Wire(contact, ("ball", vb));
            Wire(player.GetComponent<PlayerServe>(), ("ball", vb));
            var cam = MainCamera(world, true, player.transform);
            cam.transform.position = player.transform.position + new Vector3(0f, 3f, -4.8f);
            cam.transform.LookAt(player.transform.position + Vector3.up * 1.5f);

            // People.
            var people = new GameObject("People").transform;
            people.SetParent(world, false);
            var steve = Cast("Steve", people, new Vector3(2.5f, 0f, -6f), 0f);
            var dave = Cast("Dave", people, new Vector3(-1.2f, 0f, -6.5f), 0f);

            // Second court: Marlon and three club players warming up.
            var court2 = DecorCourt("Court2", world, new Vector3(14f, 0f, 0f), m, false);
            var marlon = Cast("Marlon", court2, new Vector3(-1.5f, 0f, -4.5f), 0f);
            var marlonMate = Cast("ClubPlayer", court2, new Vector3(1.4f, 0f, -3.6f), 0f);
            var rally = Players(court2, new[] { marlon.transform, marlonMate.transform }, m, "ClubFar1", "ClubFar2", 1f);

            // A third court further down the beach with a casual rally, for life in the background.
            var court3 = DecorCourt("Court3", world, new Vector3(-15f, 0f, 2f), m, false);
            var c3 = new[]
            {
                Cast("Casual1", court3, new Vector3(-1.4f, 0f, -4f), 0f).transform,
                Cast("Casual2", court3, new Vector3(1.6f, 0f, -3.5f), 0f).transform,
            };
            Players(court3, c3, m, "CasualFar1", "CasualFar2", 0.8f);

            // Entrance gate, parking, bench.
            BuildEntrance(world, m, out var car, out var bike);
            var bench = new GameObject("Bench").transform;
            bench.SetParent(world, false);
            bench.SetLocalPositionAndRotation(new Vector3(-6.2f, 0f, -3.9f), Quaternion.LookRotation(new Vector3(5.6f, 0f, 3.5f)));
            // No collider: the player sits on it in cutscenes and the controller would be pushed off.
            Box("Seat", bench, new Vector3(0f, 0.45f, 0f), new Vector3(2f, 0.08f, 0.45f), m.Base.Wood);
            foreach (float sx in new[] { -0.85f, 0.85f })
                Box("Leg", bench, new Vector3(sx, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.4f), m.Base.Wood);
            Box("Bag", bench, new Vector3(-0.8f, 0.62f, 0f), new Vector3(0.5f, 0.28f, 0.3f), m.Bag);
            Part(PrimitiveType.Cylinder, "Bottle", bench, new Vector3(-1.25f, 0.12f, 0.25f), new Vector3(0.08f, 0.12f, 0.08f), m.Bottle);

            // Drill marker and the story systems.
            var marker = FoundationBuilder.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/TrainingMarker.prefab"), world,
                new Vector3(0f, 0f, -5f), Quaternion.identity);
            marker.SetActive(false);

            var ui = StoryUi(world);
            var story = new GameObject("Story").transform;
            story.SetParent(world, false);
            var drill = Wire(story.gameObject.AddComponent<StoryDrill>(),
                ("ball", vb), ("contact", contact), ("skills", player.GetComponent<PlayerSkills>()),
                ("steve", steve.transform), ("dave", dave.transform), ("marker", marker.transform), ("ui", ui));
            Wire(story.gameObject.AddComponent<SkillToasts>(), ("skills", player.GetComponent<PlayerSkills>()));
            Wire(story.gameObject.AddComponent<Chapter1Club>(),
                ("ui", ui), ("cam", cam.GetComponent<CinematicCamera>()), ("player", player.transform),
                ("steve", steve), ("dave", dave), ("marlon", marlon), ("clubRally", rally), ("drill", drill),
                ("contact", contact), ("car", car), ("bike", bike));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void BuildEntrance(Transform world, StoryMats m, out GameObject car, out GameObject bike)
        {
            var gate = new GameObject("Entrance").transform;
            gate.SetParent(world, false);
            gate.localPosition = new Vector3(-7f, 0f, -16.3f);
            foreach (float sx in new[] { -2.4f, 2.4f })
                Box("Post", gate, new Vector3(sx, 1.6f, 0f), new Vector3(0.22f, 3.2f, 0.22f), m.Base.Wood, true);
            Box("Arch", gate, new Vector3(0f, 3.3f, 0f), new Vector3(5.3f, 0.6f, 0.15f), m.Base.Wood);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (float face in new[] { 1f, -1f })
            {
                var label = new GameObject("Sign");
                label.transform.SetParent(gate, false);
                label.transform.SetLocalPositionAndRotation(new Vector3(0f, 3.3f, face * 0.09f), Quaternion.Euler(0f, face > 0 ? 180f : 0f, 0f));
                var tm = label.AddComponent<TextMesh>();
                tm.text = "COASTLINE BEACH CLUB";
                tm.font = font;
                tm.fontSize = 80;
                tm.characterSize = 0.035f;
                tm.fontStyle = FontStyle.Bold;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = new Color(1f, 0.95f, 0.85f);
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            // Low fence either side of the gate.
            foreach (float sx in new[] { -1f, 1f })
                Box("Fence", gate, new Vector3(sx * 10f, 0.5f, 0f), new Vector3(15f, 1f, 0.1f), m.Base.Wood);

            // His ride: a car or a bike just inside the gate (the director shows the one he chose).
            car = MakeCar(world, new Vector3(-11f, 0f, -8f), 90f, m);
            bike = MakeBike(world, new Vector3(-4.3f, 0f, -13.8f), 20f, m);
        }

        static GameObject MakeCar(Transform parent, Vector3 pos, float yaw, StoryMats m)
        {
            var car = new GameObject("Car");
            car.transform.SetParent(parent, false);
            car.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            Box("Body", car.transform, new Vector3(0f, 0.55f, 0f), new Vector3(1.8f, 0.6f, 4.1f), m.Car, true);
            Box("Cabin", car.transform, new Vector3(0f, 1.1f, -0.2f), new Vector3(1.6f, 0.55f, 2.2f), m.Glass);
            foreach (float sx in new[] { -0.85f, 0.85f })
            foreach (float sz in new[] { -1.3f, 1.3f })
            {
                var wheel = Part(PrimitiveType.Cylinder, "Wheel", car.transform, new Vector3(sx, 0.33f, sz), new Vector3(0.66f, 0.12f, 0.66f), m.Dark);
                wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            return car;
        }

        static GameObject MakeBike(Transform parent, Vector3 pos, float yaw, StoryMats m)
        {
            var bike = new GameObject("Bike");
            bike.transform.SetParent(parent, false);
            bike.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            foreach (float sz in new[] { -0.52f, 0.52f })
            {
                var wheel = Part(PrimitiveType.Cylinder, "Wheel", bike.transform, new Vector3(0f, 0.35f, sz), new Vector3(0.7f, 0.02f, 0.7f), m.Dark);
                wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            var frame = Part(PrimitiveType.Cylinder, "Frame", bike.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.05f, 0.5f, 0.05f), m.Metal);
            frame.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Cylinder, "Seatpost", bike.transform, new Vector3(0f, 0.65f, -0.25f), new Vector3(0.04f, 0.3f, 0.04f), m.Metal);
            Box("Saddle", bike.transform, new Vector3(0f, 0.95f, -0.25f), new Vector3(0.12f, 0.05f, 0.25f), m.Dark);
            Part(PrimitiveType.Cylinder, "Stem", bike.transform, new Vector3(0f, 0.75f, 0.45f), new Vector3(0.04f, 0.4f, 0.04f), m.Metal);
            var bar = Part(PrimitiveType.Cylinder, "Handlebar", bike.transform, new Vector3(0f, 1.1f, 0.45f), new Vector3(0.04f, 0.28f, 0.04f), m.Dark);
            bar.localRotation = Quaternion.Euler(0f, 0f, 90f);
            return bike;
        }

        /// <summary>
        /// The ride to the club, far from the bedroom: a coast road along the sea with palms. The car
        /// (or the bike with the protagonist on it) drives along +X; the director moves it.
        /// </summary>
        static Transform BuildTravel(Transform world, StoryMats m, out GameObject car, out GameObject bike, out StoryActor rider)
        {
            var root = new GameObject("CoastRoad").transform;
            root.SetParent(world, false);
            root.localPosition = TravelOrigin;
            Box("Sand", root, new Vector3(0f, -0.1f, 0f), new Vector3(260f, 0.2f, 60f), m.Base.SandFar);
            Box("Road", root, new Vector3(0f, 0.005f, 0f), new Vector3(260f, 0.02f, 6f), m.Dark);
            for (int i = -26; i <= 26; i++)
                Box("Stripe", root, new Vector3(i * 5f, 0.02f, 0f), new Vector3(2.2f, 0.01f, 0.15f), m.Base.Line);
            Box("Shallows", root, new Vector3(0f, -0.01f, 22f), new Vector3(400f, 0.02f, 10f), m.Base.Shallows);
            Box("Sea", root, new Vector3(0f, -0.012f, 160f), new Vector3(600f, 0.02f, 270f), m.Base.Ocean);
            var rng = new System.Random(5);
            for (int i = 0; i < 26; i++)
            {
                float x = -120f + i * 9.5f + (float)rng.NextDouble() * 3f;
                float z = (i % 2 == 0 ? 6f : -7f) + (float)rng.NextDouble() * 2f;
                float h = 4f + (float)rng.NextDouble() * 2f;
                var trunk = Part(PrimitiveType.Cylinder, "Palm", root, new Vector3(x, h * 0.5f, z), new Vector3(0.3f, h * 0.5f, 0.3f), m.Base.Wood);
                trunk.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 10f - 5f);
                Part(PrimitiveType.Sphere, "Leaves", root, new Vector3(x, h + 0.2f, z), new Vector3(3f, 0.9f, 3f), m.Base.DuneGrass);
            }
            car = MakeCar(root, new Vector3(-60f, 0f, -1.5f), 90f, m);
            bike = MakeBike(root, new Vector3(-60f, 0f, -1.5f), 90f, m);
            // The protagonist's own model, sitting on the saddle (the sit clip puts the hips about 0.45 m up).
            rider = Cast("Rider", bike.transform, new Vector3(0f, 0.5f, -0.3f), 0f);
            return root;
        }

        static readonly float[] FanZ = { -5.5f, -2.1f, 1.3f, 4.7f };

        /// <summary>A few real fans in the front rows of the televised final, cheering from their seats.</summary>
        static void TvFans(Transform court)
        {
            var models = new[] { "Casual1", "Casual2" };
            int n = 0;
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 4; i++)
                {
                    var fan = Cast(models[n++ % 2], court, new Vector3(side * 8f, 0.4f, FanZ[i]), side > 0 ? -90f : 90f);
                    var so = new SerializedObject(fan);
                    so.FindProperty("idlePose").stringValue = i % 2 == 0 ? "Sitting_Cheer" : "Sitting_Clap";
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
        }
    }
}
