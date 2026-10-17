using UnityEngine;

namespace BeachVolley.Ball
{
    /// <summary>
    /// Simple, readable volleyball flight: slightly reduced gravity for a floaty arc,
    /// light air drag, and heavy rolling drag once it's sitting on the sand.
    /// Bounce and friction come from the physics materials.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VolleyBall : MonoBehaviour
    {
        /// <summary>32 cm across, about 1.5x an official ball (21 cm) so it reads from the camera.</summary>
        public const float Radius = 0.16f;

        [Tooltip("Multiplier on Physics.gravity. Below 1 gives a floatier, easier-to-read arc.")]
        [SerializeField] float gravityScale = 0.66f;
        [SerializeField] float airDrag = 0.05f;
        [Tooltip("Linear damping while resting/rolling on a floor-like surface (sand).")]
        [SerializeField] float groundDrag = 2.5f;
        [SerializeField] float groundAngularDrag = 3f;
        [SerializeField] float airAngularDrag = 0.2f;
        [SerializeField] float maxSpeed = 25f;

        Rigidbody body;
        bool touchingGround;

        public Rigidbody Body => body;
        /// <summary>Downward acceleration actually applied to the ball (positive, m/s²).</summary>
        public float GravityMagnitude => -Physics.gravity.y * gravityScale;
        /// <summary>Linear damping while flying, for anything that predicts the flight.</summary>
        public float AirDrag => airDrag;

        /// <summary>
        /// The rally is over for this ball: it has touched the sand (or was placed on it) since it
        /// was last launched. Auto-facing and the cocked arm ignore a dead ball.
        /// </summary>
        public bool IsDead { get; private set; } = true;

        /// <summary>Raised the first time a live ball touches the sand. Argument: where it landed.</summary>
        public event System.Action<Vector3> Landed;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
        }

