using System.Collections.Generic;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Lines that are only ever said once. A director offers a few alternatives for a moment
    /// ("first good pass", "third miss in a row"); this hands back one nobody has heard yet, or
    /// null when they're all used up, in which case the moment just passes in silence. That keeps
    /// five days of drills from turning into the same three lines on repeat.
    /// Lives in a static, so it spans scene loads within a play session.
    /// </summary>
    public static class BarkPool
    {
        static readonly HashSet<string> used = new HashSet<string>();

        /// <summary>A random line from <paramref name="lines"/> that hasn't been said yet (then marked as said).</summary>
        public static string Once(params string[] lines)
        {
            var fresh = new List<string>();
            foreach (var l in lines) if (!string.IsNullOrEmpty(l) && !used.Contains(l)) fresh.Add(l);
            if (fresh.Count == 0) return null;
            string pick = fresh[Random.Range(0, fresh.Count)];
            used.Add(pick);
            return pick;
        }

        /// <summary>The first line that hasn't been said yet, in order (for lines that build on each other).</summary>
        public static string Next(params string[] lines)
        {
            foreach (var l in lines)
                if (!string.IsNullOrEmpty(l) && used.Add(l)) return l;
            return null;
        }

        public static bool Said(string line) => used.Contains(line);

        public static void Forget() => used.Clear();
    }
}
