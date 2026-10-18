using System.Collections.Generic;
using BeachVolley.Ball;
using BeachVolley.CameraRig;
using BeachVolley.Match;
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
    /// Builds the story scenes from code: StoryHome (bedroom + a far-away "TV broadcast"
    /// court rendered onto the TV), StoryClub (Coastline Beach Club: training court, a second
    /// court with players warming up, Steve, Dave, Marlon) and, from Chapter 3, StoryTown (a gym,
    /// the beachside café, a food truck and the promenade). Run Build Foundation first (prefabs).
    /// Safe to re-run; every scene is rebuilt from scratch.
    /// </summary>
    public static class StoryBuilder
    {
        public const string HomeScenePath = "Assets/Scenes/StoryHome.unity";
        public const string ClubScenePath = "Assets/Scenes/StoryClub.unity";
        public const string TownScenePath = "Assets/Scenes/StoryTown.unity";
        const string PrefabDir = "Assets/Prefabs";
        const string StoryDir = "Assets/Story";
        const string TvFeedPath = StoryDir + "/TvFeed.renderTexture";

        const string HandheldNoisePath = "Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset";
        static readonly Vector3 TravelOrigin = new Vector3(-500f, 0f, 0f);
        static readonly Vector3 BroadcastOrigin = new Vector3(500f, 0f, 0f);

        [MenuItem("Beach Volleyball/Build Story (Chapters 1-3)")]
        public static void BuildAll()
        {
            CharacterBuilder.BuildAll();
            BuildScenes();
        }

        /// <summary>Just the scenes, from the character prefabs already built (much faster).</summary>
        [MenuItem("Beach Volleyball/Build Story Scenes Only")]
        public static void BuildScenes()
        {
            FoundationBuilder.EnsureFolder(StoryDir);
            BuildHome();
            BuildClub();
            BuildTown();
            MenuBuilder.ApplyBuildScenes();
            Debug.Log("[StoryBuilder] Built " + HomeScenePath + ", " + ClubScenePath + " and " + TownScenePath);
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
        static ScriptedPlay Players(Transform court, Transform[] near, StoryMats m, string far1, string far2, float tempo, params string[] subs)
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
            if (subs.Length > 0)
            {
                var bench = new Transform[subs.Length];
                for (int i = 0; i < subs.Length; i++) bench[i] = Cast(subs[i], court, new Vector3(-4.5f - i, 0f, -6f), 0f).transform;
                WireArray(play, "substitutes", bench);
            }
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

            // Chapter 2 shares the room; only the director for the save's chapter runs.
            var director2 = new GameObject("Chapter2Home").AddComponent<Chapter2Home>();
            director2.transform.SetParent(world, false);
            Wire(director2,
                ("ui", ui), ("cam", cam.GetComponent<CinematicCamera>()), ("player", player.transform),
                ("tvMatch", broadcast.GetComponent<ScriptedPlay>()), ("tvScreen", parts["Screen"].gameObject),
                ("bedSpot", parts["BedSpot"]), ("bedSeat", parts["BedSeat"]), ("doorSpot", parts["DoorSpot"]),
                ("bagOnFloor", parts["Bag"].gameObject), ("bagByDoor", parts["BagByDoor"].gameObject),
                ("bag", parts["Bag"].GetComponent<Interactable>()), ("bottle", parts["Bottle"].GetComponent<Interactable>()),
                ("phone", parts["Phone"].GetComponent<Interactable>()), ("tv", parts["TvSpot"].GetComponent<Interactable>()),
                ("door", parts["Door"].GetComponent<Interactable>()), ("laptop", parts["Laptop"].GetComponent<Interactable>()),
                ("bed", parts["Bed"].GetComponent<Interactable>()), ("ceilingLight", parts["CeilingLight"].GetComponent<Light>()),
                ("travelCar", travelCar.transform), ("travelBike", travelBike.transform), ("rider", rider));

            // Chapter 3: the same room, plus the timing bar for Day 4's wall passes.
            var director3 = new GameObject("Chapter3Home").AddComponent<Chapter3Home>();
            director3.transform.SetParent(world, false);
            var homeGym = Wire(director3.gameObject.AddComponent<GymSession>(), ("ui", ui), ("skills", player.GetComponent<PlayerSkills>()));
            Wire(director3,
                ("ui", ui), ("cam", cam.GetComponent<CinematicCamera>()), ("player", player.transform),
                ("tvMatch", broadcast.GetComponent<ScriptedPlay>()), ("tvScreen", parts["Screen"].gameObject),
                ("bedSpot", parts["BedSpot"]), ("bedSeat", parts["BedSeat"]), ("doorSpot", parts["DoorSpot"]),
                ("bagOnFloor", parts["Bag"].gameObject), ("bagByDoor", parts["BagByDoor"].gameObject),
                ("bag", parts["Bag"].GetComponent<Interactable>()), ("bottle", parts["Bottle"].GetComponent<Interactable>()),
                ("phone", parts["Phone"].GetComponent<Interactable>()), ("tv", parts["TvSpot"].GetComponent<Interactable>()),
                ("door", parts["Door"].GetComponent<Interactable>()), ("laptop", parts["Laptop"].GetComponent<Interactable>()),
                ("bed", parts["Bed"].GetComponent<Interactable>()), ("ceilingLight", parts["CeilingLight"].GetComponent<Light>()),
                ("travelCar", travelCar.transform), ("travelBike", travelBike.transform), ("rider", rider), ("gym", homeGym));

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
            // Chapter 2: the laptop (player development) and the bed (end of the evening).
            var laptop = Box("Laptop", room, new Vector3(1.8f, 0.775f, 1.62f), new Vector3(0.38f, 0.02f, 0.26f), m.Dark);
            var lid = Box("LaptopLid", laptop.transform, new Vector3(0f, 5.5f, 0.48f), new Vector3(1f, 11f, 0.06f), m.Dark);
            lid.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            parts["Laptop"] = Interact(laptop.transform, "laptop", "Open Player Development", 1.1f).transform;
            var bedUse = new GameObject("BedUse").transform;
            bedUse.SetParent(room, false);
            bedUse.localPosition = new Vector3(-0.8f, 0f, 1.2f);
            parts["Bed"] = Interact(bedUse, "bed", "Go to sleep", 0.9f).transform;
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
            var alex = Cast("Alex", people, new Vector3(-1.4f, 0f, -2f), 0f);
            var nina = Cast("Nina", people, new Vector3(1.2f, 0f, -3f), 0f);

            // Second court: Marlon and three club players warming up.
            var court2 = DecorCourt("Court2", world, new Vector3(14f, 0f, 0f), m, false);
            var marlon = Cast("Marlon", court2, new Vector3(-1.5f, 0f, -4.5f), 0f);
            var marlonMate = Cast("ClubPlayer", court2, new Vector3(1.4f, 0f, -3.6f), 0f);
            var rally = Players(court2, new[] { marlon.transform, marlonMate.transform }, m, "ClubFar1", "ClubFar2", 1f, "ClubSub");

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

            // Chapter 2: match AI on the players, talk spots, break props, drill zones.
            // Dave: a few weeks ahead of the protagonist, no more.
            CourtAi(dave, new NpcProfile { passing = 4, setting = 4, attacking = 4, serving = 4, defense = 3, blocking = 3, speed = 4,
                blocks = false, callsEarly = true, serveAtWeakest = 0.3f, callMine = "Mine!", callYours = "Yours! Go!" });
            // Marlon is the best player at the club: high jump, hard spikes, deep rolls, a real block.
            CourtAi(marlon, new NpcProfile { passing = 8, setting = 6, attacking = 10, serving = 9, defense = 7, blocking = 9, speed = 8,
                blocks = true, callsEarly = false, serveAtWeakest = 0.55f, callMine = "Ball.", callYours = "" });
            // Alex: steady and technical, a good setter, not much of a jumper.
            CourtAi(alex, new NpcProfile { passing = 6, setting = 7, attacking = 5, serving = 6, defense = 5, blocking = 4, speed = 5,
                blocks = false, callsEarly = true, serveAtWeakest = 0.6f, callMine = "Got it.", callYours = "Yours." });
            foreach (var who in new[] { dave, alex, marlon }) Interact(who.transform, who.name.ToLowerInvariant(), "Talk", 1.7f);
            var cooler = Box("WaterCooler", world, new Vector3(-7f, 0.45f, -6.4f), new Vector3(0.5f, 0.9f, 0.5f), m.Bottle);
            Box("CoolerTop", cooler.transform, new Vector3(0f, 0.6f, 0f), new Vector3(1.4f, 0.2f, 1.4f), m.Base.Wood);
            var coolerUse = Interact(cooler.transform, "cooler", "Get some water", 1.3f);
            var board = new GameObject("ClubBoard").transform;
            board.SetParent(world, false);
            board.SetLocalPositionAndRotation(new Vector3(-8.4f, 0f, -9.8f), Quaternion.Euler(0f, 60f, 0f));
            foreach (float sx in new[] { -0.7f, 0.7f })
                Box("BoardLeg", board, new Vector3(sx, 0.8f, 0f), new Vector3(0.08f, 1.6f, 0.08f), m.Base.Wood);
            Box("BoardFace", board, new Vector3(0f, 1.45f, 0f), new Vector3(1.6f, 1f, 0.06f), m.Base.Poster);
            var boardUse = Interact(board, "board", "Read the club board", 1.6f);
            var courtSpot = new GameObject("BackToCourt").transform;
            courtSpot.SetParent(world, false);
            courtSpot.localPosition = new Vector3(0f, 0f, -5f);
            var courtUse = Interact(courtSpot, "court", "Back to the court", 1.4f);
            var zoneLine = Ring("ZoneLine", world, StoryDrill.LineSpot, m.Base.Marker);
            var zoneCross = Ring("ZoneCross", world, StoryDrill.CrossSpot, m.Base.Marker);
            var offNet = Box("OffNetLine", world, new Vector3(0f, 0.012f, StoryDrill.OffNetZ), new Vector3(BeachVolley.Court.CourtDimensions.Width, 0.02f, 0.1f), m.Base.Marker);
            offNet.SetActive(false);

            var ui = StoryUi(world);
            var story = new GameObject("Story").transform;
            story.SetParent(world, false);
            var drill = Wire(story.gameObject.AddComponent<StoryDrill>(),
                ("ball", vb), ("contact", contact), ("skills", player.GetComponent<PlayerSkills>()),
                ("steve", steve.transform), ("dave", dave.transform), ("marker", marker.transform), ("ui", ui),
                ("zoneLine", zoneLine), ("zoneCross", zoneCross), ("offNetLine", offNet.transform));
            var match = Wire(story.gameObject.AddComponent<StoryMatch>(), ("ball", vb), ("contact", contact), ("ui", ui));
            Wire(story.gameObject.AddComponent<SkillToasts>(), ("skills", player.GetComponent<PlayerSkills>()));
            Wire(story.gameObject.AddComponent<Chapter1Club>(),
                ("ui", ui), ("cam", cam.GetComponent<CinematicCamera>()), ("player", player.transform),
                ("steve", steve), ("dave", dave), ("marlon", marlon), ("clubRally", rally), ("drill", drill),
                ("contact", contact), ("car", car), ("bike", bike));
            Wire(story.gameObject.AddComponent<Chapter2Club>(),
                ("ui", ui), ("cam", cam.GetComponent<CinematicCamera>()), ("player", player.transform),
                ("steve", steve), ("dave", dave), ("marlon", marlon), ("alex", alex), ("nina", nina),
                ("clubRally", rally), ("drill", drill), ("match", match), ("contact", contact), ("car", car), ("bike", bike),
                ("cooler", coolerUse), ("board", boardUse), ("court", courtUse));
            Wire(story.gameObject.AddComponent<Chapter3Club>(),
                ("ui", ui), ("cam", cam.GetComponent<CinematicCamera>()), ("player", player.transform),
                ("steve", steve), ("dave", dave), ("marlon", marlon), ("alex", alex), ("nina", nina),
                ("clubRally", rally), ("drill", drill), ("match", match), ("contact", contact), ("car", car), ("bike", bike));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------- StoryTown (Chapter 3) ----------

        /// <summary>Where the gym interior sits: far from the beach so the sea and sun never show inside.</summary>
        static readonly Vector3 GymOrigin = new Vector3(0f, 0f, -400f);

        /// <summary>
        /// The beachfront near the club: a promenade along the sea (north, +z) with lamps and a
        /// railing, a café terrace on it, a food truck with a picnic table further west, and a small
        /// gym interior far away (its own lights, no sky). Sand from the backdrop is the floor outside.
        /// </summary>
        static void BuildTown()
        {
            ClearScene(out var scene, TownScenePath);
            var world = new GameObject("World").transform;
            FoundationBuilder.BuildBackdrop(world);
            // The backdrop brings a court and a net; the town keeps only the sand under them.
            foreach (var t in world.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                if (t.name == "Net") Object.DestroyImmediate(t.gameObject);
            }
            var courtRoot = world.Find("Court");
            if (courtRoot != null)
            {
                PrefabUtility.UnpackPrefabInstance(courtRoot.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var lines = courtRoot.Find("Lines");
                if (lines != null) Object.DestroyImmediate(lines.gameObject);
                Object.DestroyImmediate(courtRoot.GetComponent<BeachVolley.Court.SoftBounds>());
            }
            var m = new StoryMats();
            var deck = FoundationBuilder.Materials.Lit("Boardwalk", new Color(0.62f, 0.48f, 0.33f), 0.2f);
            var white = FoundationBuilder.Materials.Lit("TownWhite", new Color(0.94f, 0.93f, 0.9f), 0.2f);
            var awning = FoundationBuilder.Materials.Lit("Awning", new Color(0.12f, 0.55f, 0.6f), 0.15f);
            var stripe = FoundationBuilder.Materials.Lit("AwningStripe", new Color(0.97f, 0.95f, 0.88f), 0.15f);
            var umbrella = FoundationBuilder.Materials.Lit("Umbrella", new Color(0.95f, 0.5f, 0.25f), 0.1f);
            var truck = FoundationBuilder.Materials.Lit("FoodTruck", new Color(0.96f, 0.78f, 0.2f), 0.5f, 0.2f);
            var rubber = FoundationBuilder.Materials.Lit("GymFloor", new Color(0.13f, 0.14f, 0.16f), 0.15f);
            var gymWall = FoundationBuilder.Materials.Lit("GymWall", new Color(0.78f, 0.8f, 0.82f), 0.1f);
            // Matte "mirror": a real reflection would show the sky, since the gym sits outdoors far away.
            var mirror = FoundationBuilder.Materials.Lit("GymMirror", new Color(0.62f, 0.68f, 0.72f), 0.55f);
            var iron = FoundationBuilder.Materials.Lit("GymIron", new Color(0.2f, 0.2f, 0.22f), 0.6f, 0.8f);
            var plate = FoundationBuilder.Materials.Lit("GymPlate", new Color(0.08f, 0.08f, 0.09f), 0.3f);
            var pad = FoundationBuilder.Materials.Lit("GymPad", new Color(0.55f, 0.08f, 0.1f), 0.3f);
            var glow = FoundationBuilder.Materials.Lit("LampGlow", new Color(1f, 0.85f, 0.55f), 0.3f);

            var town = new GameObject("Town").transform;
            town.SetParent(world, false);
            var spots = new Dictionary<string, Transform>();
            Transform Spot(string name, Vector3 pos, float yaw)
            {
                var s = new GameObject(name).transform;
                s.SetParent(town, false);
                s.SetLocalPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                spots[name] = s;
                return s;
            }

            // ---- Promenade: a boardwalk along z = 23, railing on the sea side, lamps. ----
            var prom = new GameObject("Promenade").transform;
            prom.SetParent(town, false);
            Box("Boardwalk", prom, new Vector3(0f, 0.04f, 23f), new Vector3(70f, 0.08f, 3.4f), deck);
            Box("RailTop", prom, new Vector3(0f, 1f, 24.75f), new Vector3(70f, 0.06f, 0.06f), white);
            Box("RailMid", prom, new Vector3(0f, 0.55f, 24.75f), new Vector3(70f, 0.04f, 0.04f), white);
            for (float x = -34f; x <= 34f; x += 2.5f)
                Box("RailPost", prom, new Vector3(x, 0.5f, 24.75f), new Vector3(0.07f, 1f, 0.07f), white);
            for (float x = -30f; x <= 30f; x += 7.5f)
            {
                Part(PrimitiveType.Cylinder, "LampPost", prom, new Vector3(x, 1.7f, 21.5f), new Vector3(0.1f, 1.7f, 0.1f), m.Base.Post);
                Part(PrimitiveType.Sphere, "LampHead", prom, new Vector3(x, 3.45f, 21.5f), Vector3.one * 0.32f, glow)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Spot("PromenadeStart", new Vector3(-3.5f, 0f, 23f), 90f);
            Spot("Junction", new Vector3(13f, 0f, 23f), 90f);
            // A side path inland at the junction.
            Box("SidePath", prom, new Vector3(13f, 0.03f, 17f), new Vector3(2.2f, 0.06f, 9f), deck);

            // ---- Café terrace (x −10..0, z 15..21.3) facing the sea. ----
            var cafe = new GameObject("Cafe").transform;
            cafe.SetParent(town, false);
            Box("CafeDeck", cafe, new Vector3(-5f, 0.05f, 18.2f), new Vector3(10f, 0.1f, 6.2f), deck);
            Box("Kiosk", cafe, new Vector3(-5f, 1.35f, 13.6f), new Vector3(6f, 2.7f, 2.4f), white);
            Box("Counter", cafe, new Vector3(-5f, 0.55f, 15.05f), new Vector3(4.6f, 1.1f, 0.5f), m.Base.Wood);
            Box("Window", cafe, new Vector3(-5f, 1.75f, 14.79f), new Vector3(4f, 0.9f, 0.02f), m.Dark);
            for (int i = 0; i < 6; i++)
                Box("Awning" + i, cafe, new Vector3(-7.5f + i * 1f, 2.75f, 15.45f), new Vector3(1f, 0.06f, 1.4f), i % 2 == 0 ? awning : stripe)
                    .transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            Box("CafeSign", cafe, new Vector3(-5f, 3.05f, 14.78f), new Vector3(3f, 0.5f, 0.06f), awning);
            // Text faces south, toward the terrace.
            var label = new GameObject("SignText").AddComponent<TextMesh>();
            label.transform.SetParent(cafe, false);
            label.transform.localPosition = new Vector3(-5f, 3.05f, 14.74f);
            label.text = "SALT & SAND CAFÉ";
            label.fontSize = 64;
            label.characterSize = 0.035f;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = Color.white;

            // Tables: round tops, chairs, umbrellas. The story table is the middle one.
            var tables = new[] { new Vector3(-8.2f, 0f, 18.4f), new Vector3(-4.6f, 0f, 19.2f), new Vector3(-1.6f, 0f, 17.6f) };
            foreach (var tp in tables)
            {
                Part(PrimitiveType.Cylinder, "TableTop", cafe, tp + new Vector3(0f, 0.74f, 0f), new Vector3(0.9f, 0.02f, 0.9f), white);
                Part(PrimitiveType.Cylinder, "TableLeg", cafe, tp + new Vector3(0f, 0.37f, 0f), new Vector3(0.06f, 0.37f, 0.06f), m.Base.Post);
                Part(PrimitiveType.Cylinder, "UmbrellaPole", cafe, tp + new Vector3(0f, 1.3f, 0f), new Vector3(0.04f, 1.3f, 0.04f), m.Base.Wood);
                Part(PrimitiveType.Sphere, "Umbrella", cafe, tp + new Vector3(0f, 2.45f, 0f), new Vector3(2.4f, 0.35f, 2.4f), umbrella)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
                foreach (float sx in new[] { -1f, 1f })
                {
                    // Chairs face each other across the table (east/west), hips 0.75 m from its centre.
                    var c = tp + new Vector3(sx * 0.85f, 0f, 0f);
                    Box("ChairSeat", cafe, c + new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.05f, 0.45f), m.Base.Wood);
                    Box("ChairBack", cafe, c + new Vector3(sx * 0.22f, 0.75f, 0f), new Vector3(0.05f, 0.6f, 0.45f), m.Base.Wood);
                    foreach (var lx in new[] { -0.18f, 0.18f }) foreach (var lz in new[] { -0.18f, 0.18f })
                        Box("ChairLeg", cafe, c + new Vector3(lx, 0.22f, lz), new Vector3(0.04f, 0.44f, 0.04f), m.Base.Post);
                }
            }
            var story = tables[1];
            // Seat transforms: hips over the chair, facing the table.
            Spot("CafeSeatYou", story + new Vector3(-0.85f, 0f, 0f), 90f);
            Spot("CafeSeatDave", story + new Vector3(0.85f, 0f, 0f), -90f);
            Spot("CafeArrive", new Vector3(-7f, 0f, 22.6f), 90f);
            Box("Mug", cafe, story + new Vector3(-0.3f, 0.8f, 0.1f), new Vector3(0.09f, 0.11f, 0.09f), white);
            Box("Mug", cafe, story + new Vector3(0.3f, 0.8f, -0.1f), new Vector3(0.09f, 0.11f, 0.09f), white);
            Box("Plate", cafe, story + new Vector3(0.25f, 0.76f, 0.2f), new Vector3(0.26f, 0.015f, 0.26f), white);
            Box("Plate", cafe, story + new Vector3(-0.25f, 0.76f, -0.2f), new Vector3(0.26f, 0.015f, 0.26f), white);

            // ---- Food truck and a picnic table (x ≈ −20). ----
            var food = new GameObject("FoodSpot").transform;
            food.SetParent(town, false);
            Box("Truck", food, new Vector3(-20f, 1.35f, 13.6f), new Vector3(5f, 2.2f, 2.2f), truck);
            Box("Hatch", food, new Vector3(-20f, 1.6f, 14.72f), new Vector3(2.4f, 0.8f, 0.04f), m.Dark);
            Box("HatchFlap", food, new Vector3(-20f, 2.25f, 15f), new Vector3(2.6f, 0.05f, 0.7f), stripe).transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            foreach (float wx in new[] { -1.6f, 1.6f })
                Part(PrimitiveType.Cylinder, "Wheel", food, new Vector3(-20f + wx, 0.35f, 14.72f), new Vector3(0.7f, 0.08f, 0.7f), m.Dark)
                    .localRotation = Quaternion.Euler(90f, 0f, 0f);
            var picnic = new Vector3(-20f, 0f, 18.4f);
            Box("PicnicTop", food, picnic + new Vector3(0f, 0.74f, 0f), new Vector3(2.4f, 0.06f, 0.9f), m.Base.Wood);
            foreach (float bz in new[] { -0.75f, 0.75f })
                Box("PicnicBench", food, picnic + new Vector3(0f, 0.45f, bz), new Vector3(2.4f, 0.05f, 0.32f), m.Base.Wood);
            foreach (float lx in new[] { -1f, 1f })
                Box("PicnicLeg", food, picnic + new Vector3(lx, 0.37f, 0f), new Vector3(0.08f, 0.74f, 1.7f), m.Base.Wood);
            // Hips on the benches: south bench faces north (+z), north bench faces south.
            Spot("FoodSeatYou", picnic + new Vector3(-0.5f, 0f, -0.75f), 0f);
            Spot("FoodSeatDave", picnic + new Vector3(0.5f, 0f, -0.75f), 0f);
            Spot("FoodSeatAlex", picnic + new Vector3(-0.5f, 0f, 0.75f), 180f);
            Spot("FoodSeatNina", picnic + new Vector3(0.5f, 0f, 0.75f), 180f);
            Box("Fries", food, picnic + new Vector3(0f, 0.8f, 0f), new Vector3(0.3f, 0.06f, 0.2f), umbrella);
            var string1 = new GameObject("StringLights").transform;
            string1.SetParent(food, false);
            for (int i = 0; i < 9; i++)
            {
                float x = -22.4f + i * 0.6f;
                float sag = Mathf.Sin(i / 8f * Mathf.PI) * 0.35f;
                Part(PrimitiveType.Sphere, "Bulb", string1, new Vector3(x, 2.9f - sag, 16.2f), Vector3.one * 0.12f, glow)
                    .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var foodLight = new GameObject("FoodLight").AddComponent<Light>();
            foodLight.transform.SetParent(food, false);
            foodLight.transform.localPosition = new Vector3(-20f, 2.8f, 17f);
            foodLight.type = LightType.Point;
            foodLight.color = new Color(1f, 0.8f, 0.55f);
            foodLight.intensity = 2.5f;
            foodLight.range = 9f;
            foodLight.gameObject.SetActive(false);

            // ---- Gym interior, 10 × 8 m, far away. Mirror wall north, racks east, bench and box west. ----
            var gym = new GameObject("Gym").transform;
            gym.SetParent(town, false);
            gym.localPosition = GymOrigin;
            const float gx = 5f, gz = 4f, gh = 3.2f;
            Box("GymFloor", gym, new Vector3(0f, -0.05f, 0f), new Vector3(gx * 2f, 0.1f, gz * 2f), rubber, true);
            Box("GymCeiling", gym, new Vector3(0f, gh, 0f), new Vector3(gx * 2f, 0.1f, gz * 2f), white);
            Box("GymWall_N", gym, new Vector3(0f, gh / 2f, gz), new Vector3(gx * 2f, gh, 0.2f), gymWall, true);
            Box("GymWall_S", gym, new Vector3(0f, gh / 2f, -gz), new Vector3(gx * 2f, gh, 0.2f), gymWall, true);
            Box("GymWall_E", gym, new Vector3(gx, gh / 2f, 0f), new Vector3(0.2f, gh, gz * 2f), gymWall, true);
            Box("GymWall_W", gym, new Vector3(-gx, gh / 2f, 0f), new Vector3(0.2f, gh, gz * 2f), gymWall, true);
            Box("Mirror", gym, new Vector3(0f, 1.4f, gz - 0.11f), new Vector3(6f, 2f, 0.02f), mirror);
            Box("Stripe", gym, new Vector3(0f, 2.6f, gz - 0.11f), new Vector3(gx * 2f, 0.12f, 0.02f), pad);
            // Squat rack with a loaded bar.
            foreach (float rx in new[] { -0.6f, 0.6f })
                Box("RackUpright", gym, new Vector3(3.6f + rx, 1.1f, 1.6f), new Vector3(0.08f, 2.2f, 0.08f), iron);
            Part(PrimitiveType.Cylinder, "Bar", gym, new Vector3(3.6f, 1.45f, 1.6f), new Vector3(0.03f, 1.1f, 0.03f), iron).localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (float px in new[] { -0.85f, 0.85f })
                Part(PrimitiveType.Cylinder, "Plate", gym, new Vector3(3.6f + px, 1.45f, 1.6f), new Vector3(0.45f, 0.03f, 0.45f), plate).localRotation = Quaternion.Euler(0f, 0f, 90f);
            // Dumbbell rack along the east wall.
            Box("DumbbellRack", gym, new Vector3(4.4f, 0.4f, -1.5f), new Vector3(0.6f, 0.8f, 2.4f), iron);
            for (int i = 0; i < 6; i++)
                Part(PrimitiveType.Cylinder, "Dumbbell", gym, new Vector3(4.4f, 0.85f, -2.5f + i * 0.4f), new Vector3(0.13f, 0.18f, 0.13f), plate).localRotation = Quaternion.Euler(0f, 0f, 90f);
            // Bench, plyo box, mats.
            Box("Bench", gym, new Vector3(-3.4f, 0.42f, 1.2f), new Vector3(0.35f, 0.1f, 1.3f), pad);
            Box("BenchFrame", gym, new Vector3(-3.4f, 0.2f, 1.2f), new Vector3(0.2f, 0.4f, 1.1f), iron);
            Box("PlyoBox", gym, new Vector3(-1.4f, 0.3f, 2.2f), new Vector3(0.75f, 0.6f, 0.6f), m.Base.Wood);
            Box("Mat", gym, new Vector3(0f, 0.01f, 0f), new Vector3(2f, 0.02f, 1.2f), awning);
            Box("Mat2", gym, new Vector3(-2.6f, 0.01f, -2f), new Vector3(2f, 0.02f, 1.2f), awning);
            Box("Kettlebell", gym, new Vector3(-4.3f, 0.15f, -3.2f), new Vector3(0.25f, 0.3f, 0.25f), plate);
            Box("GymPoster", gym, new Vector3(-gx + 0.11f, 1.7f, -1.4f), new Vector3(0.02f, 1f, 0.75f), m.Poster);
            foreach (var lp in new[] { new Vector3(-2.5f, 2.9f, 0f), new Vector3(2.5f, 2.9f, 0f) })
            {
                var l = new GameObject("GymLight").AddComponent<Light>();
                l.transform.SetParent(gym, false);
                l.transform.localPosition = lp;
                l.type = LightType.Point;
                l.color = new Color(0.95f, 0.97f, 1f);
                l.intensity = 3.2f;
                l.range = 9f;
                l.shadows = LightShadows.Soft;
                Box("GymLamp", gym, lp + Vector3.up * 0.22f, new Vector3(1.6f, 0.05f, 0.3f), white);
            }
            // More iron: a lifting platform with a loaded bar, a plate tree, a second dumbbell rack.
            Box("Platform", gym, new Vector3(1.6f, 0.02f, -2.2f), new Vector3(2.4f, 0.04f, 2.4f), m.Base.Wood);
            BarbellProp("FloorBarbell", gym, new Vector3(1.6f, 0.225f, -2.2f), m, iron, plate);
            Part(PrimitiveType.Cylinder, "PlateTree", gym, new Vector3(-4.3f, 0.6f, 2.8f), new Vector3(0.08f, 0.6f, 0.08f), iron);
            for (int i = 0; i < 4; i++)
                Part(PrimitiveType.Cylinder, "TreePlate", gym, new Vector3(-4.3f + (i % 2 == 0 ? 0.1f : -0.1f), 0.3f + i * 0.25f, 2.8f),
                    new Vector3(0.45f - i * 0.05f, 0.03f, 0.45f - i * 0.05f), plate).localRotation = Quaternion.Euler(0f, 0f, 90f);
            Box("DumbbellRack2", gym, new Vector3(-4.4f, 0.35f, -0.4f), new Vector3(0.6f, 0.7f, 1.8f), iron);
            for (int i = 0; i < 4; i++)
                DumbbellProp("RackDumbbell", gym, new Vector3(-4.4f, 0.78f, -1.1f + i * 0.45f), plate, iron).localRotation = Quaternion.Euler(0f, 90f, 0f);
            // The lifts' own equipment, carried in the hands by GymSession (hidden until a set starts).
            var liftBar = BarbellProp("LiftBarbell", gym, new Vector3(0f, 1f, 0f), m, iron, plate);
            var dbL = DumbbellProp("LiftDumbbell_L", gym, new Vector3(-0.3f, 1f, 0f), plate, iron);
            var dbR = DumbbellProp("LiftDumbbell_R", gym, new Vector3(0.3f, 1f, 0f), plate, iron);
            foreach (var t in new[] { liftBar, dbL, dbR }) t.gameObject.SetActive(false);
            Spot("GymSpot", GymOrigin + new Vector3(0f, 0f, 0f), 0f);
            Spot("GymDoor", GymOrigin + new Vector3(-3.8f, 0f, -3.2f), 30f);

            // ---- People ----
            var player = Player(world, new Vector3(-7f, 0f, 22.6f), 90f);
            DisableGameplay(player);
            var cam = MainCamera(world, false, player.transform);
            cam.transform.SetPositionAndRotation(new Vector3(-4.6f, 1.8f, 15.5f), Quaternion.LookRotation(new Vector3(0f, -0.1f, 1f)));
            var people = new GameObject("People").transform;
            people.SetParent(world, false);
            var dave = Cast("Dave", people, new Vector3(-6f, 0f, 22.6f), 90f);
            var alex = Cast("Alex", people, new Vector3(-21f, 0f, 20f), 180f);
            var nina = Cast("Nina", people, new Vector3(-19f, 0f, 20f), 180f);
            // Café regulars (one ordering, one at the railing) and a lifter in the gym, for life in the background.
            var regulars = new GameObject("CafeRegulars").transform;
            regulars.SetParent(world, false);
            SetIdlePose(Cast("Casual1", regulars, new Vector3(-6.2f, 0f, 15.8f), 180f), "Texting");
            SetIdlePose(Cast("Casual2", regulars, new Vector3(4.5f, 0f, 24.2f), 0f), "Tired_Idle");
            // The gym is a child of the town at GymOrigin, so the lifter's position is local to it.
            SetIdlePose(Cast("ClubFar2", gym, new Vector3(3f, 0f, -2.2f), -60f), "ArmsCrossed");

            // ---- Shots ----
            var shots = new GameObject("Shots").transform;
            shots.SetParent(world, false);
            Shot("CafeWide", shots, new Vector3(-13.5f, 2.6f, 16.5f), new Vector3(-4.5f, 0.9f, 20.5f), 50f);
            Shot("Cafe", shots, story + new Vector3(0.1f, 1.35f, -2.6f), story + new Vector3(0f, 0.95f, 0.4f), 44f);
            Shot("CafeSea", shots, story + new Vector3(-0.2f, 1.25f, -1.4f), story + new Vector3(0f, 1.2f, 8f), 50f);
            Shot("Food", shots, picnic + new Vector3(2.8f, 1.6f, -2.4f), picnic + new Vector3(0f, 0.9f, 0f), 46f);
            Shot("FoodWide", shots, picnic + new Vector3(6f, 2.6f, -5f), picnic + new Vector3(0f, 1f, -1f), 52f);
            // In front of the walk (west → east), looking back at them with the sea on the right.
            Shot("Promenade", shots, new Vector3(7f, 1.6f, 21.6f), new Vector3(-2f, 1.2f, 23.2f), 44f);
            Shot("Junction", shots, new Vector3(16.5f, 1.8f, 19.5f), new Vector3(12.6f, 1.2f, 23f), 46f);
            // Front-side: he faces the mirror (+z), so the lifts read from here.
            Shot("Gym", shots, GymOrigin + new Vector3(2.4f, 1.45f, 2.5f), GymOrigin + new Vector3(0f, 0.95f, 0f), 50f);
            Shot("GymWide", shots, GymOrigin + new Vector3(-4.3f, 2.5f, -3.5f), GymOrigin + new Vector3(0.5f, 0.9f, 0.8f), 58f);
            Shot("GymMirror", shots, GymOrigin + new Vector3(0.4f, 1.5f, -1.8f), GymOrigin + new Vector3(0f, 1.2f, 4f), 46f);

            // ---- Story systems ----
            var ui = StoryUi(world);
            var dir = new GameObject("Story").transform;
            dir.SetParent(world, false);
            var session = Wire(dir.gameObject.AddComponent<GymSession>(), ("ui", ui), ("skills", player.GetComponent<PlayerSkills>()),
                ("barbell", liftBar), ("dumbbellLeft", dbL), ("dumbbellRight", dbR));
            Wire(dir.gameObject.AddComponent<Chapter3Town>(),
                ("ui", ui), ("cam", cam.GetComponent<CinematicCamera>()), ("player", player.transform),
                ("dave", dave), ("alex", alex), ("nina", nina), ("gym", session), ("foodLight", foodLight),
                ("cafeArrive", spots["CafeArrive"]), ("cafeSeatYou", spots["CafeSeatYou"]), ("cafeSeatDave", spots["CafeSeatDave"]),
                ("promenadeStart", spots["PromenadeStart"]), ("junction", spots["Junction"]),
                ("foodSeatYou", spots["FoodSeatYou"]), ("foodSeatDave", spots["FoodSeatDave"]),
                ("foodSeatAlex", spots["FoodSeatAlex"]), ("foodSeatNina", spots["FoodSeatNina"]),
                ("gymSpot", spots["GymSpot"]), ("gymDoor", spots["GymDoor"]));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>An Olympic barbell, 2.2 m, along its local X: bar, sleeves, two 45 cm plates a side, collars.</summary>
        static Transform BarbellProp(string name, Transform parent, Vector3 pos, StoryMats m, Material iron, Material plate)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            var side = Quaternion.Euler(0f, 0f, 90f);
            Part(PrimitiveType.Cylinder, "Bar", root, Vector3.zero, new Vector3(0.028f, 1.1f, 0.028f), iron).localRotation = side;
            foreach (float s in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Cylinder, "Sleeve", root, new Vector3(s * 0.85f, 0f, 0f), new Vector3(0.05f, 0.24f, 0.05f), m.Base.Post).localRotation = side;
                Part(PrimitiveType.Cylinder, "Plate", root, new Vector3(s * 0.7f, 0f, 0f), new Vector3(0.45f, 0.025f, 0.45f), plate).localRotation = side;
                Part(PrimitiveType.Cylinder, "Plate", root, new Vector3(s * 0.76f, 0f, 0f), new Vector3(0.45f, 0.025f, 0.45f), plate).localRotation = side;
                Part(PrimitiveType.Cylinder, "Collar", root, new Vector3(s * 0.81f, 0f, 0f), new Vector3(0.08f, 0.015f, 0.08f), iron).localRotation = side;
            }
            foreach (var r in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(r);
            return root;
        }

        /// <summary>A hex dumbbell, handle along its local X.</summary>
        static Transform DumbbellProp(string name, Transform parent, Vector3 pos, Material head, Material handle)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            var side = Quaternion.Euler(0f, 0f, 90f);
            Part(PrimitiveType.Cylinder, "Handle", root, Vector3.zero, new Vector3(0.032f, 0.09f, 0.032f), handle).localRotation = side;
            foreach (float s in new[] { -1f, 1f })
                Box("Head", root, new Vector3(s * 0.12f, 0f, 0f), new Vector3(0.06f, 0.13f, 0.13f), head);
            foreach (var r in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(r);
            return root;
        }

        /// <summary>Gives an extra a held pose from the start (sitting, texting...).</summary>
        static void SetIdlePose(StoryActor actor, string pose)
        {
            var so = new SerializedObject(actor);
            so.FindProperty("idlePose").stringValue = pose;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Match AI body on a cast member (off until a match starts).</summary>
        static void CourtAi(StoryActor who, NpcProfile profile)
        {
            var ai = who.gameObject.AddComponent<CourtAI>();
            ai.profile = profile;
            ai.enabled = false;
            EditorUtility.SetDirty(ai);
        }

        /// <summary>A flat ring on the sand marking a defensive zone (hidden until a drill shows it).</summary>
        static Transform Ring(string name, Transform parent, Vector3 at, Material mat)
        {
            var ring = Part(PrimitiveType.Cylinder, name, parent, at + Vector3.up * 0.01f, new Vector3(2.6f, 0.005f, 2.6f), mat);
            ring.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            ring.gameObject.SetActive(false);
            return ring;
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
            // Stands just ahead of the saddle; the sit clip carries the hips back onto it.
            rider = Cast("Rider", bike.transform, new Vector3(0f, 0.5f, -0.3f + StoryActor.SitTravel), 0f);
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
