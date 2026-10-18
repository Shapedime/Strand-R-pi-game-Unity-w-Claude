using UnityEngine;
using UnityEngine.InputSystem;

namespace BeachVolley.Skills
{
    /// <summary>Tiny top-left readout of the eleven skill levels, unspent points and fatigue. F1 toggles.</summary>
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

            const float width = 220f, line = 18f;
            var rect = new Rect(10f, 10f, width, line * (PlayerSkills.Count + 2) + 12f);
            GUI.Box(rect, GUIContent.none);

            float y = rect.y + 6f;
            for (int i = 0; i < PlayerSkills.Count; i++)
            {
                var type = (SkillType)i;
                GUI.Label(new Rect(rect.x + 8f, y, width - 16f, line),
                    $"{SkillInfo.Name(type),-16} <b>{skills.Get(type),2}</b>", style);
                y += line;
            }
            GUI.Label(new Rect(rect.x + 8f, y + 4f, width - 16f, line),
                $"Points <b>{skills.DevPoints}</b>  <color=#aaaaaa>fatigue {skills.Fatigue:0.00}</color>", style);
        }
    }
}
