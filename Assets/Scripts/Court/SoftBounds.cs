using UnityEngine;

namespace BeachVolley.Court
{
    /// <summary>
    /// Soft edge of the beach patch. Past the soft line, outward movement is scaled down
    /// to zero over <see cref="slowDistance"/> and a warning shows; movement back toward
    /// the court is never touched. The hard walls at the sand edge are only a safety net.
    /// </summary>
    public class SoftBounds : MonoBehaviour
    {
        [Tooltip("Half extents (X, Z) from this transform where slowing starts.")]
        [SerializeField] Vector2 softHalfExtents = new Vector2(24f, 24f);
        [Tooltip("Distance past the soft line at which outward speed reaches zero.")]
        [SerializeField] float slowDistance = 4.5f;
        [SerializeField] string warning = "Too far from the court - head back";

        float lastWarnTime = float.NegativeInfinity;
        GUIStyle style;

        /// <summary>Returns the planar velocity with its outward components damped near the edge.</summary>
        public Vector3 LimitOutward(Vector3 position, Vector3 planarVelocity)
        {
            Vector3 local = position - transform.position;
            bool past = false;
            planarVelocity.x = Damp(local.x, softHalfExtents.x, planarVelocity.x, ref past);
            planarVelocity.z = Damp(local.z, softHalfExtents.y, planarVelocity.z, ref past);
            if (past) lastWarnTime = Time.time;
            return planarVelocity;
        }

        float Damp(float offset, float halfExtent, float velocity, ref bool past)
        {
            float depth = Mathf.Abs(offset) - halfExtent;
            if (depth <= 0f) return velocity;
            past = true;
            bool outward = Mathf.Sign(offset) == Mathf.Sign(velocity);
            return outward ? velocity * (1f - Mathf.Clamp01(depth / slowDistance)) : velocity;
        }

        void OnGUI()
        {
            if (Time.time - lastWarnTime > 0.15f) return;
            style ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.9f, 0.6f) },
            };
            GUI.Label(new Rect(0f, Screen.height * 0.12f, Screen.width, 40f), warning, style);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            Gizmos.DrawWireCube(transform.position, new Vector3(softHalfExtents.x * 2f, 0.1f, softHalfExtents.y * 2f));
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3((softHalfExtents.x + slowDistance) * 2f, 0.1f, (softHalfExtents.y + slowDistance) * 2f));
        }
    }
}
