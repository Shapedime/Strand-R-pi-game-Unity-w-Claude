using System.Collections.Generic;
using System.IO;
using BeachVolley.Story;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BeachVolley.EditorTools
{
    /// <summary>
    /// Builds the story cast as prefabs in Assets/Prefabs/Characters, plus the shared StoryActor
    /// controller. Each character uses its Tripo → Mixamo model from
    /// Assets/Models/Characters/&lt;Model&gt;/&lt;Model&gt;.fbx when it exists (Humanoid, own avatar,
    /// placed with MixamoSetup.PlaceRigged), and the primitive placeholder body until then.
    /// Story clips come from Assets/Animations/Story/*.fbx (Mixamo, without skin); any that are
    /// missing are simply left out of the controller. Safe to re-run whenever new files arrive.
    /// </summary>
    public static class CharacterBuilder
    {
        public const string CharacterDir = "Assets/Models/Characters";
        public const string StoryClipDir = "Assets/Animations/Story";
        const string PlayerClipDir = "Assets/Animations/Player";
        const string ControllerPath = StoryClipDir + "/StoryActor.controller";
        const string MaskPath = PlayerClipDir + "/UpperBody.mask";
        const string PrefabDir = "Assets/Prefabs/Characters";

        // Clip groups by role. Scripts call these role names (Gesture("Wave")...).
        static readonly string[] UpperOneShots = { "Wave", "Point", "Clap", "Shrug", "Nod", "HeadShake", "Laugh", "Drink", "Arguing", "Dismiss", "Angry", "LookAway", "Happy", "SarcasticNod", "Sigh" };
        static readonly string[] UpperHolds = { "Talking", "ArmsCrossed", "Texting" };
        static readonly string[] BaseOneShots = { "PickUp", "Cheer", "FistPump", "Stretch" };
        static readonly string[] BaseHolds = { "Tired_Idle", "Sitting_Cheer", "Sitting_Clap" };
        static readonly string[] Looping = { "Talking", "ArmsCrossed", "Texting", "Tired_Idle", "SitIdle", "Walk_Casual", "Sitting_Cheer", "Sitting_Clap" };

        /// <summary>
        /// Which Mixamo download plays each role (file names as downloaded, first one found wins),
        /// so the files never need renaming. Files not listed here are ignored.
        /// </summary>
        static readonly (string role, string[] files)[] ClipFiles =
        {
            ("Talking", new[] { "Talking" }),
            ("Wave", new[] { "Waving" }),
            ("Nod", new[] { "head nod yes", "Nod", "acknowledging", "lengthy head nod", "hard head nod" }),
            ("SarcasticNod", new[] { "sarcastic head nod" }),
            ("HeadShake", new[] { "shaking head no (2)", "annoyed head shake", "thoughtful head shake", "Shaking Head No" }),
            ("Shrug", new[] { "Shrugging" }),
            ("Point", new[] { "Pointing" }),
            ("Laugh", new[] { "Laughing" }),
            ("Clap", new[] { "Standing Clap", "Clapping" }),
            ("Cheer", new[] { "Cheering" }),
            ("FistPump", new[] { "Fist Pump", "Standing Up Fist Pump" }),
            ("SitIdle", new[] { "Sitting Idle" }),
            ("StandToSit", new[] { "Stand To Sit" }),
            ("SitToStand", new[] { "Sit To Stand" }),
            ("Texting", new[] { "Texting While Standing" }),
            ("PickUp", new[] { "Picking Up" }),
            ("Drink", new[] { "Drinking" }),
            ("Walk_Casual", new[] { "Walking" }),
            ("Arguing", new[] { "Standing Arguing" }),
            ("Sitting_Clap", new[] { "Sitting Clap" }),
            ("Sitting_Cheer", new[] { "Cheering While Sitting" }),
            // No crossed-arms clip on Mixamo: Marlon's stance is "being cocky".
            ("ArmsCrossed", new[] { "being cocky" }),
            ("Dismiss", new[] { "dismissing gesture" }),
            ("Angry", new[] { "angry gesture" }),
            ("LookAway", new[] { "look away gesture" }),
            ("Happy", new[] { "happy hand gesture" }),
            // No tired clip either: a sigh, a stretch and a restless weight shift stand in.
            ("Sigh", new[] { "relieved sigh" }),
            ("Stretch", new[] { "Arm Stretching" }),
            ("Tired_Idle", new[] { "weight shift" }),
        };

        /// <summary>One member of the cast. <c>model</c> is the FBX path under Assets/Models/Characters, without ".fbx".</summary>
        public class Def
        {
            public string name, display, model;
            public float height = 1.8f;
            public Color shirt, shorts, tag = Color.white;
            public Color? skin;
            public bool nameTag = true;
        }

        public static readonly Def[] Cast =
        {
            new Def { name = "Steve", display = "Steve", model = "CoachSteve/CoachSteve", height = 1.82f, shirt = new Color(0.15f, 0.55f, 0.35f), shorts = new Color(0.2f, 0.2f, 0.22f), tag = new Color(0.6f, 1f, 0.7f) },
            new Def { name = "Dave", display = "Dave", model = "Dave/Dave", height = 1.78f, shirt = new Color(0.98f, 0.8f, 0.15f), shorts = new Color(0.45f, 0.45f, 0.48f), tag = new Color(1f, 0.9f, 0.4f) },
            new Def { name = "Marlon", display = "Marlon", model = "Marlon/Marlon", height = 1.9f, shirt = new Color(0.12f, 0.12f, 0.14f), shorts = new Color(0.75f, 0.1f, 0.12f), tag = new Color(1f, 0.55f, 0.5f), skin = new Color(0.36f, 0.22f, 0.14f) },
            // The protagonist's own body for cutscenes he isn't playing in (riding the bike).
            new Def { name = "Rider", display = "Rider", model = "MainCharacter/MainCharacter", height = 1.8f, shirt = new Color(0.1f, 0.1f, 0.12f), shorts = new Color(0.1f, 0.1f, 0.12f), nameTag = false },
            // Extras: club players, TV players and the casual court share the player and viewer models.
            Extra("ClubPlayer", "Players/ManPlayer", new Color(0.92f, 0.92f, 0.9f), new Color(0.08f, 0.33f, 0.9f)),
            Extra("ClubFar1", "Players/WomanPlayer", new Color(0.1f, 0.65f, 0.6f), new Color(0.2f, 0.2f, 0.22f)),
            Extra("ClubFar2", "Viewers/RemyViewerMan1", new Color(0.1f, 0.65f, 0.6f), new Color(0.2f, 0.2f, 0.22f)),
            Extra("TvWinner", "Players/ManPlayer", new Color(0.97f, 0.27f, 0.18f), new Color(0.08f, 0.33f, 0.9f)),
            Extra("TvPartner", "Viewers/RemyViewerMan1", new Color(0.97f, 0.27f, 0.18f), new Color(0.08f, 0.33f, 0.9f)),
            Extra("TvFar1", "Players/WomanPlayer", new Color(0.1f, 0.65f, 0.6f), new Color(0.92f, 0.92f, 0.9f)),
            Extra("TvFar2", "Viewers/MeganViewerWoman1", new Color(0.1f, 0.65f, 0.6f), new Color(0.92f, 0.92f, 0.9f)),
            Extra("Casual1", "Viewers/MeganViewerWoman1", new Color(0.9f, 0.3f, 0.25f), new Color(0.08f, 0.33f, 0.9f)),
            Extra("Casual2", "Viewers/RemyViewerMan1", new Color(0.25f, 0.5f, 0.9f), new Color(0.2f, 0.2f, 0.22f)),
            Extra("CasualFar1", "Players/WomanPlayer", new Color(0.3f, 0.75f, 0.4f), new Color(0.92f, 0.92f, 0.9f)),
            Extra("CasualFar2", "Players/ManPlayer", new Color(0.3f, 0.75f, 0.4f), new Color(0.92f, 0.92f, 0.9f)),
        };

        static Def Extra(string name, string model, Color shirt, Color shorts) =>
            new Def { name = name, display = name, model = model, shirt = shirt, shorts = shorts, nameTag = false,
                height = model.Contains("Woman") ? 1.72f : 1.84f };

        [MenuItem("Beach Volleyball/Build Story Characters")]
        public static void BuildAll()
        {
            FoundationBuilder.EnsureFolder(StoryClipDir);
            FoundationBuilder.EnsureFolder(PrefabDir);
            FoundationBuilder.EnsureFolder(CharacterDir);
            var controller = BuildController(out int clipCount);
            var mats = new FoundationBuilder.Materials();
            int rigged = 0;
            foreach (var def in Cast)
                if (BuildCharacter(def, controller, mats)) rigged++;
            Debug.Log($"[CharacterBuilder] {Cast.Length} characters ({rigged} rigged, {Cast.Length - rigged} placeholder), {clipCount} story clips.");
        }

        public static string PrefabPath(string name) => $"{PrefabDir}/{name}.prefab";

        public static RuntimeAnimatorController Controller => AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

        /// <summary>Places a cast member's prefab in the scene (scene builders call this).</summary>
        public static StoryActor Spawn(string name, Transform parent, Vector3 localPos, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name));
            if (prefab == null)
            {
                BuildAll();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name));
            }
            var go = FoundationBuilder.Instantiate(prefab, parent, localPos, Quaternion.Euler(0f, yaw, 0f));
            return go.GetComponent<StoryActor>();
        }

        // ---------- Characters ----------

        static string ModelPath(string model) => $"{CharacterDir}/{model}.fbx";

        static bool BuildCharacter(Def def, AnimatorController controller, FoundationBuilder.Materials mats)
        {
            var root = new GameObject(def.name);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);

            Animator animator = null;
            string modelPath = ModelPath(def.model);
            bool rigged = ImportHumanoid(modelPath);
            if (rigged)
            {
                var model = new GameObject("Model").transform;
                model.SetParent(body, false);
                var mesh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                mesh.transform.SetParent(model, false);
                MixamoSetup.ConfigureCharacter(mesh, controller, modelPath);
                if (OwnMaterials.TryGetValue(modelPath, out var own))
                    foreach (var r in mesh.GetComponentsInChildren<Renderer>()) r.sharedMaterial = own;
                MixamoSetup.PlaceRigged(model, mesh, def.height);
                animator = mesh.GetComponent<Animator>();
                // Head look-at needs the IK pass on the base layer (set in the controller).
            }
            else
            {
                var shirt = FoundationBuilder.Materials.Lit("Cast_" + def.name + "_Shirt", def.shirt, 0.25f);
                var shorts = FoundationBuilder.Materials.Lit("Cast_" + def.name + "_Shorts", def.shorts, 0.25f);
                var skin = def.skin.HasValue ? FoundationBuilder.Materials.Lit("Cast_" + def.name + "_Skin", def.skin.Value, 0.35f) : null;
                FoundationBuilder.BuildBody(body, 0f, shirt, shorts, mats, skin);
                body.localScale = Vector3.one * (def.height / 1.8f);
            }

            TextMesh tag = null;
            if (def.nameTag)
            {
                var tagGo = new GameObject("NameTag");
                tagGo.transform.SetParent(root.transform, false);
                tagGo.transform.localPosition = new Vector3(0f, def.height + 0.35f, 0f);
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                tag = tagGo.AddComponent<TextMesh>();
                tag.text = def.display;
                tag.font = font;
                tag.fontSize = 64;
                tag.characterSize = 0.035f;
                tag.anchor = TextAnchor.MiddleCenter;
                tag.alignment = TextAlignment.Center;
                tag.color = def.tag;
                tagGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }

            var actor = root.AddComponent<StoryActor>();
            actor.displayName = def.display;
            var so = new SerializedObject(actor);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("actingController").objectReferenceValue = controller;
            so.FindProperty("body").objectReferenceValue = rigged ? null : body;
            so.FindProperty("nameTag").objectReferenceValue = tag;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(def.name));
            Object.DestroyImmediate(root);
            return rigged;
        }

        /// <summary>Materials made for models that import without any (applied to the prefab's renderers).</summary>
        static readonly Dictionary<string, Material> OwnMaterials = new Dictionary<string, Material>();

        /// <summary>Imports a Mixamo character FBX as Humanoid with its own avatar. False if missing or invalid.</summary>
        static bool ImportHumanoid(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return false;
            if (importer.animationType != ModelImporterAnimationType.Human || importer.importAnimation)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                importer.SaveAndReimport();
            }
            var own = FixMaterial(path);
            if (own != null) OwnMaterials[path] = own;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Avatar a && a.isHuman) return true;
            Debug.LogWarning($"[CharacterBuilder] {path} has no valid humanoid avatar (Rig tab). Using the placeholder.");
            return false;
        }

        static Texture MainTexture(Material m) =>
            m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap") : m.mainTexture;

        /// <summary>
        /// AI-rigged exports often lose the texture link (Meshy names its embedded image ".fbm",
        /// OBJ-converted models point at a PNG beside them). If the model's only material has no
        /// texture, extract the embedded image (renamed to .png) or pick the PNG next to the model,
        /// and remap the material to a URP Lit material that uses it.
        /// </summary>
        public static Material FixMaterial(string fbxPath)
        {
            var materials = new List<Material>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (o is Material m) materials.Add(m);
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            string dir = Path.GetDirectoryName(fbxPath).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(fbxPath);
            string matPath = $"{dir}/{name}.mat";
            bool remapped = AssetDatabase.LoadAssetAtPath<Material>(matPath) != null;
            if (materials.Count > 1)
            {
                // Mixamo stock characters: several materials with embedded maps. Extracting the
                // images lets the importer link them to the materials by name.
                if (materials.TrueForAll(m => MainTexture(m) != null)) return null;
                if (importer.ExtractTextures($"{dir}/Textures"))
                {
                    AssetDatabase.Refresh();
                    importer.SaveAndReimport();
                }
                return null;
            }
            if (materials.Count == 1 && !remapped && MainTexture(materials[0]) != null) return null;

            // Embedded image first (fixing a non-image extension), then images next to the model.
            string texDir = $"{dir}/Textures";
            Texture2D tex = null;
            string own = $"{texDir}/{name}_BaseColor.png";
            if (!File.Exists(own) && importer.ExtractTextures(texDir) && Directory.Exists(texDir))
            {
                AssetDatabase.Refresh();
                foreach (var f in Directory.GetFiles(texDir))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".meta" || ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga") continue;
                    File.Delete(f + ".meta");
                    File.Move(f, own);
                    break;
                }
                AssetDatabase.Refresh();
            }
            if (File.Exists(own)) tex = LoadTexture(own);
            if (tex == null)
            {
                // The image the FBX itself refers to, else the only one in the folder.
                string referenced = ReferencedImage(fbxPath);
                string fbxText = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(fbxPath));
                var images = new List<string>();
                foreach (var f in Directory.GetFiles(dir))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga") images.Add(f.Replace('\\', '/'));
                }
                // Meshy conversions keep the source mesh name, which matches its texture's file name.
                string pick = images.Find(f => referenced != null && Path.GetFileName(f) == referenced)
                              ?? images.Find(f => fbxText.Contains(Path.GetFileNameWithoutExtension(f)))
                              ?? (images.Count == 1 ? images[0] : null);
                if (pick != null) tex = LoadTexture(pick);
            }
            if (tex == null) { Debug.LogWarning($"[CharacterBuilder] No texture found for {fbxPath}."); return null; }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(mat);
            if (materials.Count == 0) return mat;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), materials[0].name), mat);
            importer.SaveAndReimport();
            return null;
        }

        static Texture2D LoadTexture(string path)
        {
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureShape != TextureImporterShape.Texture2D)
            {
                ti.textureShape = TextureImporterShape.Texture2D;
                ti.SaveAndReimport();
            }
            if (AssetImporter.GetAtPath(path) is TextureImporter t2 && t2.maxTextureSize > 2048)
            {
                t2.maxTextureSize = 2048;
                t2.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>File name of the first image an FBX references (binary FBX string scan).</summary>
        static string ReferencedImage(string fbxPath)
        {
            string text = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(fbxPath));
            var match = System.Text.RegularExpressions.Regex.Match(text, @"([A-Za-z0-9_\-. ]+\.(png|jpg|jpeg|tga))", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        // ---------- The acting controller ----------

        static Dictionary<string, AnimationClip> StoryClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            if (!Directory.Exists(StoryClipDir)) return clips;
            foreach (var (role, files) in ClipFiles)
            {
                string path = null;
                foreach (var f in files)
                {
                    string candidate = $"{StoryClipDir}/{f}.fbx";
                    if (File.Exists(candidate)) { path = candidate; break; }
                }
                if (path == null) continue;
                var clip = ImportClip(path, role);
                if (clip != null) clips[role] = clip;
            }
            return clips;
        }

        /// <summary>Imports one Mixamo animation FBX (any rig, its own avatar) as a clip named after its role.</summary>
        static AnimationClip ImportClip(string path, string role)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return null;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            // Some downloads came with the character's skin; only the motion is needed.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            var defaults = importer.defaultClipAnimations;
            if (defaults.Length == 0) { Debug.LogWarning($"[CharacterBuilder] {path} has no animation."); return null; }
            var src = defaults[0];
            bool loop = System.Array.IndexOf(Looping, role) >= 0;
            bool travels = role == "Walk_Casual";
            bool sitting = role.StartsWith("Sit") || role.StartsWith("StandToSit");
            importer.clipAnimations = new[]
            {
                new ModelImporterClipAnimation
                {
                    name = role, takeName = src.takeName, firstFrame = src.firstFrame, lastFrame = src.lastFrame,
                    loopTime = loop, loopPose = loop,
                    // The actor's transform owns facing and position; keep the clip's height (sitting lowers the hips).
                    lockRootRotation = true, keepOriginalOrientation = true,
                    lockRootPositionXZ = !travels, keepOriginalPositionXZ = true,
                    // Standing clips keep the feet on the sand ("Based Upon: Feet"); clips downloaded on
                    // other Mixamo bodies otherwise float or sink a little. Sitting keeps the clip's
                    // own hip height so the seat lines up.
                    lockRootHeightY = true, keepOriginalPositionY = sitting, heightFromFeet = !sitting,
                },
            };
            importer.SaveAndReimport();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && c.name == role) return c;
            return null;
        }

        static Dictionary<string, AnimationClip> PlayerClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var name in new[] { "Idle", "Walk", "Run" })
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath($"{PlayerClipDir}/{name}.fbx"))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__")) clips[c.name] = c;
            foreach (var name in new[] { "Bump", "Set", "SpikeCock", "SpikeSwing", "Block", "Dive" })
            {
                var c = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{PlayerClipDir}/{name}.anim");
                if (c != null) clips[name] = c;
            }
            return clips;
        }

        static AnimatorController BuildController(out int storyClipCount)
        {
            var story = StoryClips();
            storyClipCount = story.Count;
            var clips = PlayerClips();
            foreach (var kv in story) clips[kv.Key] = kv.Value;

            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Sitting", AnimatorControllerParameterType.Bool);

            // ----- Base layer: locomotion, sitting, volleyball touches, full-body moments -----
            var layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            var sm = controller.layers[0].stateMachine;

            var loco = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            if (clips.TryGetValue("Idle", out var idle)) tree.AddChild(idle, 0f);
            if (clips.TryGetValue("Walk_Casual", out var walkCasual)) tree.AddChild(walkCasual, 1.6f); // = StoryActor walkSpeed: NPC walks play the pure walk cycle
            else if (clips.TryGetValue("Walk", out var walk)) tree.AddChild(walk, 1.6f);
            if (clips.TryGetValue("Run", out var run)) tree.AddChild(run, 5f);
            sm.defaultState = loco;

            // Sitting: StandToSit → SitIdle → SitToStand when the clips exist.
            if (clips.TryGetValue("SitIdle", out var sitIdle))
            {
                var sit = sm.AddState("SitIdle");
                sit.motion = sitIdle;
                AnimatorState down = null, up = null;
                if (clips.TryGetValue("StandToSit", out var s2s)) { down = sm.AddState("StandToSit"); down.motion = s2s; }
                if (clips.TryGetValue("SitToStand", out var s2u)) { up = sm.AddState("SitToStand"); up.motion = s2u; }
                Bool(loco, down ?? sit, "Sitting", true, 0.25f);
                if (down != null) Exit(down, sit, 0.2f);
                Bool(sit, up ?? loco, "Sitting", false, 0.25f);
                if (up != null) Exit(up, loco, 0.25f);
            }

            // Volleyball touches (the authored player clips) and full-body one-shots, back to locomotion.
            foreach (var name in new[] { "Bump", "Set", "Block", "Dive" })
                OneShot(controller, sm, clips, name, loco);
            foreach (var name in BaseOneShots)
                OneShot(controller, sm, clips, name, loco);
            if (clips.TryGetValue("SpikeCock", out var cock) && clips.TryGetValue("SpikeSwing", out var swing))
            {
                controller.AddParameter("Spike", AnimatorControllerParameterType.Trigger);
                var c = sm.AddState("SpikeCock");
                c.motion = cock;
                var s = sm.AddState("SpikeSwing");
                s.motion = swing;
                var t = sm.AddAnyStateTransition(c);
                t.AddCondition(AnimatorConditionMode.If, 0f, "Spike");
                t.duration = 0.1f;
                t.canTransitionToSelf = false;
                Exit(c, s, 0.08f, 0.35f / Mathf.Max(cock.length, 0.01f));
                Exit(s, loco, 0.25f);
            }
            foreach (var name in BaseHolds)
                Hold(controller, sm, clips, name, loco);

            // ----- Gesture layer: upper body only, so gestures work while walking or sitting -----
            controller.AddLayer("Gesture");
            layers = controller.layers;
            var gesture = layers[layers.Length - 1];
            gesture.defaultWeight = 1f;
            gesture.avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            controller.layers = layers;
            var gsm = gesture.stateMachine;
            var empty = gsm.AddState("Empty");
            gsm.defaultState = empty;
            foreach (var name in UpperOneShots) OneShot(controller, gsm, clips, name, empty);
            foreach (var name in UpperHolds) Hold(controller, gsm, clips, name, empty);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static void OneShot(AnimatorController controller, AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips, string name, AnimatorState back)
        {
            if (!clips.TryGetValue(name, out var clip)) return;
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            var state = sm.AddState(name);
            state.motion = clip;
            var t = sm.AddAnyStateTransition(state);
            t.AddCondition(AnimatorConditionMode.If, 0f, name);
            t.duration = 0.15f;
            t.canTransitionToSelf = false;
            Exit(state, back, 0.25f);
        }

        static void Hold(AnimatorController controller, AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips, string name, AnimatorState back)
        {
            if (!clips.TryGetValue(name, out var clip)) return;
            controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            var state = sm.AddState(name);
            state.motion = clip;
            Bool(back, state, name, true, 0.3f);
            Bool(state, back, name, false, 0.3f);
        }

        static void Bool(AnimatorState from, AnimatorState to, string param, bool value, float duration)
        {
            var t = from.AddTransition(to);
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
            t.hasExitTime = false;
            t.duration = duration;
        }

        static void Exit(AnimatorState from, AnimatorState to, float duration, float speed = 1f)
        {
            from.speed = speed;
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 0.9f;
            t.duration = duration;
        }
    }
}
