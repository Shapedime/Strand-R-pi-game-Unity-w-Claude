using System;
using BeachVolley.Story;
using UnityEngine;

namespace BeachVolley.Match
{
    /// <summary>How an NPC plays: levels 1–10 like the player's skills, plus a few habits.</summary>
    [Serializable]
    public class NpcProfile
    {
        [Range(1, 10)] public int passing = 4;
        [Range(1, 10)] public int setting = 4;
        [Range(1, 10)] public int attacking = 4;
        [Range(1, 10)] public int serving = 4;
        [Range(1, 10)] public int defense = 4;
        [Range(1, 10)] public int blocking = 3;
        [Range(1, 10)] public int speed = 4;
        [Tooltip("Goes up to the net to block the other team's attack.")]
        public bool blocks;
        [Tooltip("Calls the ball early ('Mine!' / 'Yours!'). Partners who don't are harder to play with.")]
        public bool callsEarly = true;
        [Tooltip("Chance a serve goes at the weaker receiver (the protagonist).")]
        [Range(0f, 1f)] public float serveAtWeakest = 0.35f;
        public string callMine = "Mine!";
        public string callYours = "Yours!";

        public static float N(int level) => (Mathf.Clamp(level, 1, 10) - 1) / 9f;

        public NpcProfile Scaled(float bias)
        {
            int S(int v) => Mathf.Clamp(Mathf.RoundToInt(v * bias), 1, 10);
            var p = (NpcProfile)MemberwiseClone();
            p.passing = S(passing); p.setting = S(setting); p.attacking = S(attacking); p.serving = S(serving);
            p.defense = S(defense); p.blocking = S(blocking); p.speed = S(speed);
            return p;
        }
    }

    /// <summary>
    /// The body of an NPC in a match: runs to where <see cref="StoryMatch"/> tells it, faces the
    /// ball, jumps for spikes and blocks, lunges for dives. All decisions are the match's; the
    /// StoryActor on the same object plays the animations from the real movement.
    /// </summary>
    [RequireComponent(typeof(StoryActor))]
    public class CourtAI : MonoBehaviour
    {
        public NpcProfile profile = new NpcProfile();

        [SerializeField] float accel = 18f;
        [Tooltip("Run speed at speed 1 .. speed 10 (m/s).")]
        [SerializeField] Vector2 runSpeed = new Vector2(3.6f, 5.6f);

        Vector3 target;
        bool hasTarget;
        Vector3 velocity;
        Vector3? facePoint;
        float jumpStart = float.NegativeInfinity, jumpHeight, jumpDuration;
        float lungeUntil;
        Vector3 lungeVelocity;

        public StoryActor Actor { get; private set; }
        public string DisplayName => Actor != null && !string.IsNullOrEmpty(Actor.displayName) ? Actor.displayName : name;

        /// <summary>Profile in use this match (the authored one, maybe scaled for difficulty).</summary>
        public NpcProfile Live { get; set; }

        public float MaxSpeed => Mathf.Lerp(runSpeed.x, runSpeed.y, NpcProfile.N((Live ?? profile).speed));
        public bool IsAirborne => Time.time - jumpStart < jumpDuration;
        public bool IsLunging => Time.time < lungeUntil;
        public Vector3 Ground => new Vector3(transform.position.x, 0f, transform.position.z);

        void Awake()
        {
            Actor = GetComponent<StoryActor>();
            Live = profile;
        }

        /// <summary>Takes the NPC over for a match: no colliders in the ball's way, no scripted walks.</summary>
        public void BeginMatch()
        {
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (Actor != null) { Actor.Stop(); Actor.LookAt(null); }
            enabled = true;
        }

        public void EndMatch()
        {
            hasTarget = false;
            facePoint = null;
            velocity = Vector3.zero;
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, 0f, p.z);
            enabled = false;
        }

        public void MoveTo(Vector3 point)
        {
            target = new Vector3(point.x, 0f, point.z);
            hasTarget = true;
        }

        public void Hold() => hasTarget = false;

        public void Face(Vector3? point) => facePoint = point;

        public void Teleport(Vector3 at, Vector3 lookAt)
        {
            transform.position = new Vector3(at.x, 0f, at.z);
            Vector3 d = lookAt - at;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d);
            target = transform.position;
            hasTarget = false;
            velocity = Vector3.zero;
        }

        public void Jump(float height, float duration)
        {
            if (IsAirborne) return;
            jumpStart = Time.time;
            jumpHeight = height;
            jumpDuration = duration;
        }

        /// <summary>A dive: a quick lunge toward <paramref name="point"/>.</summary>
        public void Lunge(Vector3 point, float duration = 0.3f)
        {
            Vector3 d = point - Ground;
            d.y = 0f;
            lungeVelocity = d / Mathf.Max(duration, 0.05f);
            lungeUntil = Time.time + duration;
            if (Actor != null) Actor.Volley(Player.HitType.Dive);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 pos = Ground;

            if (IsLunging) velocity = lungeVelocity;
            else if (hasTarget && !IsAirborne)
            {
                Vector3 to = target - pos;
                float dist = to.magnitude;
                // Arrive: slow down over the last bit so they don't overshoot.
                float want = Mathf.Min(MaxSpeed, dist * 4f);
                Vector3 desired = dist > 0.03f ? to / dist * want : Vector3.zero;
                velocity = Vector3.MoveTowards(velocity, desired, accel * dt);
            }
            else if (!IsAirborne) velocity = Vector3.MoveTowards(velocity, Vector3.zero, accel * dt);

            pos += velocity * dt;
            float y = 0f;
            if (IsAirborne)
            {
                float k = (Time.time - jumpStart) / jumpDuration;
                y = jumpHeight * Mathf.Sin(Mathf.PI * Mathf.Clamp01(k));
            }
            transform.position = new Vector3(pos.x, y, pos.z);

            Vector3 face = facePoint.HasValue ? facePoint.Value - pos : velocity;
            face.y = 0f;
            if (face.sqrMagnitude > 1e-3f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), 720f * dt);
        }
    }
}
