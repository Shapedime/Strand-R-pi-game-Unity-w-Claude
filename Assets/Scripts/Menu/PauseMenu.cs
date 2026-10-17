using BeachVolley.Core;
using BeachVolley.Practice;
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
        VisualElement screen;
        InputAction pauseAction;

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
            Build();
            SetOpen(false);
        }

        void OnDisable()
        {
            pauseAction.Disable();
            if (IsOpen) Time.timeScale = 1f;
        }

        void OnDestroy() => pauseAction.Dispose();

        void Update()
        {
            if (!pauseAction.WasPressedThisFrame()) return;
            if (practiceMenu != null && practiceMenu.IsOpen) practiceMenu.SetOpen(false);
            else SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (screen != null) MenuStyle.Show(screen, open);
            Time.timeScale = open ? 0f : 1f;
            UiPointerGate.MenuOpen = open;
            if (open)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
            }
        }

        void Build()
        {
            var root = document.rootVisualElement;
            root.Clear();
            screen = MenuStyle.Screen(dim: true);
            var panel = MenuStyle.Panel(380);
            panel.Add(MenuStyle.Title("Paused"));
            panel.Add(MenuStyle.Button("Resume", () => SetOpen(false)));
            panel.Add(MenuStyle.Button("Practice options", () =>
            {
                SetOpen(false);
                if (practiceMenu != null) practiceMenu.SetOpen(true);
            }, enabled: practiceMenu != null));
            panel.Add(MenuStyle.Button("Main menu", () =>
            {
                SetOpen(false);
                SceneManager.LoadScene(MainMenuScene);
            }));
            panel.Add(MenuStyle.Note("Click the game to take control again after resuming."));
            screen.Add(panel);
            root.Add(screen);
        }
    }
}
