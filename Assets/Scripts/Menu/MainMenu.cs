using BeachVolley.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BeachVolley.Menu
{
    /// <summary>
    /// Title screen over the beach: Story Mode, Online, Settings, Quit.
    /// Story Mode opens the career hub (Practice now; Gym and Tournaments coming); Online and
    /// Settings are placeholders until those features exist.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenu : MonoBehaviour
    {
        public const string BeachScene = "Beach";

        UIDocument document;
        VisualElement title, story, online, settings;

        void Awake() => document = GetComponent<UIDocument>();

        void OnEnable()
        {
            Time.timeScale = 1f;
            UiPointerGate.MenuOpen = false;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            Build();
            ShowOnly(title);
        }

        void Build()
        {
            var root = document.rootVisualElement;
            root.Clear();

            title = Screen(
                MenuStyle.Title("BEACH VOLLEY", 54),
                MenuStyle.Note("From city courts to the big tournaments."),
                MenuStyle.Button("Story Mode", () => ShowOnly(story)),
                MenuStyle.Button("Online", () => ShowOnly(online)),
                MenuStyle.Button("Settings", () => ShowOnly(settings)),
                MenuStyle.Button("Quit", Quit));

            story = Screen(
                MenuStyle.Title("Story Mode"),
                MenuStyle.Note("Train, get stronger, play tournaments. Push too hard and an injury will set you back."),
                MenuStyle.Button("Practice", StartPractice),
                MenuStyle.Button("Gym  ·  coming soon", null, enabled: false),
                MenuStyle.Button("Tournaments  ·  coming soon", null, enabled: false),
                MenuStyle.Button("Back", () => ShowOnly(title)));

            online = Screen(
                MenuStyle.Title("Online"),
                MenuStyle.Note("Play with your friends: coming soon."),
                MenuStyle.Note("Your story player comes with you (skills, gear, even an injury), so training pays off. A ranked mode will use equal stats so only your hands decide."),
                MenuStyle.Button("Play with friends  ·  coming soon", null, enabled: false),
                MenuStyle.Button("Back", () => ShowOnly(title)));

            settings = Screen(
                MenuStyle.Title("Settings"),
                MenuStyle.Note("Controls, graphics and audio settings will live here."),
                MenuStyle.Button("Back", () => ShowOnly(title)));

            root.Add(title);
            root.Add(story);
            root.Add(online);
            root.Add(settings);
        }

        static VisualElement Screen(params VisualElement[] items)
        {
            var screen = MenuStyle.Screen(dim: false);
            var panel = MenuStyle.Panel(560);
            foreach (var item in items) panel.Add(item);
            screen.Add(panel);
            return screen;
        }

        void ShowOnly(VisualElement visible)
        {
            foreach (var s in new[] { title, story, online, settings })
                MenuStyle.Show(s, s == visible);
        }

        static void StartPractice()
        {
            GameMode.Current = GameModeKind.Practice;
            SceneManager.LoadScene(BeachScene);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
