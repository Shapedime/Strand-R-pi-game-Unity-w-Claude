using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeachVolley.Core
{
    /// <summary>
    /// Lets IMGUI overlays claim screen areas so gameplay ignores clicks on them
    /// (e.g. the camera won't capture the cursor when you click "Start practice").
    /// Overlays call <see cref="Block"/> from OnGUI every frame they're visible. Full-screen menus
    /// (pause, practice options) set <see cref="MenuOpen"/> instead: while it's true the camera
    /// won't capture the cursor and the player's action keys are ignored.
    /// </summary>
    public static class UiPointerGate
    {
        static readonly List<Rect> rects = new List<Rect>();
        static int frame = -1;

        /// <summary>A menu is open: gameplay input (clicks and action keys) is ignored.</summary>
        public static bool MenuOpen { get; set; }

        /// <param name="guiRect">Rect in GUI coordinates (origin top-left).</param>
        public static void Block(Rect guiRect)
        {
            if (Time.frameCount != frame)
            {
                rects.Clear();
                frame = Time.frameCount;
            }
            rects.Add(guiRect);
        }

        public static bool IsPointerOverUi
        {
            get
            {
                // OnGUI runs after Update, so rects from the previous frame are still current.
                if (MenuOpen) return true;
                if (Time.frameCount - frame > 1 || Mouse.current == null) return false;
                Vector2 p = Mouse.current.position.ReadValue();
                p.y = Screen.height - p.y;
                foreach (var r in rects)
                    if (r.Contains(p)) return true;
                return false;
            }
        }
    }
}
