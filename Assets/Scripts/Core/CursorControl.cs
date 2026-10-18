using System.Collections.Generic;
using UnityEngine;

namespace BeachVolley.Core
{
    /// <summary>
    /// One owner for the mouse cursor: hidden and locked while playing and during plain dialogue,
    /// visible while any menu is open (main menu, pause, practice options, story choices, the
    /// phone's buttons, the development screen). Menus work with the keys too.
    /// A menu calls <see cref="Show"/> with itself when it opens and <see cref="Hide"/> when it
    /// closes; a menu that is destroyed while open (a scene change) stops counting on its own.
    /// Installs itself at startup.
    /// </summary>
    public class CursorControl : MonoBehaviour
    {
        static readonly List<Object> open = new List<Object>();

        public static void Show(Object menu)
        {
            if (menu != null && !open.Contains(menu)) open.Add(menu);
        }

        public static void Hide(Object menu) => open.Remove(menu);

        public static bool Visible => open.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            open.Clear();
            var go = new GameObject("CursorControl");
            DontDestroyOnLoad(go);
            go.AddComponent<CursorControl>();
        }

        void LateUpdate()
        {
            // Destroyed menus compare equal to null.
            open.RemoveAll(m => m == null);
            bool show = open.Count > 0;
            var mode = show ? CursorLockMode.None : CursorLockMode.Locked;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            if (Cursor.visible != show) Cursor.visible = show;
        }
    }
}
