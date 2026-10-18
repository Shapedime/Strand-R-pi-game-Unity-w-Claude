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
    /// Save version 2 stores the eleven 1–10 skill levels and unspent development points; a
    /// version-1 save (eight 0–100 skills plus XP) is converted on load.
    /// </summary>
    public static class StoryState
    {
        [Serializable]
        class Entry { public string key; public string value; }

        [Serializable]
        class Data
        {
            public int saveVersion = CurrentVersion;
            public int chapter = 1;
            public string checkpoint = "";
            public List<Entry> flags = new List<Entry>();
            public List<Entry> relationships = new List<Entry>();
            public int[] levels = new int[0];
            public int devPoints;
            public float credit;
            public int[] gymLevels = new int[0];
            public int gymPoints;
            public bool gymUnlocked;
            // Chapter 3 on: fatigue carries from one day to the next (0 fresh .. 1 exhausted).
            public float fatigue;
            // Version 1 only (read once for the conversion).
            public int[] skills = new int[0];
            public float[] skillXp = new float[0];
            public float enjoyment = -1f;
        }

        const int CurrentVersion = 3;

        /// <summary>Development points a Chapter 1 save brings into the new system.</summary>
        const int ConvertedChapter1Points = 2;

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
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    var d = JsonUtility.FromJson<Data>(json) ?? new Data();
                    // Old saves have no saveVersion field, so JsonUtility leaves the default; detect by content.
                    if (!json.Contains("\"saveVersion\"")) d.saveVersion = 1;
                    if (d.saveVersion < 2) Upgrade(d);
                    if (d.saveVersion < 3) UpgradeToGym(d);
                    return d;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StoryState] Could not read save, starting fresh: " + e.Message);
            }
            return new Data();
        }

        /// <summary>
        /// Version 1 → 2: eight 0–100 skills (Serve, Pass, Set, Attack, Block, Defense, Footwork,
        /// Awareness) become eleven 1–10 levels. Chapter 1 never got anyone past the rookie range,
        /// so the levels mostly start at 1, and the old XP turns into a few development points.
        /// </summary>
        static void Upgrade(Data d)
        {
            if (d.skills != null && d.skills.Length == 8)
            {
                int L(int old) => Mathf.Clamp(1 + Mathf.Max(0, old - 10) / 10, PlayerSkills.MinLevel, PlayerSkills.MaxLevel);
                var o = d.skills;
                var n = new int[PlayerSkills.Count];
                for (int i = 0; i < n.Length; i++) n[i] = PlayerSkills.MinLevel;
                n[(int)SkillType.Serve] = L(o[0]);
                n[(int)SkillType.Bump] = L(o[1]);
                n[(int)SkillType.Set] = L(o[2]);
                n[(int)SkillType.Spike] = L(o[3]);
                n[(int)SkillType.Roll] = L(o[3]);
                n[(int)SkillType.Block] = L(o[4]);
                n[(int)SkillType.Athleticism] = L(o[6]);
                n[(int)SkillType.CourtAwareness] = L(o[7]);
                d.levels = n;
                d.devPoints += d.chapter >= 2 || d.checkpoint == "home_end" ? ConvertedChapter1Points : 0;
            }
            d.skills = new int[0];
            d.skillXp = new float[0];
            d.saveVersion = 2;
        }

        /// <summary>
        /// Version 2 → 3: Jumping (the eleventh skill) became the gym's Leg Power. Points spent on it
        /// come back, and the gym starts at level 1 with no gym points.
        /// </summary>
        static void UpgradeToGym(Data d)
        {
            const int oldCount = 11, jumping = 10;
            if (d.levels != null && d.levels.Length == oldCount)
            {
                for (int lv = 2; lv <= d.levels[jumping]; lv++) d.devPoints += PlayerSkills.CostOfLevel(lv);
                var n = new int[PlayerSkills.Count];
                System.Array.Copy(d.levels, n, PlayerSkills.Count);
                d.levels = n;
            }
            d.saveVersion = CurrentVersion;
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

        /// <summary>Fatigue carried between days (Chapter 3 on). Chapters 1–2 reset it every session.</summary>
        public static float Fatigue { get => Current.fatigue; set => Current.fatigue = Mathf.Clamp01(value); }

        /// <summary>Copies the saved skills onto the player (no-op on a fresh save).</summary>
        public static void ApplySkills(PlayerSkills skills)
        {
            if (skills == null) return;
            skills.ImportGym(Current.gymLevels, Current.gymPoints, Current.gymUnlocked);
            skills.SetFatigue(Current.fatigue);
            if (Current.levels.Length != PlayerSkills.Count) return;
            skills.Import(Current.levels, Current.devPoints, Current.credit);
        }

        public static void StoreSkills(PlayerSkills skills)
        {
            if (skills == null) return;
            skills.Export(out Current.levels, out Current.devPoints, out Current.credit);
            skills.ExportGym(out Current.gymLevels, out Current.gymPoints, out Current.gymUnlocked);
            Current.fatigue = skills.Fatigue;
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
