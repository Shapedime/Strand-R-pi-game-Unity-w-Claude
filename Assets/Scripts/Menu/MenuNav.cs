using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BeachVolley.Menu
{
    /// <summary>
    /// Keyboard and gamepad control for a menu built from <see cref="MenuStyle.Button"/>s: Up/Down
    /// (arrows, W/S, d-pad) walks over the visible, enabled buttons in order, Enter/Space (or
    /// gamepad South) presses the highlighted one. The highlight is an outline, so it doesn't fight
    /// buttons that show a selected state of their own.
    /// </summary>
    public class MenuNav
    {
        static readonly Color Outline = MenuStyle.SelectedColor;

        readonly InputAction up, down, submit;
        readonly List<Button> buttons = new List<Button>();
        Button current;

        public MenuNav()
        {
            up = new InputAction("MenuUp", InputActionType.Button, "<Keyboard>/upArrow");
            up.AddBinding("<Keyboard>/w");
            up.AddBinding("<Gamepad>/dpad/up");
            down = new InputAction("MenuDown", InputActionType.Button, "<Keyboard>/downArrow");
            down.AddBinding("<Keyboard>/s");
            down.AddBinding("<Gamepad>/dpad/down");
            submit = new InputAction("MenuSubmit", InputActionType.Button, "<Keyboard>/enter");
            submit.AddBinding("<Keyboard>/numpadEnter");
            submit.AddBinding("<Keyboard>/space");
            submit.AddBinding("<Gamepad>/buttonSouth");
        }

        public void Enable() { up.Enable(); down.Enable(); submit.Enable(); }
        public void Disable() { up.Disable(); down.Disable(); submit.Disable(); }
        public void Dispose() { up.Dispose(); down.Dispose(); submit.Dispose(); }

        /// <summary>Call every frame while <paramref name="root"/> is the menu on screen.</summary>
        public void Update(VisualElement root)
        {
            buttons.Clear();
            root.Query<Button>().ForEach(b => { if (b.enabledInHierarchy && Shown(b, root)) buttons.Add(b); });
            if (buttons.Count == 0) { Mark(null); return; }

            int i = current != null ? buttons.IndexOf(current) : -1;
            if (i < 0) i = 0;
            if (up.WasPressedThisFrame()) i = (i + buttons.Count - 1) % buttons.Count;
            if (down.WasPressedThisFrame()) i = (i + 1) % buttons.Count;
            Mark(buttons[i]);

            if (submit.WasPressedThisFrame())
            {
                using (var e = NavigationSubmitEvent.GetPooled())
                {
                    e.target = current;
                    current.SendEvent(e);
                }
            }
        }

        /// <summary>Forget the highlight (e.g. when a different screen of the menu opens).</summary>
        public void Reset() => Mark(null);

        void Mark(Button b)
        {
            if (b == current) return;
            if (current != null) SetOutline(current, 0f);
            current = b;
            if (current != null) SetOutline(current, 3f);
        }

        static void SetOutline(Button b, float width)
        {
            b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = width;
            b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = Outline;
        }

        static bool Shown(VisualElement e, VisualElement root)
        {
            for (var v = e; v != null; v = v.parent)
            {
                if (v.resolvedStyle.display == DisplayStyle.None) return false;
                if (v == root) return true;
            }
            return true;
        }
    }
}
