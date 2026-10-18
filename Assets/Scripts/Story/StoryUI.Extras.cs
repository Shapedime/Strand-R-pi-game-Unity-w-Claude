using System.Collections;
using System.Collections.Generic;
using BeachVolley.Core;
using BeachVolley.Menu;
using BeachVolley.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BeachVolley.Story
{
    /// <summary>One line of the end-of-day training card.</summary>
    public struct TrainingLine
    {
        public string label, grade;
        public int points;
        public TrainingLine(string label, string grade, int points) { this.label = label; this.grade = grade; this.points = points; }
    }

    /// <summary>
    /// The Chapter 2 additions to the story UI: live barks during play (they never freeze the
    /// player), the partner's hand signal, the energy meter, the player development screen and
    /// the training-complete card.
    /// </summary>
    public partial class StoryUI
    {
        static readonly Color PipEmpty = new Color(0.22f, 0.27f, 0.32f);
        static readonly Color PipFull = new Color(0.25f, 0.68f, 0.9f);
        static readonly Color PipNew = new Color(0.98f, 0.62f, 0.2f);

        VisualElement barkBox, signal, signalHand, meter, meterFill, devScreen, trainingCard;
        VisualElement timing, timingZone, timingMarker;
        Label barkSpeaker, barkText, signalCaption, meterLabel, timingTitle, timingInfo;
        float barkUntil;
        InputAction leftAction, rightAction;

        void BuildExtras(VisualElement root)
        {
            leftAction ??= Bind("Left", "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left");
            rightAction ??= Bind("Right", "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right");

            // Bark: a short line someone calls out during play.
            barkBox = new VisualElement();
            barkBox.style.position = Position.Absolute;
            barkBox.style.bottom = 90;
            barkBox.style.left = new Length(50, LengthUnit.Percent);
            barkBox.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            barkBox.style.maxWidth = 900;
            barkBox.style.flexDirection = FlexDirection.Row;
            barkBox.style.backgroundColor = new Color(0.05f, 0.08f, 0.12f, 0.78f);
            Pad(barkBox, 20, 10);
            Round(barkBox, 12);
            barkSpeaker = Text("", 22, MenuStyle.SelectedColor, bold: true);
            barkSpeaker.style.marginRight = 10;
            barkText = Text("", 22, MenuStyle.TextColor);
            barkText.style.whiteSpace = WhiteSpace.Normal;
            barkText.style.flexShrink = 1;
            barkBox.Add(barkSpeaker);
            barkBox.Add(barkText);
            root.Add(barkBox);

            // The hand signal behind the blocker's back.
            signal = new VisualElement();
            signal.style.position = Position.Absolute;
            signal.style.right = 80;
            signal.style.top = new Length(30, LengthUnit.Percent);
            signal.style.alignItems = Align.Center;
            signal.style.backgroundColor = MenuStyle.PanelColor;
            Pad(signal, 26, 18);
            Round(signal, 16);
            signalHand = new VisualElement();
            signalHand.style.alignItems = Align.Center;
            signalCaption = Text("", 20, MenuStyle.TextColor, bold: true);
            signalCaption.style.marginTop = 12;
            signal.Add(Text("SIGNAL", 15, MenuStyle.NoteColor, bold: true));
            signal.Add(signalHand);
            signal.Add(signalCaption);
            root.Add(signal);

            // Energy meter (fatigue), top right.
            meter = new VisualElement();
            meter.style.position = Position.Absolute;
            meter.style.right = 40;
            meter.style.top = 32;
            meter.style.width = 260;
            meter.style.backgroundColor = MenuStyle.PanelColor;
            Pad(meter, 14, 10);
            Round(meter, 10);
            meterLabel = Text("Energy", 16, MenuStyle.NoteColor, bold: true);
            var track = new VisualElement();
            track.style.height = 12;
            track.style.marginTop = 6;
            track.style.backgroundColor = PipEmpty;
            Round(track, 6);
            meterFill = new VisualElement();
            meterFill.style.height = 12;
            meterFill.style.backgroundColor = new Color(0.4f, 0.85f, 0.45f);
            Round(meterFill, 6);
            track.Add(meterFill);
            meter.Add(meterLabel);
            meter.Add(track);
            root.Add(meter);

            // Timing bar (gym exercises, wall passes), bottom centre.
            timing = new VisualElement();
            timing.style.position = Position.Absolute;
            timing.style.bottom = 70;
            timing.style.left = new Length(50, LengthUnit.Percent);
            timing.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            timing.style.width = 720;
            timing.style.alignItems = Align.Center;
            timing.style.backgroundColor = MenuStyle.PanelColor;
            Pad(timing, 26, 16);
            Round(timing, 14);
            timingTitle = Text("", 26, MenuStyle.TextColor, bold: true);
            timingInfo = Text("", 17, MenuStyle.NoteColor);
            timingInfo.style.marginTop = 4;
            var bar = new VisualElement();
            bar.style.width = 640;
            bar.style.height = 26;
            bar.style.marginTop = 12;
            bar.style.backgroundColor = PipEmpty;
            Round(bar, 8);
            timingZone = new VisualElement();
            timingZone.style.position = Position.Absolute;
            timingZone.style.top = 0;
            timingZone.style.bottom = 0;
            timingZone.style.backgroundColor = new Color(0.4f, 0.85f, 0.45f, 0.85f);
            Round(timingZone, 6);
            timingMarker = new VisualElement();
            timingMarker.style.position = Position.Absolute;
            timingMarker.style.top = -6;
            timingMarker.style.bottom = -6;
            timingMarker.style.width = 8;
            timingMarker.style.backgroundColor = Color.white;
            Round(timingMarker, 3);
            bar.Add(timingZone);
            bar.Add(timingMarker);
            var timingKeys = Text("Space / Click / (A) when the marker is in the green", 14, MenuStyle.NoteColor);
            timingKeys.style.marginTop = 10;
            timing.Add(timingTitle);
            timing.Add(timingInfo);
            timing.Add(bar);
            timing.Add(timingKeys);
            root.Add(timing);

            devScreen = MenuStyle.Screen(dim: true);
            trainingCard = MenuStyle.Screen(dim: true);

            foreach (var e in new[] { barkBox, signal, meter, timing, devScreen, trainingCard }) MenuStyle.Show(e, false);
        }

        /// <summary>Full-screen panels go on top of everything, the black fade included.</summary>
        void AddOverlays(VisualElement root)
        {
            root.Add(trainingCard);
            root.Add(devScreen);
        }

        InputAction Bind(string name, params string[] paths)
        {
            var a = new InputAction(name, InputActionType.Button, paths[0]);
            for (int i = 1; i < paths.Length; i++) a.AddBinding(paths[i]);
            a.Enable();
            return a;
        }

        void UpdateExtras()
        {
            if (barkBox != null && barkBox.style.display == DisplayStyle.Flex && Time.time > barkUntil)
                MenuStyle.Show(barkBox, false);
        }

        void DisposeExtras()
        {
            leftAction?.Dispose();
            rightAction?.Dispose();
        }

        // ---------- Barks, signal, meter ----------

        /// <summary>A line called out during play. Never blocks: gameplay keeps running.</summary>
        public void Bark(string speaker, string text, float duration = 2.8f)
        {
            if (barkBox == null || string.IsNullOrEmpty(text)) return;
            barkSpeaker.text = speaker ?? "";
            MenuStyle.Show(barkSpeaker, !string.IsNullOrEmpty(speaker));
            barkText.text = text;
            MenuStyle.Show(barkBox, true);
            barkUntil = Time.time + duration;
        }

        /// <summary>Shows a one- or two-finger signal (0 hides it), with an optional caption.</summary>
        public void ShowSignal(int fingers, string caption)
        {
            if (signal == null) return;
            MenuStyle.Show(signal, fingers > 0);
            if (fingers <= 0) return;
            signalHand.Clear();
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            row.style.height = 70;
            for (int i = 0; i < 4; i++)
            {
                bool up = i < fingers;
                var f = new VisualElement();
                f.style.width = 16;
                f.style.height = up ? 66 : 22;
                f.style.marginLeft = f.style.marginRight = 3;
                f.style.backgroundColor = up ? new Color(0.95f, 0.78f, 0.62f) : new Color(0.75f, 0.58f, 0.45f);
                Round(f, 8);
                row.Add(f);
            }
            var palm = new VisualElement();
            palm.style.width = 92;
            palm.style.height = 54;
            palm.style.backgroundColor = new Color(0.95f, 0.78f, 0.62f);
            Round(palm, 14);
            signalHand.Add(row);
            signalHand.Add(palm);
            signalHand.Add(Text(fingers == 1 ? "1 — LINE" : "2 — CROSS", 26, MenuStyle.SelectedColor, bold: true));
            signalCaption.text = caption ?? "";
            MenuStyle.Show(signalCaption, !string.IsNullOrEmpty(caption));
        }

        /// <summary>Energy bar from fatigue (0 fresh .. 1 exhausted); null hides it.</summary>
        public void SetEnergy(float? fatigue)
        {
            if (meter == null) return;
            MenuStyle.Show(meter, fatigue.HasValue);
            if (!fatigue.HasValue) return;
            float energy = 1f - Mathf.Clamp01(fatigue.Value);
            meterFill.style.width = new Length(energy * 100f, LengthUnit.Percent);
            meterFill.style.backgroundColor = Color.Lerp(new Color(0.95f, 0.4f, 0.3f), new Color(0.4f, 0.85f, 0.45f), energy);
            meterLabel.text = energy > 0.66f ? "Energy" : energy > 0.33f ? "Energy  ·  getting tired" : "Energy  ·  running on empty";
        }

        // ---------- Timing bar ----------

        /// <summary>Shows or hides the timing bar.</summary>
        public void ShowTiming(bool show) { if (timing != null) MenuStyle.Show(timing, show); }

        /// <summary>
        /// Updates the timing bar: the green zone (centre and width, 0..1 of the bar), the marker
        /// (0..1) and an optional flash colour for the marker after a press.
        /// </summary>
        public void SetTiming(string title, string info, float zoneCentre, float zoneWidth, float marker, Color? flash = null)
        {
            if (timing == null) return;
            timingTitle.text = title;
            timingInfo.text = info;
            float left = Mathf.Clamp01(zoneCentre - zoneWidth * 0.5f), right = Mathf.Clamp01(zoneCentre + zoneWidth * 0.5f);
            timingZone.style.left = new Length(left * 100f, LengthUnit.Percent);
            timingZone.style.width = new Length((right - left) * 100f, LengthUnit.Percent);
            timingMarker.style.left = new Length(Mathf.Clamp01(marker) * 100f, LengthUnit.Percent);
            timingMarker.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            timingMarker.style.backgroundColor = flash ?? Color.white;
        }

        // ---------- Training complete ----------

        /// <summary>The end-of-day card: each drill, how it went, the points it earned. Waits for a press.</summary>
        public IEnumerator TrainingCard(string title, IList<TrainingLine> lines, string footer) =>
            TrainingCard(title, lines, footer, "Development points earned");

        /// <summary>The same card with its own total label (e.g. "Gym points earned").</summary>
        public IEnumerator TrainingCard(string title, IList<TrainingLine> lines, string footer, string totalLabel)
        {
            Block();
            trainingCard.Clear();
            var panel = MenuStyle.Panel(760);
            panel.Add(MenuStyle.Title(title, 38));
            int total = 0;
            foreach (var l in lines)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.justifyContent = Justify.SpaceBetween;
                row.style.marginTop = 8;
                var name = Text(l.label, 22, MenuStyle.TextColor);
                name.style.width = 360;
                var grade = Text(l.grade, 20, MenuStyle.NoteColor);
                grade.style.width = 200;
                var pts = Text(l.points > 0 ? $"+{l.points}" : "", 22, PipNew, bold: true);
                pts.style.width = 70;
                pts.style.unityTextAlign = TextAnchor.MiddleRight;
                row.Add(name);
                row.Add(grade);
                row.Add(pts);
                panel.Add(row);
                total += l.points;
            }
            var sum = Text($"{totalLabel}: {total}", 24, PipNew, bold: true);
            sum.style.marginTop = 18;
            panel.Add(sum);
            if (!string.IsNullOrEmpty(footer)) { var f = MenuStyle.Note(footer); f.style.marginTop = 10; panel.Add(f); }
            var hint = Text("▼  Space / Enter", 15, MenuStyle.NoteColor);
            hint.style.marginTop = 14;
            hint.style.alignSelf = Align.FlexEnd;
            panel.Add(hint);
            trainingCard.Add(panel);
            MenuStyle.Show(trainingCard, true);
            openedFrame = Time.frameCount;
            yield return null;
            while (!AdvancePressed()) yield return null;
            MenuStyle.Show(trainingCard, false);
            Unblock();
            yield return null;
        }

        // ---------- Player development ----------

        /// <summary>One line on the development screen: a volleyball or a gym skill behind delegates.</summary>
        class SkillRow
        {
            public string name, desc;
            public System.Func<int> level, pending, cost;
            public System.Func<bool> canRaise;
            public System.Action raise, lower;
            public bool isNew;
            public VisualElement row;
            public VisualElement[] pips;
            public Label levelLabel, costLabel;
        }

        /// <summary>
        /// The development screen, two tabs: Volleyball (development points from court training) and
        /// Gym (gym points; locked until the gym opens). Up/Down picks a skill (above the first one
        /// is the tab bar, where Left/Right switches tab; Q/E switch from anywhere), Right/Left
        /// raises and undoes, Enter confirms. Read-only when <paramref name="canSpend"/> is false.
        /// </summary>
        public IEnumerator Development(PlayerSkills skills, string note, bool canSpend, ICollection<SkillType> highlight = null, int startTab = 0)
        {
            Block();
            CursorControl.Show(this);
            if (canSpend) skills.BeginAllocation();
            int tab = Mathf.Clamp(startTab, 0, 1), selected = 0;
            bool done = false;

            var courtRows = new List<SkillRow>();
            foreach (var t in SkillInfo.DisplayOrder)
            {
                var type = t;
                courtRows.Add(new SkillRow
                {
                    name = SkillInfo.Name(type), desc = SkillInfo.Description(type),
                    level = () => skills.Get(type), pending = () => canSpend ? skills.PendingGain(type) : 0,
                    cost = () => skills.CostToRaise(type), canRaise = () => canSpend && skills.CanRaise(type),
                    raise = () => { if (canSpend) skills.TryRaise(type); }, lower = () => { if (canSpend) skills.TryLower(type); },
                    isNew = highlight != null && highlight.Contains(type),
                });
            }
            var gymRows = new List<SkillRow>();
            foreach (var t in SkillInfo.GymOrder)
            {
                var type = t;
                gymRows.Add(new SkillRow
                {
                    name = SkillInfo.Name(type), desc = SkillInfo.Description(type),
                    level = () => skills.Get(type), pending = () => canSpend ? skills.PendingGain(type) : 0,
                    cost = () => skills.CostToRaise(type), canRaise = () => canSpend && skills.CanRaise(type),
                    raise = () => { if (canSpend) skills.TryRaise(type); }, lower = () => { if (canSpend) skills.TryLower(type); },
                });
            }
            bool gymLocked = !skills.GymUnlocked && skills.GymPoints == 0;

            devScreen.Clear();
            var panel = MenuStyle.Panel(1080);
            panel.style.paddingTop = 22;
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.Add(MenuStyle.Title("PLAYER DEVELOPMENT", 32));
            var pointsLabel = Text("", 26, PipNew, bold: true);
            header.Add(pointsLabel);
            panel.Add(header);

            // Tabs.
            var tabBar = new VisualElement();
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.marginBottom = 8;
            Pad(tabBar, 4, 4);
            Round(tabBar, 10);
            var tabLabels = new[] { Text("VOLLEYBALL", 20, MenuStyle.TextColor, bold: true), Text("GYM", 20, MenuStyle.TextColor, bold: true) };
            for (int i = 0; i < tabLabels.Length; i++)
            {
                int index = i;
                var l = tabLabels[i];
                Pad(l, 22, 6);
                Round(l, 8);
                l.style.marginRight = 8;
                l.pickingMode = PickingMode.Position;
                l.RegisterCallback<ClickEvent>(_ => { tab = index; selected = 0; });
                tabBar.Add(l);
            }
            panel.Add(tabBar);

            if (!string.IsNullOrEmpty(note))
            {
                var n = Text(note, 18, new Color(0.7f, 0.95f, 0.75f));
                n.style.whiteSpace = WhiteSpace.Normal;
                n.style.marginBottom = 8;
                panel.Add(n);
            }
            var locked = Text("Gym opens in Chapter 3 — trained legs jump higher, strong arms hit harder. Gym points are earned only in the gym.", 18, PipNew);
            locked.style.whiteSpace = WhiteSpace.Normal;
            locked.style.marginBottom = 8;
            panel.Add(locked);

            var courtList = new VisualElement();
            var gymList = new VisualElement();
            foreach (var (list, rows) in new[] { (courtList, courtRows), (gymList, gymRows) })
                for (int i = 0; i < rows.Count; i++)
                {
                    int index = i;
                    var r = rows[i];
                    BuildRow(r, canSpend, () => selected = index);
                    list.Add(r.row);
                }
            panel.Add(courtList);
            panel.Add(gymList);

            var desc = Text("", 18, MenuStyle.NoteColor);
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.minHeight = 48;
            desc.style.marginTop = 12;
            panel.Add(desc);
            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            if (canSpend)
            {
                var reset = MenuStyle.Button("Reset", () => { skills.CancelAllocation(); skills.BeginAllocation(); });
                reset.style.width = 160;
                reset.style.marginRight = 12;
                buttons.Add(reset);
            }
            var confirm = MenuStyle.Button(canSpend ? "Confirm" : "Close", () => done = true);
            confirm.style.width = 200;
            buttons.Add(confirm);
            panel.Add(buttons);
            var keys = Text(canSpend ? "↑ ↓ choose   ← → undo / raise   Q / E switch tab   Enter confirm   ·   unspent points are saved for later"
                : "↑ ↓ choose   Q / E switch tab   Enter to close", 14, MenuStyle.NoteColor);
            keys.style.marginTop = 8;
            panel.Add(keys);
            devScreen.Add(panel);
            MenuStyle.Show(devScreen, true);
            openedFrame = Time.frameCount;
            var kb = Keyboard.current;
            yield return null;

            while (!done)
            {
                var rows = tab == 0 ? courtRows : gymRows;
                if (!Paused)
                {
                    // selected == -1 is the tab bar.
                    if (upAction.WasPressedThisFrame()) selected = selected <= -1 ? rows.Count - 1 : selected - 1;
                    if (downAction.WasPressedThisFrame()) selected = selected >= rows.Count - 1 ? -1 : selected + 1;
                    bool right = rightAction.WasPressedThisFrame(), left = leftAction.WasPressedThisFrame();
                    bool tabKey = kb != null && (kb.qKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame);
                    if (tabKey || (selected == -1 && (right || left)))
                    {
                        tab = 1 - tab;
                        if (selected >= 0) selected = 0;
                        rows = tab == 0 ? courtRows : gymRows;
                    }
                    else if (selected >= 0)
                    {
                        if (right) rows[selected].raise();
                        if (left) rows[selected].lower();
                    }
                    if (confirmAction.WasPressedThisFrame() && Time.frameCount > openedFrame) done = true;
                }

                MenuStyle.Show(courtList, tab == 0);
                MenuStyle.Show(gymList, tab == 1);
                MenuStyle.Show(locked, tab == 1 && gymLocked);
                for (int i = 0; i < tabLabels.Length; i++)
                {
                    tabLabels[i].style.backgroundColor = i == tab ? MenuStyle.ButtonColor : new Color(0f, 0f, 0f, 0f);
                    tabLabels[i].style.color = i == tab ? MenuStyle.TextColor : MenuStyle.NoteColor;
                }
                tabBar.style.backgroundColor = selected == -1 ? new Color(0.18f, 0.32f, 0.42f, 0.9f) : new Color(0f, 0f, 0f, 0f);
                int points = tab == 0 ? skills.DevPoints : skills.GymPoints;
                string unit = tab == 0 ? "Points" : "Gym points";
                pointsLabel.text = canSpend ? $"{unit} to spend:  {points}" : $"Unspent {unit.ToLowerInvariant()}:  {points}";
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = rows[i];
                    int lv = r.level(), gained = r.pending();
                    for (int k = 0; k < r.pips.Length; k++)
                        r.pips[k].style.backgroundColor = k < lv - gained ? PipFull : k < lv ? PipNew : PipEmpty;
                    r.levelLabel.text = gained > 0 ? $"Lv {lv}  (+{gained})" : $"Lv {lv}";
                    r.costLabel.text = lv >= PlayerSkills.MaxLevel ? "max" : $"next: {r.cost()} pt";
                    r.costLabel.style.color = r.canRaise() ? MenuStyle.TextColor : MenuStyle.NoteColor;
                    r.row.style.backgroundColor = i == selected ? new Color(0.18f, 0.32f, 0.42f, 0.9f) : new Color(0f, 0f, 0f, 0f);
                }
                desc.text = selected >= 0 && selected < rows.Count ? rows[selected].name + " — " + rows[selected].desc
                    : "← → switch between Volleyball and Gym.";
                yield return null;
            }

            if (canSpend) skills.ConfirmAllocation();
            CursorControl.Hide(this);
            MenuStyle.Show(devScreen, false);
            devScreen.Clear();
            Unblock();
            yield return null;
        }

        void BuildRow(SkillRow r, bool canSpend, System.Action onHover)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 38;
            row.style.marginTop = 3;
            Pad(row, 12, 0);
            Round(row, 8);
            row.RegisterCallback<MouseEnterEvent>(_ => onHover());

            var name = Text(r.name, 20, MenuStyle.TextColor, bold: true);
            name.style.width = 210;
            var tag = Text(r.isNew ? "NEW" : "", 13, PipNew, bold: true);
            tag.style.width = 44;
            var pipRow = new VisualElement();
            pipRow.style.flexDirection = FlexDirection.Row;
            pipRow.style.width = 330;
            r.pips = new VisualElement[PlayerSkills.MaxLevel];
            for (int k = 0; k < r.pips.Length; k++)
            {
                var pip = new VisualElement();
                pip.style.width = 26;
                pip.style.height = 16;
                pip.style.marginRight = 5;
                Round(pip, 4);
                pipRow.Add(pip);
                r.pips[k] = pip;
            }
            r.levelLabel = Text("", 20, MenuStyle.TextColor, bold: true);
            r.levelLabel.style.width = 80;
            r.costLabel = Text("", 16, MenuStyle.NoteColor);
            r.costLabel.style.width = 120;
            row.Add(name);
            row.Add(tag);
            row.Add(pipRow);
            row.Add(r.levelLabel);
            row.Add(r.costLabel);
            if (canSpend)
            {
                var minus = MenuStyle.Button("−", () => { onHover(); r.lower(); }, fontSize: 20);
                var plus = MenuStyle.Button("+", () => { onHover(); r.raise(); }, fontSize: 20);
                foreach (var b in new[] { minus, plus })
                {
                    b.style.width = 54;
                    b.style.height = 32;
                    b.style.marginLeft = 6;
                    b.style.marginTop = b.style.marginBottom = 0;
                }
                row.Add(minus);
                row.Add(plus);
            }
            r.row = row;
        }
    }
}
