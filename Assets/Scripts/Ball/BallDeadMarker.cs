using UnityEngine;

namespace BeachVolley.Ball
{
    /// <summary>
    /// Red ring on the sand around the ball while it's dead (it has touched the sand since it was
    /// last played), so it's obvious the rally is over and the ball can't be played. Follows the
    /// ball while it rolls; disappears the moment the ball is launched or tossed again.
    /// </summary>
    [RequireComponent(typeof(VolleyBall))]
    public class BallDeadMarker : MonoBehaviour
    {
        [SerializeField] Color color = new Color(0.95f, 0.12f, 0.1f);
        [Tooltip("Ring diameter on the sand (m).")]
        [SerializeField] float size = 0.95f;

        VolleyBall ball;
        Transform ring;

        void Awake()
        {
            ball = GetComponent<VolleyBall>();
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "BallDeadMarker";
            Destroy(go.GetComponent<Collider>());
            ring = go.transform;
            ring.rotation = Quaternion.Euler(90f, 0f, 0f);
            ring.localScale = Vector3.one * size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = RingTexture(color) };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
        }

        void OnDisable()
        {
            if (ring != null) ring.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (ring != null) Destroy(ring.gameObject);
        }

        void LateUpdate()
        {
            bool show = ball.IsDead && !ball.Body.isKinematic;
            if (ring.gameObject.activeSelf != show) ring.gameObject.SetActive(show);
            if (show)
            {
                Vector3 p = ball.transform.position;
                ring.position = new Vector3(p.x, 0.015f, p.z);
            }
        }

        /// <summary>A bold ring with a clear centre, so the ball itself stays visible inside it.</summary>
        static Texture2D RingTexture(Color c)
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "DeadBallRing" };
            var px = new Color32[s * s];
            Color32 on = new Color(c.r, c.g, c.b, 1f);
            Color32 off = new Color32(0, 0, 0, 0);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.5f);
                px[y * s + x] = d <= 1f && d >= 0.74f ? on : off;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
