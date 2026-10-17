using System;
using System.Collections.Generic;
using System.IO;
using BeachVolley.Skills;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// The story save: chapter and checkpoint, choice flags, relationships and the player's skills.
    /// Lives in a static so it survives scene loads, and is written to story.json at every checkpoint.
    /// Later chapters read the flags Chapter 1 sets (e.g. "signup", "first_answer", "transport").
    /// </summary>
    public static class StoryState
    {
        [Serializable]
        class Entry { public string key; public string value; }

        [Serializable]
        class Data
        {
            public int chapter = 1;
            public string checkpoint = "";
            public List<Entry> flags = new List<Entry>();
            public List<Entry> relationships = new List<Entry>();
            public int[] skills = new int[0];
            public float[] skillXp = new float[0];
            public float enjoyment = -1f;
        }

        static Data data;

        static string SavePath => Path.Combine(Application.persistentDataPath, "story.json");

        static Data Current => data ??= LoadOrNew();

        public static bool HasSave => File.Exists(SavePath);

        public static int Chapter { get => Current.chapter; set => Current.chapter = value; }
        public static string CheckpointName => Current.checkpoint;

        public static void NewGame()
        {
            data = new Data();
            Save();
        }

        /// <summary>Re-reads the save from disk (e.g. Continue from the main menu).</summary>
        public static void Load() => data = LoadOrNew();

        static Data LoadOrNew()
        {
            try
            {
                if (File.Exists(SavePath)) return JsonUtility.FromJson<Data>(File.ReadAllText(SavePath)) ?? new Data();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StoryState] Could not read save, starting fresh: " + e.Message);
            }
            return new Data();
        }

        public static void Save()
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(Current, true)); }
            catch (Exception e) { Debug.LogWarning("[StoryState] Could not write save: " + e.Message); }
        }

        public static void Checkpoint(string name)
        {
            Current.checkpoint = name;
            Save();
        }

        // ---------- Flags (choices) ----------

        public static void SetFlag(string key, string value) => Set(Current.flags, key, value);
        public static string Flag(string key, string fallback = "") => Get(Current.flags, key) ?? fallback;
        public static bool Is(string key, string value) => Flag(key) == value;

        // ---------- Relationships ----------

        public static int Relationship(string who) => int.TryParse(Get(Current.relationships, who), out int v) ? v : 0;
        public static void AddRelationship(string who, int amount) =>
            Set(Current.relationships, who, (Relationship(who) + amount).ToString());

        // ---------- Skills ----------

        /// <summary>Copies the saved skills onto the player (no-op on a fresh save).</summary>
        public static void ApplySkills(PlayerSkills skills)
        {
            if (skills == null || Current.skills.Length != PlayerSkills.Count) return;
            skills.Import(Current.skills, Current.skillXp, Current.enjoyment);
        }

        public static void StoreSkills(PlayerSkills skills)
        {
            if (skills == null) return;
            skills.Export(out Current.skills, out Current.skillXp, out Current.enjoyment);
        }

        static string Get(List<Entry> list, string key)
        {
            foreach (var e in list) if (e.key == key) return e.value;
            return null;
        }

        static void Set(List<Entry> list, string key, string value)
        {
            foreach (var e in list)
                if (e.key == key) { e.value = value; return; }
            list.Add(new Entry { key = key, value = value });
        }
    }
}
