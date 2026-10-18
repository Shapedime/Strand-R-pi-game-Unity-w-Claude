using System;
using System.Collections;
using System.Collections.Generic;
using BeachVolley.Core;
using BeachVolley.Menu;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BeachVolley.Story
{
    /// <summary>
    /// Everything the story draws on screen, in one UI Toolkit document: the dialogue box (with a
    /// typewriter and a thought style), choice buttons, the phone, fades, title cards, the
    /// interaction prompt, a drill objective and short toasts. The coroutines here are what
    /// <see cref="StoryDirector"/> scripts yield on.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public partial class StoryUI : MonoBehaviour
    {
        [SerializeField] float charsPerSecond = 48f;

        static readonly Color ThoughtColor = new Color(0.72f, 0.86f, 1f);
        static readonly Color PhoneColor = new Color(0.1f, 0.1f, 0.12f, 0.97f);
        static readonly Color BubbleMine = new Color(0.16f, 0.52f, 0.95f);
        static readonly Color BubbleTheirs = new Color(0.25f, 0.27f, 0.3f);

        UIDocument document;
        VisualElement fade, card, dialogue, choices, phone, phoneContent, phoneButtons;
        Label cardTitle, cardSubtitle, speakerLabel, textLabel, continueLabel, phoneHeader, promptLabel, objectiveLabel, toastLabel;
        InputAction advanceAction, upAction, downAction, confirmAction;
        readonly List<Button> choiceButtons = new List<Button>();
        int choiceIndex;
        int chosen = -1;
        int openedFrame;
        float toastUntil;
        int blockers;

        /// <summary>True while dialogue, a choice or the phone holds the player still.</summary>
        public bool IsBusy => blockers > 0;

        void Awake()
        {
            document = GetComponent<UIDocument>();
            advanceAction = new InputAction("Advance", InputActionType.Button, "<Keyboard>/enter");
            advanceAction.AddBinding("<Keyboard>/space");
            advanceAction.AddBinding("<Keyboard>/numpadEnter");
            advanceAction.AddBinding("<Keyboard>/e");
            advanceAction.AddBinding("<Gamepad>/buttonSouth");
            advanceAction.AddBinding("<Mouse>/leftButton");
            upAction = new InputAction("Up", InputActionType.Button, "<Keyboard>/upArrow");
            upAction.AddBinding("<Keyboard>/w");
            upAction.AddBinding("<Gamepad>/dpad/up");
            downAction = new InputAction("Down", InputActionType.Button, "<Keyboard>/downArrow");
            downAction.AddBinding("<Keyboard>/s");
            downAction.AddBinding("<Gamepad>/dpad/down");
            confirmAction = new InputAction("Confirm", InputActionType.Button, "<Keyboard>/enter");
            confirmAction.AddBinding("<Keyboard>/space");
            confirmAction.AddBinding("<Keyboard>/numpadEnter");
            confirmAction.AddBinding("<Gamepad>/buttonSouth");
        }

        InputAction[] Actions => new[] { advanceAction, upAction, downAction, confirmAction };

        void OnEnable()
        {
            foreach (var a in Actions) a.Enable();
            Build();
        }

        void OnDisable() { foreach (var a in Actions) a.Disable(); }
        void OnDestroy() { foreach (var a in Actions) a.Dispose(); DisposeExtras(); }

        void Update()
        {
            // The pause menu clears MenuOpen when it closes; keep gameplay held while a box is up.
            if (blockers > 0 && !UiPointerGate.MenuOpen && !Paused) UiPointerGate.MenuOpen = true;
            if (toastLabel != null && toastLabel.style.display == DisplayStyle.Flex && Time.time > toastUntil)
                MenuStyle.Show(toastLabel, false);
            UpdateExtras();
        }

        // ---------- Layout ----------

        void Build()
        {
            var root = document.rootVisualElement;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;

            objectiveLabel = Text("", 22, MenuStyle.TextColor, bold: true);
            objectiveLabel.style.position = Position.Absolute;
            objectiveLabel.style.left = 40;
            objectiveLabel.style.top = 32;
            objectiveLabel.style.backgroundColor = MenuStyle.PanelColor;
            Pad(objectiveLabel, 16, 10);
            Round(objectiveLabel, 10);
            root.Add(objectiveLabel);

            toastLabel = Text("", 34, new Color(1f, 0.85f, 0.3f), bold: true);
            toastLabel.style.position = Position.Absolute;
            toastLabel.style.left = toastLabel.style.right = 0;
            toastLabel.style.top = 140;
            toastLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            root.Add(toastLabel);

            promptLabel = Text("", 22, MenuStyle.TextColor, bold: true);
            promptLabel.style.position = Position.Absolute;
            promptLabel.style.alignSelf = Align.Center;
            promptLabel.style.bottom = 300;
            promptLabel.style.left = new Length(50, LengthUnit.Percent);
            promptLabel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            promptLabel.style.backgroundColor = MenuStyle.PanelColor;
            Pad(promptLabel, 18, 10);
            Round(promptLabel, 10);
            root.Add(promptLabel);

            // Dialogue box along the bottom.
            dialogue = new VisualElement();
            dialogue.style.position = Position.Absolute;
            dialogue.style.left = 220;
            dialogue.style.right = 220;
            dialogue.style.bottom = 40;
            dialogue.style.minHeight = 170;
            dialogue.style.backgroundColor = MenuStyle.PanelColor;
            Pad(dialogue, 34, 22);
            Round(dialogue, 14);
            speakerLabel = Text("", 24, MenuStyle.SelectedColor, bold: true);
            speakerLabel.style.marginBottom = 8;
            textLabel = Text("", 26, MenuStyle.TextColor);
            textLabel.style.whiteSpace = WhiteSpace.Normal;
            continueLabel = Text("▼  Space / Enter", 15, MenuStyle.NoteColor);
            continueLabel.style.position = Position.Absolute;
            continueLabel.style.right = 26;
            continueLabel.style.bottom = 14;
            dialogue.Add(speakerLabel);
            dialogue.Add(textLabel);
            dialogue.Add(continueLabel);
            root.Add(dialogue);

            // Choices stack above the dialogue box.
            choices = new VisualElement();
            choices.style.position = Position.Absolute;
            choices.style.right = 220;
            choices.style.bottom = 230;
            choices.style.width = 760;
            choices.style.flexDirection = FlexDirection.Column;
            root.Add(choices);

            // Phone on the right.
            phone = new VisualElement();
            phone.style.position = Position.Absolute;
            phone.style.right = 160;
            phone.style.top = 70;
            phone.style.width = 440;
            phone.style.height = 760;
            phone.style.backgroundColor = PhoneColor;
            Pad(phone, 22, 26);
            Round(phone, 42);
            phone.style.borderTopWidth = phone.style.borderBottomWidth = phone.style.borderLeftWidth = phone.style.borderRightWidth = 6;
            phone.style.borderTopColor = phone.style.borderBottomColor = phone.style.borderLeftColor = phone.style.borderRightColor = new Color(0.3f, 0.32f, 0.36f);
            phoneHeader = Text("", 20, MenuStyle.TextColor, bold: true);
            phoneHeader.style.unityTextAlign = TextAnchor.MiddleCenter;
            phoneHeader.style.marginBottom = 14;
            phoneContent = new VisualElement();
            phoneContent.style.flexGrow = 1;
            phoneContent.style.flexDirection = FlexDirection.Column;
            phoneButtons = new VisualElement();
            phoneButtons.style.flexDirection = FlexDirection.Column;
            phone.Add(phoneHeader);
            phone.Add(phoneContent);
            phone.Add(phoneButtons);
            root.Add(phone);

            card = MenuStyle.Screen(dim: false);
            card.style.flexDirection = FlexDirection.Column;
            card.style.justifyContent = Justify.Center;
            card.pickingMode = PickingMode.Ignore;
            cardTitle = Text("", 54, MenuStyle.TextColor, bold: true);
            cardTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            cardSubtitle = Text("", 26, MenuStyle.NoteColor);
            cardSubtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            cardSubtitle.style.whiteSpace = WhiteSpace.Normal;
            cardSubtitle.style.maxWidth = 1100;
            cardSubtitle.style.marginTop = 18;
            card.Add(cardTitle);
            card.Add(cardSubtitle);

            BuildExtras(root);

            fade = MenuStyle.Screen(dim: false);
            fade.style.backgroundColor = Color.black;
            fade.style.opacity = 0f;
            fade.pickingMode = PickingMode.Ignore;
            // The card sits above the fade so it can show over black.
            root.Add(fade);
            root.Add(card);
            AddOverlays(root);

            foreach (var e in new VisualElement[] { objectiveLabel, toastLabel, promptLabel, dialogue, choices, phone, card })
                MenuStyle.Show(e, false);
        }

        internal static Label Text(string text, int size, Color color, bool bold = false)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        internal static void Pad(VisualElement e, float h, float v)
        {
            e.style.paddingLeft = e.style.paddingRight = h;
            e.style.paddingTop = e.style.paddingBottom = v;
        }

        internal static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        // ---------- Blocking (freezes the player, frees the cursor) ----------

        internal void Block()
        {
            if (blockers++ == 0)
            {
                // The cursor stays hidden: dialogue, choices and the phone all work with the keys.
                UiPointerGate.MenuOpen = true;
            }
        }

        internal void Unblock()
        {
            blockers = Mathf.Max(0, blockers - 1);
            if (blockers == 0) UiPointerGate.MenuOpen = false;
        }

        bool Paused => Time.timeScale == 0f;

        bool AdvancePressed() => !Paused && Time.frameCount > openedFrame && advanceAction.WasPressedThisFrame();

        // ---------- Dialogue ----------

        public IEnumerator Say(string speaker, string text) => Line(speaker, text, false);

        public IEnumerator Think(string text) => Line("", "“" + text + "”", true);

        /// <summary>Narration: no speaker, plain style.</summary>
        public IEnumerator Narrate(string text) => Line("", text, false);

        IEnumerator Line(string speaker, string text, bool thought)
        {
            // A missing line (e.g. a used-up BarkPool) must never freeze the story.
            if (string.IsNullOrEmpty(text)) yield break;
            Block();
            MenuStyle.Show(dialogue, true);
            MenuStyle.Show(speakerLabel, !string.IsNullOrEmpty(speaker));
            speakerLabel.text = speaker;
            textLabel.style.unityFontStyleAndWeight = thought ? FontStyle.Italic : FontStyle.Normal;
            textLabel.style.color = thought ? ThoughtColor : MenuStyle.TextColor;
            openedFrame = Time.frameCount;

            // Typewriter; a press shows the rest at once.
            float shown = 0f;
            textLabel.text = "";
            MenuStyle.Show(continueLabel, false);
            while (shown < text.Length)
            {
                if (AdvancePressed()) break;
                shown += Time.deltaTime * charsPerSecond;
                textLabel.text = text.Substring(0, Mathf.Min(text.Length, (int)shown));
                yield return null;
            }
            textLabel.text = text;
            MenuStyle.Show(continueLabel, true);
            yield return null;
            while (!AdvancePressed()) yield return null;

            MenuStyle.Show(dialogue, false);
            Unblock();
            // Let the press that closed the box finish before anything else reads it.
            yield return null;
        }

        // ---------- Choices ----------

        /// <summary>Index of the option picked by the last <see cref="Choose"/> / <see cref="PhoneChoose"/>.</summary>
        public int LastChoice { get; private set; }

        public IEnumerator Choose(params string[] options) => ChooseIn(choices, options, 22);

        IEnumerator ChooseIn(VisualElement container, string[] options, int fontSize)
        {
            Block();
            // A menu: the pointer shows so the options can be clicked (the keys work too).
            CursorControl.Show(this);
            container.Clear();
            choiceButtons.Clear();
            chosen = -1;
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var b = MenuStyle.Button(options[i], () => chosen = index, fontSize: fontSize);
                b.style.unityTextAlign = TextAnchor.MiddleLeft;
                b.style.paddingLeft = 18;
                b.style.whiteSpace = WhiteSpace.Normal;
                b.style.height = StyleKeyword.Auto;
                b.style.minHeight = fontSize * 2.2f;
                b.RegisterCallback<MouseEnterEvent>(_ => Highlight(index));
                container.Add(b);
                choiceButtons.Add(b);
            }
            MenuStyle.Show(container, true);
            Highlight(0);
            openedFrame = Time.frameCount;
            yield return null;

            while (chosen < 0)
            {
                if (!Paused)
                {
                    if (upAction.WasPressedThisFrame()) Highlight((choiceIndex + options.Length - 1) % options.Length);
                    if (downAction.WasPressedThisFrame()) Highlight((choiceIndex + 1) % options.Length);
                    if (confirmAction.WasPressedThisFrame() && Time.frameCount > openedFrame) chosen = choiceIndex;
                    var kb = Keyboard.current;
                    if (kb != null)
                        for (int i = 0; i < options.Length && i < 9; i++)
                            if (kb[Key.Digit1 + i].wasPressedThisFrame) chosen = i;
                }
                yield return null;
            }

            LastChoice = chosen;
            if (container == choices) MenuStyle.Show(container, false);
            container.Clear();
            choiceButtons.Clear();
            CursorControl.Hide(this);
            Unblock();
            yield return null;
        }

        void Highlight(int index)
        {
            choiceIndex = index;
            for (int i = 0; i < choiceButtons.Count; i++) MenuStyle.Select(choiceButtons[i], i == index);
        }

        // ---------- Phone ----------

        public void ShowPhone(bool show, string header = "")
        {
            if (show && phone.style.display != DisplayStyle.Flex) Block();
            else if (!show && phone.style.display == DisplayStyle.Flex) Unblock();
            MenuStyle.Show(phone, show);
            phoneHeader.text = header;
            phoneContent.Clear();
            phoneButtons.Clear();
        }

        public void PhoneClear(string header = null)
        {
            if (header != null) phoneHeader.text = header;
            phoneContent.Clear();
        }

        /// <summary>A white card like an ad or a notification.</summary>
        public void PhoneCard(string title, string body)
        {
            var c = new VisualElement();
            c.style.backgroundColor = new Color(0.97f, 0.95f, 0.9f);
            Pad(c, 18, 16);
            Round(c, 16);
            c.style.marginBottom = 12;
            var t = Text(title, 22, new Color(0.05f, 0.3f, 0.55f), bold: true);
            t.style.whiteSpace = WhiteSpace.Normal;
            var b = Text(body, 17, new Color(0.15f, 0.15f, 0.15f));
            b.style.whiteSpace = WhiteSpace.Normal;
            b.style.marginTop = 6;
            c.Add(t);
            c.Add(b);
            phoneContent.Add(c);
        }

        /// <summary>A chat bubble: mine on the right in blue, theirs on the left in grey.</summary>
        public void PhoneBubble(string text, bool mine, string sender = null)
        {
            var wrap = new VisualElement();
            wrap.style.flexDirection = FlexDirection.Column;
            wrap.style.alignItems = mine ? Align.FlexEnd : Align.FlexStart;
            wrap.style.marginBottom = 10;
            if (!string.IsNullOrEmpty(sender)) wrap.Add(Text(sender, 14, MenuStyle.NoteColor));
            var bubble = Text(text, 18, Color.white);
            bubble.style.whiteSpace = WhiteSpace.Normal;
            bubble.style.maxWidth = 300;
            bubble.style.backgroundColor = mine ? BubbleMine : BubbleTheirs;
            Pad(bubble, 14, 10);
            Round(bubble, 16);
            wrap.Add(bubble);
            phoneContent.Add(wrap);
        }

        /// <summary>Buttons at the bottom of the phone screen; the pick lands in <see cref="LastChoice"/>.</summary>
        public IEnumerator PhoneChoose(params string[] options) => ChooseIn(phoneButtons, options, 18);

        // ---------- Fades, cards, prompt, HUD ----------

        public IEnumerator Fade(float to, float duration)
        {
            float from = fade.resolvedStyle.opacity;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                fade.style.opacity = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            fade.style.opacity = to;
        }

        public void SetBlack(bool black) => fade.style.opacity = black ? 1f : 0f;

        /// <summary>Big centred text (usually over black). Holds for <paramref name="hold"/> s or until a press.</summary>
        public IEnumerator TitleCard(string title, string subtitle, float hold)
        {
            cardTitle.text = title;
            cardSubtitle.text = subtitle;
            MenuStyle.Show(cardSubtitle, !string.IsNullOrEmpty(subtitle));
            card.style.opacity = 0f;
            MenuStyle.Show(card, true);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { card.style.opacity = t / 0.6f; yield return null; }
            card.style.opacity = 1f;
            openedFrame = Time.frameCount;
            for (float t = 0f; t < hold && !AdvancePressed(); t += Time.deltaTime) yield return null;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { card.style.opacity = 1f - t / 0.5f; yield return null; }
            MenuStyle.Show(card, false);
        }

        public void SetPrompt(string text)
        {
            bool show = !string.IsNullOrEmpty(text) && !IsBusy;
            MenuStyle.Show(promptLabel, show);
            if (show) promptLabel.text = text;
        }

        public void SetObjective(string text)
        {
            MenuStyle.Show(objectiveLabel, !string.IsNullOrEmpty(text));
            objectiveLabel.text = text ?? "";
        }

        public void Toast(string text, Color? color = null, float duration = 1.4f)
        {
            toastLabel.text = text;
            toastLabel.style.color = color ?? new Color(1f, 0.85f, 0.3f);
            MenuStyle.Show(toastLabel, true);
            toastUntil = Time.time + duration;
        }
    }
}
