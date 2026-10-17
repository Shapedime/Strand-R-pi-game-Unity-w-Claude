using BeachVolley.Ball;
using BeachVolley.Court;
using BeachVolley.Player;
using UnityEngine;

namespace BeachVolley.Practice
{
    /// <summary>
    /// Three coloured zones on the far court (deep left, deep right, short by the net) for
    /// practising placement. A zone flashes and counts when one of your attacks or serves lands
    /// in it. Just for fun: no skills or XP.
    /// </summary>
    public class TargetZones : MonoBehaviour
    {
        [SerializeField] VolleyBall ball;
        [SerializeField] Transform player;
        [SerializeField] BallContact contact;
        [SerializeField] PlayerServe serve;
        [SerializeField] bool zonesOn = true;

        // Zone layout on the far half: centre (x, distance from the net) and size (x, z).
        static readonly (string name, Vector2 center, Vector2 size, Color color)[] Layout =
        {
            ("Deep left", new Vector2(-2.6f, 6.6f), new Vector2(2.4f, 2.2f), new Color(1f, 0.45f, 0.2f)),
            ("Short", new Vector2(0f, 1.5f), new Vector2(3.2f, 1.6f), new Color(1f, 0.85f, 0.2f)),
            ("Deep right", new Vector2(2.6f, 6.6f), new Vector2(2.4f, 2.2f), new Color(0.2f, 0.85f, 0.9f)),
        };

        Transform[] zones;
        Material[] materials;
        float[] flashUntil;
        int[] counts;
        float layoutSide;
        bool shotByPlayer;
        GUIStyle style;

        public bool ZonesOn
        {
            get => zonesOn;
            set
            {
                zonesOn = value;
                if (zones != null) foreach (var z in zones) z.gameObject.SetActive(value);
            }
        }

        void Awake()
        {
            if (ball == null) ball = FindAnyObjectByType<VolleyBall>();
            if (contact == null) contact = FindAnyObjectByType<BallContact>();
            if (serve == null) serve = FindAnyObjectByType<PlayerServe>();
            if (player == null && contact != null) player = contact.transform;

            zones = new Transform[Layout.Length];
            materials = new Material[Layout.Length];
            flashUntil = new float[Layout.Length];
            counts = new int[Layout.Length];
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            for (int i = 0; i < Layout.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Target " + Layout[i].name;
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                materials[i] = new Material(shader);
                go.GetComponent<Renderer>().sharedMaterial = materials[i];
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                zones[i] = go.transform;
            }
            layoutSide = 0f;
            ZonesOn = zonesOn;
        }

        void OnEnable()
        {
            if (contact != null) contact.Played += OnPlayed;
            if (serve != null) serve.Hit += OnServed;
            if (ball != null) ball.Landed += OnLanded;
        }

        void OnDisable()
        {
            if (contact != null) contact.Played -= OnPlayed;
            if (serve != null) serve.Hit -= OnServed;
            if (ball != null) ball.Landed -= OnLanded;
        }

        // Only shots that go toward the far court count: attacks and serves.
        void OnPlayed(HitType type, float height) =>
            shotByPlayer = type == HitType.Spike || type == HitType.Roll || type == HitType.Block;
        void OnServed(float side) => shotByPlayer = true;

        float FarSide => player != null && player.position.z > 0f ? -1f : 1f;

        void Update()
        {
            if (FarSide != layoutSide) PlaceZones();
            for (int i = 0; i < zones.Length; i++)
            {
                bool flash = Time.time < flashUntil[i];
                var c = Layout[i].color;
                materials[i].color = flash ? Color.Lerp(c, Color.white, 0.6f) : c * 0.85f;
                float pulse = flash ? 1f + 0.08f * Mathf.Sin(Time.time * 30f) : 1f;
                zones[i].localScale = new Vector3(Layout[i].size.x * pulse, 0.02f, Layout[i].size.y * pulse);
            }
        }

        void PlaceZones()
        {
            layoutSide = FarSide;
            for (int i = 0; i < zones.Length; i++)
                zones[i].position = new Vector3(Layout[i].center.x, 0.012f, layoutSide * Layout[i].center.y);
        }

        void OnLanded(Vector3 point)
        {
            bool counted = shotByPlayer;
            shotByPlayer = false;
            if (!zonesOn || !counted || Mathf.Sign(point.z) != layoutSide) return;
            for (int i = 0; i < zones.Length; i++)
            {
                Vector3 d = point - zones[i].position;
                if (Mathf.Abs(d.x) <= Layout[i].size.x * 0.5f + VolleyBall.Radius
                    && Mathf.Abs(d.z) <= Layout[i].size.y * 0.5f + VolleyBall.Radius)
                {
                    counts[i]++;
                    flashUntil[i] = Time.time + 0.8f;
                }
            }
        }

        void OnGUI()
        {
            if (!zonesOn) return;
            style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, alignment = TextAnchor.UpperRight, richText = true,
                normal = { textColor = Color.white },
            };
            var text = $"Targets   <color=#ff8a4d>Deep left {counts[0]}</color>   <color=#ffd94d>Short {counts[1]}</color>   <color=#4dd9e6>Deep right {counts[2]}</color>";
            GUI.Label(new Rect(Screen.width - 520f, 12f, 508f, 24f), text, style);
        }
    }
}
