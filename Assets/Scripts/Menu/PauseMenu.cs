using BeachVolley.Core;
using BeachVolley.Practice;
using BeachVolley.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BeachVolley.Menu
{
    /// <summary>
    /// Esc (or gamepad Start) pauses: Resume, Practice options, Main Menu. Time stops while it's
    /// open. Esc with the practice options open just closes them.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PauseMenu : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";

        [SerializeField] PracticeMenu practiceMenu;

        UIDocument document;
        VisualElement screen, skillsScreen;
        InputAction pauseAction;
        readonly MenuNav nav = new MenuNav();
        bool cursorShown;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            document = GetComponent<UIDocument>();
            if (practiceMenu == null) practiceMenu = FindAnyObjectByType<PracticeMenu>();
            pauseAction = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");
        }

        void OnEnable()
        {
            pauseAction.Enable();
            nav.Enable();
            Build();
            SetOpen(false);
        }

        void OnDisable()
        {
            pauseAction.Disable();
            nav.Disable();
            if (IsOpen) Time.timeScale = 1f;
            if (cursorShown) { CursorControl.Hide(this); cursorShown = false; }
        }

        void OnDestroy() { pauseAction.Dispose(); nav.Dispose(); }

        void Update()
        {
            if (IsOpen && !pauseAction.WasPressedThisFrame())
            {
                bool skills = skillsScreen != null && skillsScreen.style.display == DisplayStyle.Flex;
                nav.Update(skills ? skillsScreen : screen);
            }
            if (!pauseAction.WasPressedThisFrame()) return;
            if (practiceMenu != null && practiceMenu.IsOpen) practiceMenu.SetOpen(false);
            else if (skillsScreen != null && skillsScreen.style.display == DisplayStyle.Flex) ShowSkills(false);
            else SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (screen != null) MenuStyle.Show(screen, open);
            if (!open && skillsScreen != null) MenuStyle.Show(skillsScreen, false);
            Time.timeScale = open ? 0f : 1f;
            UiPointerGate.MenuOpen = open;
            if (open != cursorShown)
            {
                if (open) CursorControl.Show(this); else CursorControl.Hide(this);
                cursorShown = open;
            }
            nav.Reset();
        }

        void Build()
        {
            var root = document.rootVisualElement;
            root.Clear();
            screen = MenuStyle.Screen(dim: true);
            var panel = MenuStyle.Panel(380);
            panel.Add(MenuStyle.Title("Paused"));
            panel.Add(MenuStyle.Button("Resume", () => SetOpen(false)));
            panel.Add(MenuStyle.Button("Your skills", () => ShowSkills(true), enabled: FindAnyObjectByType<PlayerSkills>() != null));
            panel.Add(MenuStyle.Button("Practice options", () =>
            {
                SetOpen(false);
                if (practiceMenu != null) practiceMenu.SetOpen(true);
            }, enabled: practiceMenu != null));
            panel.Add(MenuStyle.Button("Main menu", () =>
            {
                SetOpen(false);
                SceneLoader.Load(MainMenuScene);
            }));
            panel.Add(MenuStyle.Note("Click the game to take control again after resuming."));
            screen.Add(panel);
            root.Add(screen);
            skillsScreen = MenuStyle.Screen(dim: true);
            MenuStyle.Show(skillsScreen, false);
            root.Add(skillsScreen);
        }

        /// <summary>Read-only skill levels (points are spent at home in story mode).</summary>
        void ShowSkills(bool show)
        {
            MenuStyle.Show(screen, !show && IsOpen);
            MenuStyle.Show(skillsScreen, show);
            nav.Reset();
            if (!show) return;
            skillsScreen.Clear();
            var skills = FindAnyObjectByType<PlayerSkills>();
            var panel = MenuStyle.Panel(520);
            panel.Add(MenuStyle.Title("Your skills"));
            if (skills != null)
            {
                foreach (var type in SkillInfo.DisplayOrder)
                {
                    int lv = skills.Get(type);
                    var row = new Label($"{SkillInfo.Name(type)}   <b>{lv}</b>   <color=#5ab0e0>{new string('■', lv)}</color><color=#3a4550>{new string('■', PlayerSkills.MaxLevel - lv)}</color>");
                    row.enableRichText = true;
                    row.style.fontSize = 19;
                    row.style.color = MenuStyle.TextColor;
                    row.style.marginTop = 3;
                    panel.Add(row);
                }
                panel.Add(MenuStyle.Heading("Gym"));
                foreach (var type in SkillInfo.GymOrder)
                {
                    int lv = skills.Get(type);
                    var row = new Label($"{SkillInfo.Name(type)}   <b>{lv}</b>   <color=#e0a040>{new string('■', lv)}</color><color=#3a4550>{new string('■', PlayerSkills.MaxLevel - lv)}</color>");
                    row.enableRichText = true;
                    row.style.fontSize = 19;
                    row.style.color = MenuStyle.TextColor;
                    row.style.marginTop = 3;
                    panel.Add(row);
                }
                panel.Add(MenuStyle.Note($"Unspent development points: {skills.DevPoints}, gym points: {skills.GymPoints}. In story mode you spend them at home."));
            }
            panel.Add(MenuStyle.Button("Back", () => ShowSkills(false)));
            skillsScreen.Add(panel);
        }
    }
}
