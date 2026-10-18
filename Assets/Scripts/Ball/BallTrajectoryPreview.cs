using BeachVolley.Skills;
using UnityEngine;

namespace BeachVolley.Ball
{
    /// <summary>
    /// Dotted line along the ball's predicted flight plus a ring where it will land.
    /// Uses the same gravity and air drag as <see cref="VolleyBall"/>. Awareness decides
    /// how much of it you get: a rookie sees a short, faint stub; a master sees the whole
    /// arc and a bright landing mark. Hidden while the ball is resting on the sand.
    /// </summary>
    [RequireComponent(typeof(VolleyBall))]
    public class BallTrajectoryPreview : MonoBehaviour
    {
        [SerializeField] PlayerSkills skills;
        [Tooltip("Strong blue reads against the sand; a dark outline under the line adds contrast.")]
        [SerializeField] Color color = new Color(0.12f, 0.42f, 1f);
        [SerializeField] Color outlineColor = new Color(0.02f, 0.08f, 0.25f);
        [Tooltip("World Y of the sand surface.")]
        [SerializeField] float groundHeight = 0f;
        [SerializeField] float simStep = 0.02f;
        [Tooltip("Longest flight the landing prediction looks ahead.")]
        [SerializeField] float maxPredictTime = 6f;
        [Tooltip("Below this speed on the sand the ball counts as resting and nothing is drawn.")]
        [SerializeField] float restSpeed = 0.6f;

        [Header("Awareness 0 → 100")]
        [SerializeField] float trailTimeRookie = 0.7f;
        [SerializeField] float trailTimeMaster = 3f;
        [SerializeField] float widthRookie = 0.08f;
        [SerializeField] float widthMaster = 0.13f;
        [SerializeField, Range(0f, 1f)] float alphaRookie = 0.85f;
        [SerializeField, Range(0f, 1f)] float alphaMaster = 1f;
        [SerializeField, Range(0f, 1f)] float landingAlphaRookie = 0.7f;
        [SerializeField, Range(0f, 1f)] float landingAlphaMaster = 1f;
        [SerializeField] float landingSize = 0.6f;

        VolleyBall ball;
        LineRenderer line;
        LineRenderer outline;
        Transform landing;
        Material landingMat;
        Vector3[] points;

        void Awake()
        {
            ball = GetComponent<VolleyBall>();
            if (skills == null) skills = FindAnyObjectByType<PlayerSkills>();
            points = new Vector3[Mathf.CeilToInt(maxPredictTime / simStep) + 2];

            Shader shader = Shader.Find("Sprites/Default");

            // Lives outside the ball so it doesn't spin or scale with it. Solid dashes on top of a
            // slightly wider dark outline, so the trail stands out on bright sand.
            var dashes = DashTexture();
            line = MakeLine("BallTrail", shader, dashes, sortingOrder: 1);
            outline = MakeLine("BallTrailOutline", shader, dashes, sortingOrder: 0);

            var mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mark.name = "BallLandingMark";
            Destroy(mark.GetComponent<Collider>());
            landing = mark.transform;
            landing.rotation = Quaternion.Euler(90f, 0f, 0f);
            landingMat = new Material(shader) { mainTexture = LandingTexture(color) };
            var r = mark.GetComponent<Renderer>();
            r.sharedMaterial = landingMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            SetVisible(false);
        }

        void OnDisable() => SetVisible(false);

        void OnDestroy()
        {
            if (line != null) Destroy(line.gameObject);
            if (outline != null) Destroy(outline.gameObject);
            if (landing != null) Destroy(landing.gameObject);
        }

