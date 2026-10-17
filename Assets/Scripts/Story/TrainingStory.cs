using System.Collections.Generic;
using BeachVolley.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeachVolley.Story
{
    /// <summary>
    /// Light story framing on the training court: a short rookie intro before the first
    /// practice, a coach line when a drill unlocks, and one beat after a few practices
    /// when the player starts to enjoy it. Linear lines only, no branching.
    /// </summary>
    public class TrainingStory : MonoBehaviour
    {
        [System.Serializable]
        public struct Line
        {
            [Tooltip("Empty = narration.")] public string speaker;
            [TextArea] public string text;
        }

        [SerializeField] string coachName = "Coach Mira";
        [SerializeField] bool playIntroOnStart = true;

        [SerializeField] Line[] intro =
        {
            new Line { text = "A flyer at the bus stop: CITY BEACH VOLLEYBALL - BEGINNER TRAINING. ALL LEVELS WELCOME." },
            new Line { text = "You've never really played. You can barely keep a ball in the air. You came anyway." },
            new Line { speaker = "Coach", text = "New face! Don't worry about being bad. Everyone on this sand started at zero." },
            new Line { speaker = "Coach", text = "We practise, you get a little better each time. That's the whole secret. Let's start with footwork." },
        };

        [Tooltip("Practices completed before the 'starting to enjoy it' beat plays.")]
        [SerializeField] int enjoymentBeatAfter = 3;
        [SerializeField] Line[] enjoymentBeat =
        {
            new Line { text = "Somewhere between the sand in your shoes and the coach's whistle, you notice you're smiling." },
            new Line { speaker = "Coach", text = "See that? You're starting to enjoy this. That's when it really starts to click." },
        };

        [SerializeField] Line passingUnlocked = new Line { speaker = "Coach", text = "Your feet are waking up. Next time, I'll toss you some balls - try the Passing drill." };
        [SerializeField] Line serveUnlocked = new Line { speaker = "Coach", text = "Ready for something new? Toss it up and send it over the net. The Serve drill is open." };

        readonly Queue<Line> queue = new Queue<Line>();
        Line? current;
        int advancedFrame = -1;
        InputAction advanceAction;
        GUIStyle nameStyle, textStyle;

        /// <summary>True while a line is on screen (training waits for it).</summary>
        public bool IsShowing => current.HasValue;
        /// <summary>True on the frame a line was dismissed, so the same key press doesn't also start practice.</summary>
        public bool AdvancedThisFrame => advancedFrame == Time.frameCount;

        void Awake()
        {
            advanceAction = new InputAction("StoryAdvance", InputActionType.Button, "<Keyboard>/enter");
            advanceAction.AddBinding("<Keyboard>/space");
            advanceAction.AddBinding("<Keyboard>/numpadEnter");
            advanceAction.AddBinding("<Gamepad>/buttonSouth");
        }

        void OnEnable() => advanceAction.Enable();
        void OnDisable() => advanceAction.Disable();
        void OnDestroy() => advanceAction.Dispose();

        void Start()
        {
            if (playIntroOnStart) Enqueue(intro);
        }

        void Update()
        {
            if (current.HasValue && advanceAction.WasPressedThisFrame())
                Advance();
        }

        public void Enqueue(IEnumerable<Line> lines)
        {
            foreach (var l in lines) queue.Enqueue(l);
            if (!current.HasValue) Next();
        }

        public void Say(Line line) => Enqueue(new[] { line });

        /// <summary>Called by training after each finished practice.</summary>
        public void OnPracticeCompleted(int completedPractices)
        {
            if (completedPractices == enjoymentBeatAfter) Enqueue(enjoymentBeat);
        }

        public void OnPassingUnlocked() => Say(passingUnlocked);
        public void OnServeUnlocked() => Say(serveUnlocked);

        void Advance()
        {
            advancedFrame = Time.frameCount;
            Next();
        }

        void Next() => current = queue.Count > 0 ? queue.Dequeue() : (Line?)null;

        void OnGUI()
        {
            if (!current.HasValue) return;
            nameStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.45f) } };
            textStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, richText = true };

            float width = Mathf.Min(720f, Screen.width - 40f);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 170f, width, 150f);
            UiPointerGate.Block(rect);
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);

            var line = current.Value;
            bool narration = string.IsNullOrEmpty(line.speaker);
            string speaker = line.speaker == "Coach" ? coachName : line.speaker;
            if (!narration)
                GUI.Label(new Rect(rect.x + 16f, rect.y + 10f, width - 32f, 22f), speaker, nameStyle);

            var oldColor = GUI.color;
            if (narration) GUI.color = new Color(0.85f, 0.9f, 1f);
            GUI.Label(new Rect(rect.x + 16f, rect.y + (narration ? 16f : 36f), width - 32f, 70f),
                narration ? $"<i>{line.text}</i>" : line.text, textStyle);
            GUI.color = oldColor;

            if (GUI.Button(new Rect(rect.xMax - 170f, rect.yMax - 42f, 154f, 32f), "Continue  (Enter)"))
                Advance();
        }
    }
}
