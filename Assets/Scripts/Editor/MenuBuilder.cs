using System.IO;
using BeachVolley.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeachVolley.EditorTools
{
    /// <summary>
    /// Builds the MainMenu scene (beach backdrop, orbiting camera, UI Toolkit menu) and the shared
    /// UI assets (panel settings + runtime theme). Also owns the build scene list: MainMenu first,
    /// then the Beach court. Run Build Foundation first so the court prefabs exist.
    /// </summary>
    public static class MenuBuilder
    {
        public const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string UiDir = "Assets/UI";
        const string ThemePath = UiDir + "/MenuTheme.tss";
        const string PanelPath = UiDir + "/MenuPanel.asset";

        [MenuItem("Beach Volleyball/Build Menu")]
        public static void BuildMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var world = new GameObject("World").transform;
            FoundationBuilder.BuildBackdrop(world);

            var cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.gameObject.AddComponent<MenuCameraOrbit>();
            cam.transform.position = new Vector3(-6f, 5.7f, -14f);
            cam.transform.LookAt(new Vector3(0f, 1.2f, 0f));

            // Fetched only now: building the backdrop saves and re-imports material/texture assets,
            // which would leave an earlier PanelSettings reference stale (saved as empty).
            var ui = new GameObject("MainMenu");
            var doc = ui.AddComponent<UIDocument>();
            doc.panelSettings = MenuPanelSettings();
            ui.AddComponent<MainMenu>();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            ApplyBuildScenes();
            Debug.Log("[MenuBuilder] Main menu built into " + MenuScenePath);
        }

        /// <summary>Main menu first (the game starts there), then the Beach court and the story scenes.</summary>
        public static void ApplyBuildScenes()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            if (File.Exists(MenuScenePath)) scenes.Add(new EditorBuildSettingsScene(MenuScenePath, true));
            if (File.Exists(FoundationBuilder.ScenePath)) scenes.Add(new EditorBuildSettingsScene(FoundationBuilder.ScenePath, true));
            if (File.Exists(StoryBuilder.HomeScenePath)) scenes.Add(new EditorBuildSettingsScene(StoryBuilder.HomeScenePath, true));
            if (File.Exists(StoryBuilder.ClubScenePath)) scenes.Add(new EditorBuildSettingsScene(StoryBuilder.ClubScenePath, true));
            if (File.Exists(StoryBuilder.TownScenePath)) scenes.Add(new EditorBuildSettingsScene(StoryBuilder.TownScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Shared panel settings for every menu: scales with the screen, default runtime theme.</summary>
        public static PanelSettings MenuPanelSettings()
        {
            if (!AssetDatabase.IsValidFolder(UiDir)) AssetDatabase.CreateFolder("Assets", "UI");
            if (!File.Exists(ThemePath))
            {
                // The default runtime theme gives UI Toolkit its fonts and base control styles.
                File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath);
            }
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            return panel;
        }
    }
}
