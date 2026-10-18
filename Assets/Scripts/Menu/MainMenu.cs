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
        readonly MenuNav nav = new MenuNav();

        void Awake() => document = GetComponent<UIDocument>();

        void OnEnable()
        {
            Time.timeScale = 1f;
            UiPointerGate.MenuOpen = false;
            CursorControl.Show(this);
            nav.Enable();
            Build();
            ShowOnly(title);
        }

        void OnDisable()
        {
            CursorControl.Hide(this);
            nav.Disable();
        }

        void OnDestroy() => nav.Dispose();

        void Update()
        {
            if (!SceneLoader.IsLoading) nav.Update(document.rootVisualElement);
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
            int chapter = StoryState.Chapter;
            var items = new System.Collections.Generic.List<VisualElement>
            {
                MenuStyle.Title("ACE — The Road to the Top"),
                MenuStyle.Note("An absolute beginner discovers beach volleyball. Train, make friends and rivals, and work your way up."),
            };
            if (chapter <= 1)
            {
                items.Add(MenuStyle.Heading("Chapter 1:  The First Step"));
                items.Add(MenuStyle.Button("Continue", ContinueStory, enabled: hasSave));
                items.Add(hasSave ? EraseButton("Restart Chapter 1") : MenuStyle.Button("New story", NewStory));
            }
            else if (chapter == 2)
            {
                items.Add(MenuStyle.Heading("Chapter 2:  The Sand Never Forgets"));
                bool started = StoryState.CheckpointName != "ch2_start";
                items.Add(MenuStyle.Button(started ? "Continue" : "Start Chapter 2", ContinueStory));
                if (started) items.Add(ConfirmButton("Restart Chapter 2  (keeps your skills)", "Press again: restart Chapter 2 from the beginning", ReplayChapter2));
                items.Add(EraseButton("Replay Chapter 1  (new story)"));
            }
            else if (chapter == 3)
            {
                items.Add(MenuStyle.Heading("Chapter 3:  The Price"));
                bool started = StoryState.CheckpointName != "ch3_start";
                items.Add(MenuStyle.Button(started ? "Continue" : "Start Chapter 3", ContinueStory));
                if (started) items.Add(ConfirmButton("Restart Chapter 3  (keeps your skills)", "Press again: restart Chapter 3 from the beginning", ReplayChapter3));
                items.Add(ConfirmButton("Replay Chapter 2  (keeps your skills)", "Press again: replay Chapter 2 from the beginning", ReplayChapter2));
                items.Add(EraseButton("Replay Chapter 1  (new story)"));
            }
            else
            {
                items.Add(MenuStyle.Heading("Chapter 4:  The First Defeat"));
                items.Add(MenuStyle.Button("Chapter 4  ·  coming soon", null, enabled: false));
                items.Add(ConfirmButton("Replay Chapter 3  (keeps your skills)", "Press again: replay Chapter 3 from the beginning", ReplayChapter3));
                items.Add(ConfirmButton("Replay Chapter 2  (keeps your skills)", "Press again: replay Chapter 2 from the beginning", ReplayChapter2));
                items.Add(EraseButton("Replay Chapter 1  (new story)"));
            }
            items.Add(MenuStyle.Heading("Free play"));
            items.Add(MenuStyle.Button("Practice", StartPractice));
            items.Add(MenuStyle.Button("Gym  ·  coming soon", null, enabled: false));
            items.Add(MenuStyle.Button("Tournaments  ·  coming soon", null, enabled: false));
            items.Add(MenuStyle.Button("Back", () => ShowOnly(title)));
            story = Screen(items.ToArray());

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
            nav.Reset();
        }

        /// <summary>A button that erases the save: the first press asks, the second one does it.</summary>
        static Button EraseButton(string label) =>
            ConfirmButton(label, "Press again: this erases your story save", NewStory);

        /// <summary>
        /// A button for something that loses progress: the first press only changes its text to
        /// <paramref name="confirm"/>, the second press does it.
        /// </summary>
        static Button ConfirmButton(string label, string confirm, System.Action action)
        {
            Button b = null;
            bool armed = false;
            b = MenuStyle.Button(label, () =>
            {
                if (!armed) { armed = true; b.text = confirm; return; }
                action();
            });
            return b;
        }

        static void ReplayChapter2()
        {
            GameMode.Current = GameModeKind.Story;
            StoryState.Chapter = 2;
            StoryState.Checkpoint("ch2_start");
            SceneLoader.Load(StoryDirector.HomeScene);
        }

        static void ReplayChapter3()
        {
            GameMode.Current = GameModeKind.Story;
            StoryState.Chapter = 3;
            StoryState.Checkpoint("ch3_start");
            SceneLoader.Load(StoryDirector.HomeScene);
        }

        static void NewStory()
        {
            GameMode.Current = GameModeKind.Story;
            StoryState.NewGame();
            SceneLoader.Load(StoryDirector.HomeScene);
        }

        static void ContinueStory()
        {
            GameMode.Current = GameModeKind.Story;
            StoryState.Load();
            SceneLoader.Load(StoryDirector.SceneFor(StoryState.CheckpointName));
        }

        static void StartPractice()
        {
            GameMode.Current = GameModeKind.Practice;
            SceneLoader.Load(BeachScene);
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
