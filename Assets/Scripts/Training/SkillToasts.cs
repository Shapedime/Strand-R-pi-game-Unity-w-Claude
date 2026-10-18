using System.Collections.Generic;
using BeachVolley.Skills;
using UnityEngine;

namespace BeachVolley.Training
{
    /// <summary>Floating "+2 development points" messages whenever training awards points.</summary>
    public class SkillToasts : MonoBehaviour
    {
        [SerializeField] PlayerSkills skills;
        [SerializeField] float lifetime = 2.2f;
        [SerializeField] float riseDistance = 40f;

        struct Toast
        {
            public string text;
            public float born;
        }

        readonly List<Toast> toasts = new List<Toast>();
        GUIStyle style;

        void Awake()
        {
            if (skills == null) skills = FindAnyObjectByType<PlayerSkills>();
        }

        void OnEnable()
        {
            if (skills != null) skills.PointsAwarded += OnPoints;
        }

        void OnDisable()
        {
            if (skills != null) skills.PointsAwarded -= OnPoints;
        }

        void OnPoints(int amount, string reason) =>
            toasts.Add(new Toast { text = amount == 1 ? "+1 development point" : $"+{amount} development points", born = Time.time });

        void OnGUI()
        {
            if (toasts.Count == 0) return;
            style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

            toasts.RemoveAll(t => Time.time - t.born > lifetime);
            for (int i = 0; i < toasts.Count; i++)
            {
                float age = (Time.time - toasts[i].born) / lifetime;
                float y = Screen.height * 0.38f - age * riseDistance - i * 30f;
                style.normal.textColor = new Color(0.55f, 1f, 0.6f, 1f - age * age);
                GUI.Label(new Rect(0f, y, Screen.width, 30f), toasts[i].text, style);
            }
        }
    }
}
