using BeachVolley.Core;
using BeachVolley.Menu;
using BeachVolley.Skills;
using BeachVolley.Story;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BeachVolley.Practice
{
    /// <summary>
    /// Practice options on Tab: ball machine mode and pace, pause the machine, target zones on/off.
    /// The game keeps running; while open the cursor is free and gameplay input is ignored.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PracticeMenu : MonoBehaviour
    {
        [SerializeField] BallMachine machine;
        [SerializeField] TargetZones zones;

        UIDocument document;
        VisualElement screen;
        InputAction toggleAction;
        Button[] modeButtons;
        Button[] paceButtons;
        Button pauseButton;
        Button zonesButton;

        public bool IsOpen { get; private set; }
        bool cursorShown;

        static readonly (MachineMode mode, string label)[] Modes =
        {
            (MachineMode.Off, "Off (free play)"),
            (MachineMode.FreeBalls, "Free balls"),
            (MachineMode.Serves, "Serves"),
            (MachineMode.SetsToSpike, "Sets to spike"),
            (MachineMode.Rally, "Rally"),
        };

        void Awake()
        {
            document = GetComponent<UIDocument>();
            if (machine == null) machine = FindAnyObjectByType<BallMachine>();
            if (zones == null) zones = FindAnyObjectByType<TargetZones>();
            toggleAction = new InputAction("PracticeMenu", InputActionType.Button, "<Keyboard>/tab");
            toggleAction.AddBinding("<Gamepad>/dpad/up");
        }

        void OnEnable()
        {
            toggleAction.Enable();
            Build();
            SetOpen(false);
        }

        void Start()
        {
            // Practice with your story player: the levels you built carry over (spending stays a story thing).
            if (StoryState.HasSave) StoryState.ApplySkills(FindAnyObjectByType<PlayerSkills>());
        }

        void OnDisable()
        {
            toggleAction.Disable();
            if (cursorShown) { CursorControl.Hide(this); cursorShown = false; }
        }
        void OnDestroy() => toggleAction.Dispose();

        void Update()
        {
            if (toggleAction.WasPressedThisFrame() && Time.timeScale > 0f) SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (screen != null) MenuStyle.Show(screen, open);
            UiPointerGate.MenuOpen = open;
            if (open != cursorShown)
            {
                if (open) CursorControl.Show(this); else CursorControl.Hide(this);
                cursorShown = open;
            }
            if (open) Refresh();
        }

        void Build()
        {
            var root = document.rootVisualElement;
            root.Clear();
            screen = MenuStyle.Screen(dim: false, horizontal: Justify.FlexEnd);
            screen.style.paddingRight = 30;
            var panel = MenuStyle.Panel(360);
            panel.Add(MenuStyle.Title("Practice", 28));

            panel.Add(MenuStyle.Heading("Ball machine"));
            modeButtons = new Button[Modes.Length];
            for (int i = 0; i < Modes.Length; i++)
            {
                var mode = Modes[i].mode;
                modeButtons[i] = MenuStyle.Button(Modes[i].label, () => { machine.Mode = mode; Refresh(); }, fontSize: 17);
                panel.Add(modeButtons[i]);
            }

            panel.Add(MenuStyle.Heading("Pace"));
            paceButtons = new[]
            {
                MenuStyle.Button("Slow", () => { machine.Pace = MachinePace.Slow; Refresh(); }, fontSize: 16),
                MenuStyle.Button("Normal", () => { machine.Pace = MachinePace.Normal; Refresh(); }, fontSize: 16),
                MenuStyle.Button("Fast", () => { machine.Pace = MachinePace.Fast; Refresh(); }, fontSize: 16),
            };
            panel.Add(MenuStyle.Row(paceButtons));

            pauseButton = MenuStyle.Button("", () => { machine.Paused = !machine.Paused; Refresh(); }, fontSize: 17);
            zonesButton = MenuStyle.Button("", () => { zones.ZonesOn = !zones.ZonesOn; Refresh(); }, fontSize: 17);
            panel.Add(MenuStyle.Heading("Extras"));
            panel.Add(pauseButton);
            panel.Add(zonesButton);
            panel.Add(MenuStyle.Note("Tab to close · R puts the ball in front of you"));
            panel.Add(MenuStyle.Button("Close", () => SetOpen(false), fontSize: 17));

            screen.Add(panel);
            root.Add(screen);
        }

        void Refresh()
        {
            if (machine == null) return;
            for (int i = 0; i < Modes.Length; i++) MenuStyle.Select(modeButtons[i], machine.Mode == Modes[i].mode);
            for (int i = 0; i < paceButtons.Length; i++) MenuStyle.Select(paceButtons[i], (int)machine.Pace == i);
            pauseButton.text = machine.Paused ? "Machine paused: resume" : "Pause machine";
            MenuStyle.Select(pauseButton, machine.Paused);
            if (zones != null)
            {
                zonesButton.text = zones.ZonesOn ? "Target zones: on" : "Target zones: off";
                MenuStyle.Select(zonesButton, zones.ZonesOn);
            }
        }
    }
}
