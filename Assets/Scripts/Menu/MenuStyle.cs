using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeachVolley.Menu
{
    /// <summary>
    /// Shared look for every menu (main, pause, practice options): sun-bleached panels, sea-blue
    /// buttons, built in code so all screens stay consistent without separate UXML per screen.
    /// </summary>
    public static class MenuStyle
    {
        public static readonly Color PanelColor = new Color(0.07f, 0.12f, 0.18f, 0.88f);
        public static readonly Color ButtonColor = new Color(0.12f, 0.45f, 0.62f);
        public static readonly Color ButtonHover = new Color(0.18f, 0.58f, 0.78f);
        public static readonly Color SelectedColor = new Color(0.98f, 0.62f, 0.2f);
        public static readonly Color DisabledColor = new Color(0.3f, 0.34f, 0.38f);
        public static readonly Color TextColor = new Color(0.97f, 0.95f, 0.9f);
        public static readonly Color NoteColor = new Color(0.75f, 0.82f, 0.86f);

        /// <summary>A rounded, padded column panel.</summary>
        public static VisualElement Panel(float width)
        {
            var p = new VisualElement();
            p.style.width = width;
            p.style.backgroundColor = PanelColor;
            p.style.paddingLeft = p.style.paddingRight = 28;
            p.style.paddingTop = p.style.paddingBottom = 24;
            SetRadius(p, 14);
            p.style.flexDirection = FlexDirection.Column;
            return p;
        }

        public static Label Title(string text, int size = 34)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = TextColor;
            l.style.marginBottom = 14;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            return l;
        }

        public static Label Heading(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 15;
            l.style.color = NoteColor;
            l.style.marginTop = 12;
            l.style.marginBottom = 4;
            return l;
        }

        public static Label Note(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 15;
            l.style.color = NoteColor;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginTop = 6;
            l.style.marginBottom = 10;
            return l;
        }

        /// <summary>A full-width menu button. Disabled buttons show greyed out with their reason.</summary>
        public static Button Button(string text, Action onClick, bool enabled = true, int fontSize = 20)
        {
            var b = new Button(onClick) { text = text };
            b.style.fontSize = fontSize;
            b.style.height = fontSize * 2.3f;
            b.style.marginTop = b.style.marginBottom = 5;
            b.style.color = enabled ? TextColor : NoteColor;
            b.style.backgroundColor = enabled ? ButtonColor : DisabledColor;
            b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
            SetRadius(b, 8);
            b.SetEnabled(enabled);
            // Keyboard and gamepad presses go through MenuNav (or the story UI's own keys) only;
            // UI Toolkit's own focus could otherwise press a second, different button.
            b.focusable = false;
            if (enabled)
            {
                b.RegisterCallback<MouseEnterEvent>(_ => { if (!IsSelected(b)) b.style.backgroundColor = ButtonHover; });
                b.RegisterCallback<MouseLeaveEvent>(_ => { if (!IsSelected(b)) b.style.backgroundColor = ButtonColor; });
            }
            return b;
        }

        /// <summary>A row that lays its children out side by side, sharing the width.</summary>
        public static VisualElement Row(params VisualElement[] children)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            foreach (var c in children)
            {
                c.style.flexGrow = 1;
                c.style.marginLeft = c.style.marginRight = 3;
                row.Add(c);
            }
            return row;
        }

        /// <summary>Highlights one option of a group (machine mode, pace...).</summary>
        public static void Select(Button b, bool selected)
        {
            b.userData = selected;
            b.style.backgroundColor = selected ? SelectedColor : ButtonColor;
        }

        static bool IsSelected(Button b) => b.userData is bool s && s;

        /// <summary>A full-screen layer that centres its content; optional dimmed backdrop.</summary>
        public static VisualElement Screen(bool dim, Justify horizontal = Justify.Center)
        {
            var s = new VisualElement();
            s.style.position = Position.Absolute;
            s.style.left = s.style.right = s.style.top = s.style.bottom = 0;
            s.style.flexDirection = FlexDirection.Row;
            s.style.justifyContent = horizontal;
            s.style.alignItems = Align.Center;
            if (dim) s.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            return s;
        }

        public static void Show(VisualElement e, bool show) => e.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }
    }
}
