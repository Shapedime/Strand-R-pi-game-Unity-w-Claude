using BeachVolley.Core;
using BeachVolley.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BeachVolley.Menu
{
    /// <summary>
    /// Title screen over the beach: Story Mode, Online, Settings, Quit.
    /// Story Mode opens the story (Chapter 1: New / Continue) plus free Practice; Online and
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

            StoryState.Load();
            bool hasSave = StoryState.HasSave && !string.IsNullOrEmpty(StoryState.CheckpointName);
            bool chapterDone = StoryState.Chapter >= 2;
            story = Screen(
                MenuStyle.Title("ACE — The Road to the Top"),
                MenuStyle.Note("An absolute beginner discovers beach volleyball. Train, make friends and rivals, and work your way up."),
                MenuStyle.Heading("Chapter 1:  The First Step"),
                chapterDone
                    ? MenuStyle.Button("Chapter 2  ·  coming soon", null, enabled: false)
                    : MenuStyle.Button("Continue", ContinueStory, enabled: hasSave),
                MenuStyle.Button(chapterDone ? "Replay Chapter 1" : hasSave ? "Restart Chapter 1" : "New story", NewStory),
                MenuStyle.Heading("Free play"),
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

        static void NewStory()
        {
            GameMode.Current = GameModeKind.Story;
            StoryState.NewGame();
            SceneManager.LoadScene(StoryDirector.HomeScene);
        }

        static void ContinueStory()
        {
            GameMode.Current = GameModeKind.Story;
            StoryState.Load();
            SceneManager.LoadScene(StoryDirector.SceneFor(StoryState.CheckpointName));
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