        void FixedUpdate()
        {
            body.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
            body.linearDamping = touchingGround ? groundDrag : airDrag;
            body.angularDamping = touchingGround ? groundAngularDrag : airAngularDrag;
            if (body.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
                body.linearVelocity = body.linearVelocity.normalized * maxSpeed;
            touchingGround = false;
        }

        void OnCollisionEnter(Collision collision) => OnCollisionStay(collision);

        void OnCollisionStay(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                // Floor-like and low: the sand, not the top of a player's head.
                if (contact.normal.y > 0.7f && contact.point.y < 0.15f)
                {
                    touchingGround = true;
                    if (!IsDead)
                    {
                        IsDead = true;
                        Landed?.Invoke(contact.point);
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Launch velocity that carries the ball from <paramref name="origin"/> to <paramref name="target"/>
        /// peaking at <paramref name="apexHeight"/> (world Y), under this ball's gravity.
        /// </summary>
        /// <summary>
        /// A hard, flat drive from <paramref name="origin"/> to <paramref name="target"/> that still
        /// clears <paramref name="netTop"/> where it crosses the net plane (z = 0). Starts at
        /// <paramref name="maxSpeed"/> horizontal and backs off toward <paramref name="minSpeed"/>
        /// until the flight clears, so a hit from deep still goes over.
        /// </summary>
        public Vector3 DriveOverNet(Vector3 origin, Vector3 target, float netTop, float maxSpeed, float minSpeed)
        {
            float g = GravityMagnitude;
            Vector3 flat = new Vector3(target.x - origin.x, 0f, target.z - origin.z);
            float dist = Mathf.Max(flat.magnitude, 0.01f);
            Vector3 dir = flat / dist;
            // Fraction of the flight at which the ball crosses the net plane.
            float netFrac = Mathf.Abs(origin.z) / Mathf.Max(Mathf.Abs(target.z - origin.z), 0.01f);

            Vector3 velocity = Vector3.zero;
            for (float h = maxSpeed; h >= minSpeed - 0.01f; h -= 0.5f)
            {
                float t = dist / h;
                float vy = (target.y - origin.y + 0.5f * g * t * t) / t;
                float tn = t * netFrac;
                velocity = dir * h + Vector3.up * vy;
                if (origin.y + vy * tn - 0.5f * g * tn * tn >= netTop) break;
            }
            return velocity;
        }

        /// <summary>
        /// A soft lob from <paramref name="origin"/> to <paramref name="target"/> that peaks at least at
        /// <paramref name="minApex"/> and is still above <paramref name="netTop"/> where it crosses the
        /// net plane (z = 0). Raises the apex until the simulated flight (with this ball's drag) does:
        /// a short ball from deep needs a higher arc because its peak sits near the hitter.
        /// </summary>
        public Vector3 ArcOverNet(Vector3 origin, Vector3 target, float minApex, float netTop)
        {
            float apex = Mathf.Max(minApex, origin.y + 0.2f, target.y + 0.2f);
            Vector3 velocity = VelocityToReach(origin, target, apex);
            if (Mathf.Sign(origin.z) == Mathf.Sign(target.z)) return velocity;
            for (int i = 0; i < 40 && NetCrossingHeight(origin, velocity) < netTop; i++)
            {
                apex += 0.15f;
                velocity = VelocityToReach(origin, target, apex);
            }
            return velocity;
        }

        /// <summary>
        /// Seconds until the flying ball next comes down through <paramref name="height"/> (world Y),
        /// predicted from its current position and velocity with the same flight model as FixedUpdate.
        /// If it is already below that height and falling, returns how long ago it passed (negative).
        /// False if it won't get there within <paramref name="maxTime"/>.
        /// </summary>
        public bool PredictTimeToHeight(float height, float maxTime, out float time)
        {
            Vector3 p = transform.position, v = body != null ? body.linearVelocity : Vector3.zero;
            if (p.y < height && v.y < 0f)
            {
                time = -(height - p.y) / Mathf.Max(-v.y, 0.5f);
                return true;
            }
            const float step = 0.01f;
            float g = GravityMagnitude;
            for (float t = 0f; t < maxTime; t += step)
            {
                Vector3 next = p + v * step;
                if (v.y < 0f && p.y >= height && next.y < height)
                {
                    time = t + step * Mathf.InverseLerp(p.y, next.y, height);
                    return true;
                }
                v.y -= g * step;
                v *= 1f / (1f + airDrag * step);
                p = next;
                if (p.y < Radius) break;
            }
            time = 0f;
            return false;
        }

        /// <summary>Where the flying ball will be in <paramref name="time"/> seconds (same flight model, sand ignored).</summary>
        public Vector3 PredictPositionAt(float time)
        {
            Vector3 p = transform.position, v = body != null ? body.linearVelocity : Vector3.zero;
            const float step = 0.01f;
            float g = GravityMagnitude;
            for (float t = 0f; t < time; t += step)
            {
                float dt = Mathf.Min(step, time - t);
                p += v * dt;
                v.y -= g * dt;
                v *= 1f / (1f + airDrag * dt);
            }
            return p;
        }

        /// <summary>Where the flying ball will touch the sand, predicted from its current state.</summary>
        public Vector3 PredictLanding(float maxTime = 6f)
        {
            Vector3 p = transform.position, v = body != null ? body.linearVelocity : Vector3.zero;
            const float step = 0.01f;
            float g = GravityMagnitude;
            for (float t = 0f; t < maxTime && p.y > Radius; t += step)
            {
                p += v * step;
                v.y -= g * step;
                v *= 1f / (1f + airDrag * step);
            }
            p.y = 0f;
            return p;
        }

        /// <summary>Height at which a ball launched from <paramref name="origin"/> crosses z = 0, same flight model as FixedUpdate.</summary>
        float NetCrossingHeight(Vector3 origin, Vector3 velocity)
        {
            const float step = 0.01f;
            float g = GravityMagnitude;
            Vector3 p = origin, v = velocity;
            for (float t = 0f; t < 6f; t += step)
            {
                Vector3 next = p + v * step;
                if (Mathf.Sign(next.z) != Mathf.Sign(p.z)) return next.y;
                v.y -= g * step;
                v *= 1f / (1f + airDrag * step);
                p = next;
                if (p.y < 0f) break;
            }
            return float.NegativeInfinity;
        }

        public Vector3 VelocityToReach(Vector3 origin, Vector3 target, float apexHeight)
        {
            float g = GravityMagnitude;
            float apex = Mathf.Max(apexHeight, origin.y + 0.2f, target.y + 0.2f);
            float vy = Mathf.Sqrt(2f * g * (apex - origin.y));
            float flightTime = vy / g + Mathf.Sqrt(2f * (apex - target.y) / g);
            Vector3 horizontal = new Vector3(target.x - origin.x, 0f, target.z - origin.z) / flightTime;
            return horizontal + Vector3.up * vy;
        }

        /// <summary>Teleports the ball and kills all motion.</summary>
        public void Place(Vector3 position)
        {
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            touchingGround = false;
            // Placed on the sand = a dead ball; placed in the air (a toss) stays live until it lands.
            IsDead = position.y < Radius + 0.05f;
        }

        /// <summary>
        /// Sets the ball's velocity outright, with a little tumble so it reads as moving. A launch
        /// puts the ball back in play unless <paramref name="revive"/> is false (e.g. kicking a dead
        /// ball along the sand).
        /// </summary>
        public void Launch(Vector3 velocity, bool revive = true)
        {
            body.linearVelocity = velocity;
            body.angularVelocity = Vector3.Cross(Vector3.up, velocity).normalized * Mathf.Min(velocity.magnitude, 8f);
            touchingGround = false;
            if (revive) IsDead = false;
        }
    }
}
