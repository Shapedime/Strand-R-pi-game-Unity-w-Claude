using UnityEngine;
using UnityEngine.InputSystem;

namespace BeachVolley.Skills
{
    /// <summary>Tiny top-left readout of the eight skills, XP toward the next point, and enjoyment. F1 toggles.</summary>
    [RequireComponent(typeof(PlayerSkills))]
    public class SkillDebugHud : MonoBehaviour
    {
        [Tooltip("Hidden by default while skills are not part of practice; F1 shows it.")]
        [SerializeField] bool visible = false;

        PlayerSkills skills;
        GUIStyle style;

        void Awake() => skills = GetComponent<PlayerSkills>();

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };

            const float width = 190f, line = 18f;
            var rect = new Rect(10f, 10f, width, line * (PlayerSkills.Count + 2) + 12f);
            GUI.Box(rect, GUIContent.none);

            float y = rect.y + 6f;
            for (int i = 0; i < PlayerSkills.Count; i++)
            {
                var type = (SkillType)i;
                int xpPct = Mathf.FloorToInt(skills.Xp(type) / skills.XpToNext(type) * 100f);
                GUI.Label(new Rect(rect.x + 8f, y, width - 16f, line),
                    $"{type,-10} <b>{skills.Get(type),3}</b>   <color=#aaaaaa>xp {xpPct,2}%</color>", style);
                y += line;
            }
            GUI.Label(new Rect(rect.x + 8f, y + 4f, width - 16f, line),
                $"Enjoyment <b>{skills.Enjoyment:0}</b>  <color=#aaaaaa>x{skills.XpMultiplier:0.00}</color>", style);
        }
    }
}