        void LateUpdate()
        {
            Vector3 pos = ball.transform.position;
            Vector3 vel = ball.Body.linearVelocity;
            bool resting = pos.y < groundHeight + VolleyBall.Radius + 0.05f && vel.sqrMagnitude < restSpeed * restSpeed;
            if (resting || ball.Body.isKinematic) { SetVisible(false); return; }

            float awareness = skills != null ? skills.Normalized(SkillType.CourtAwareness) : 0f;
            float trailTime = Mathf.Lerp(trailTimeRookie, trailTimeMaster, awareness);
            float width = Mathf.Lerp(widthRookie, widthMaster, awareness);
            float alpha = Mathf.Lerp(alphaRookie, alphaMaster, awareness);

            int count = Simulate(pos, vel, out bool landed, out Vector3 landPoint, out int trailCount, trailTime);

            // Fades a little toward the far end so the near part reads strongest.
            Draw(line, trailCount, width, color, alpha, count > 1);
            Draw(outline, trailCount, width * 1.6f, outlineColor, alpha * 0.8f, count > 1);

            landing.gameObject.SetActive(landed);
            if (landed)
            {
                landing.position = landPoint + Vector3.up * 0.02f;
                landing.localScale = Vector3.one * landingSize;
                // The texture carries the colours (blue disc, white ring); the material only fades it.
                landingMat.color = new Color(1f, 1f, 1f, Mathf.Lerp(landingAlphaRookie, landingAlphaMaster, awareness));
            }
        }

        /// <summary>Fills <see cref="points"/> with the predicted path. Returns the total sample count.</summary>
        int Simulate(Vector3 pos, Vector3 vel, out bool landed, out Vector3 landPoint, out int trailCount, float trailTime)
        {
            float g = ball.GravityMagnitude;
            float damp = 1f / (1f + ball.AirDrag * simStep);
            float floor = groundHeight + VolleyBall.Radius;
            int n = 0;
            trailCount = 0;
            landed = false;
            landPoint = pos;
            points[n++] = pos;
            for (float t = simStep; t <= maxPredictTime && n < points.Length; t += simStep)
            {
                vel.y -= g * simStep;
                vel *= damp;
                Vector3 next = pos + vel * simStep;
                if (next.y <= floor && vel.y < 0f)
                {
                    float k = Mathf.InverseLerp(pos.y, next.y, floor);
                    landPoint = Vector3.Lerp(pos, next, k);
                    landPoint.y = groundHeight;
                    points[n++] = Vector3.Lerp(pos, next, k);
                    landed = true;
                    if (t <= trailTime) trailCount = n;
                    break;
                }
                pos = next;
                points[n++] = pos;
                if (t <= trailTime) trailCount = n;
            }
            return n;
        }

        LineRenderer MakeLine(string name, Shader shader, Texture2D texture, int sortingOrder)
        {
            var lr = new GameObject(name).AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.textureMode = LineTextureMode.Tile;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(shader) { mainTexture = texture };
            lr.sortingOrder = sortingOrder;
            return lr;
        }

        void Draw(LineRenderer lr, int trailCount, float width, Color c, float alpha, bool visible)
        {
            lr.positionCount = Mathf.Max(trailCount, 2);
            lr.SetPositions(points);
            lr.widthMultiplier = width;
            // Dash length in world units stays constant whatever the width.
            lr.textureScale = new Vector2(1f / 0.35f, 1f);
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.55f, 1f) });
            lr.colorGradient = gradient;
            lr.enabled = visible;
        }

        void SetVisible(bool on)
        {
            if (line != null) line.enabled = on;
            if (outline != null) outline.enabled = on;
            if (landing != null) landing.gameObject.SetActive(on);
        }

        /// <summary>Sharp, fully opaque dash (about 70% of the tile) with a short gap.</summary>
        static Texture2D DashTexture()
        {
            const int w = 32, h = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, name = "TrailDash" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)(x < w * 0.7f ? 255 : 0));
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>Landing mark: a filled disc in the trail colour with a bold white ring.</summary>
        static Texture2D LandingTexture(Color fill)
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "LandingDisc" };
            var px = new Color32[s * s];
            Color32 disc = new Color(fill.r, fill.g, fill.b, 0.75f);
            Color32 ring = new Color(1f, 1f, 1f, 1f);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.5f);
                px[y * s + x] = d > 1f ? new Color32(0, 0, 0, 0) : d > 0.78f ? ring : disc;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
