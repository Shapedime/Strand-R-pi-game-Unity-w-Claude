using System;
using BeachVolley.Ball;
using BeachVolley.Core;
using BeachVolley.Court;
using BeachVolley.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace BeachVolley.Player
{
    /// <summary>
    /// Serve: C tosses the ball high and a little in front. While that toss is in the air the
    /// attack keys serve it (BallContact routes them here at the swing's contact beat):
    /// left mouse = power serve, F = short serve that drops just over the net. Hit it standing,
    /// or run in and jump for a jump serve. The hit window is generous; the Serve skill tightens
    /// where the ball lands.
    /// </summary>
    [RequireComponent(typeof(PlayerSkills))]
    public class PlayerServe : MonoBehaviour
    {
        [SerializeField] VolleyBall ball;

        [Header("Toss")]
        [SerializeField] float handHeight = 1.5f;
        [Tooltip("How far in front the toss goes, so you can step in and jump to it.")]
        [SerializeField] float tossForward = 0.6f;
        [Tooltip("How far above the hand the toss peaks (≈3.3 m at the top, reachable in a jump).")]
        [SerializeField] float tossHeight = 1.8f;
        [Tooltip("A toss stops counting after this long, or once it lands.")]
        [SerializeField] float tossLife = 4f;

        [Header("Hit window (generous)")]
        [SerializeField] float hitMinHeight = 1.5f;
        [SerializeField] float hitMaxHeight = 3.8f;
        [SerializeField] float hitReach = 1.3f;

        [Header("Power serve")]
        [Tooltip("Standing: aim point depth into the far half, from the net.")]
        [SerializeField] float targetDepth = 4.5f;
        [SerializeField] float serveApex = 4.3f;
        [Tooltip("Jump serve: a flatter, harder drive aimed deep.")]
        [SerializeField] float jumpServeDepth = 6f;
        [SerializeField] float jumpServeSpeed = 13f;
        [SerializeField] float jumpServeMinSpeed = 8f;
        [SerializeField] float jumpServeNetClearance = 0.3f;

        [Header("Short serve")]
        [Tooltip("Lands this far past the net (min..max), aimed sideways along the camera.")]
        [SerializeField] Vector2 shortDepth = new Vector2(1.5f, 2.5f);
        [Tooltip("Peaks this far above the net tape.")]
        [SerializeField] float shortNetApex = 0.8f;
        [SerializeField] float sidelineMargin = 0.5f;

        [Header("Error: rookie (Serve 0) → master (Serve 100)")]
        [SerializeField] float aimErrorRookie = 12f;
        [SerializeField] float aimErrorMaster = 2f;
        [SerializeField] float powerErrorRookie = 0.18f;
        [SerializeField] float powerErrorMaster = 0.03f;
        [SerializeField] float targetScatterRookie = 1.5f;
        [SerializeField] float targetScatterMaster = 0.4f;

        PlayerSkills skills;
        BallContact contact;
        PlayerMotor motor;
        InputAction tossAction;
        bool tossed;
        float tossTime;

        /// <summary>Ball was tossed up for a serve.</summary>
        public event Action Tossed;
        /// <summary>Serve was struck. Argument: +1 if served toward +Z, -1 toward -Z.</summary>
        public event Action<float> Hit;

        /// <summary>A serve toss is in the air and not yet hit: the attack keys serve it.</summary>
        public bool IsTossLive => tossed && ball != null && Time.time - tossTime <= tossLife
            && !(Time.time - tossTime > 0.3f && ball.transform.position.y < VolleyBall.Radius + 0.05f);

        void Awake()
        {
            skills = GetComponent<PlayerSkills>();
            contact = GetComponent<BallContact>();
            motor = GetComponent<PlayerMotor>();
            if (ball == null) ball = FindAnyObjectByType<VolleyBall>();

            tossAction = new InputAction("ServeToss", InputActionType.Button, "<Keyboard>/c");
            tossAction.AddBinding("<Gamepad>/leftShoulder");
        }

        void OnEnable() => tossAction.Enable();
        void OnDisable() => tossAction.Disable();
        void OnDestroy() => tossAction.Dispose();

        void Update()
        {
            if (ball == null || UiPointerGate.MenuOpen || !tossAction.WasPressedThisFrame()) return;
            // C only tosses; a toss already in the air is left alone.
            if (!IsTossLive) Toss();
        }

        void Toss()
        {
            Vector3 forward = Flat(transform.forward);
            ball.Place(transform.position + forward * tossForward + Vector3.up * handHeight);
            ball.Launch(Vector3.up * Mathf.Sqrt(2f * ball.GravityMagnitude * tossHeight));
            tossed = true;
            tossTime = Time.time;
            Tossed?.Invoke();
        }

        bool InHitWindow()
        {
            Vector3 offset = ball.transform.position - transform.position;
            return offset.y >= hitMinHeight && offset.y <= hitMaxHeight
                && new Vector2(offset.x, offset.z).sqrMagnitude <= hitReach * hitReach;
        }

        /// <summary>
        /// Power serve. Standing: a high, deep arc. Jumping: a flatter, harder drive aimed deep.
        /// Returns false (and does nothing) if the ball isn't in reach yet.
        /// </summary>
        public bool HitPower(float quality = 1f)
        {
            if (!IsTossLive || !InHitWindow()) return false;
            Vector3 origin = ball.transform.position;
            float side = FarSide();
            float scatter = skills.Scale(SkillType.Serve, targetScatterRookie, targetScatterMaster) + (1f - quality) * 1.5f;
            Vector2 jitter = Random.insideUnitCircle * scatter;

            Vector3 velocity;
            bool jumping = motor != null && !motor.IsGrounded;
            if (jumping)
            {
                float x = BallContact.AimAcross(origin, Flat(transform.forward), side, jumpServeDepth, 0.8f);
                var target = new Vector3(x + jitter.x, VolleyBall.Radius, side * jumpServeDepth + jitter.y);
                float netTop = CourtDimensions.NetHeight + VolleyBall.Radius + jumpServeNetClearance;
                // A mistimed jump serve loses its pace.
                float pace = Mathf.Lerp(jumpServeMinSpeed, jumpServeSpeed, quality) * skills.Scale(GymSkill.Strength, 1f, 1.2f);
                velocity = ball.DriveOverNet(origin, target, netTop, pace, jumpServeMinSpeed * 0.8f);
            }
            else
            {
                var target = new Vector3(jitter.x, VolleyBall.Radius, side * targetDepth + jitter.y);
                velocity = ball.VelocityToReach(origin, target, serveApex);
            }

            float aimErr = skills.Scale(SkillType.Serve, aimErrorRookie, aimErrorMaster);
            float powerErr = skills.Scale(SkillType.Serve, powerErrorRookie, powerErrorMaster) + (1f - quality) * 0.15f;
            aimErr += (1f - quality) * 6f;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            horizontal = Quaternion.Euler(0f, Random.Range(-aimErr, aimErr), 0f) * horizontal * (1f + Random.Range(-powerErr, powerErr));
            Strike(horizontal + Vector3.up * velocity.y, side);
            return true;
        }

        /// <summary>
        /// Short serve: a soft ball that just clears the net and drops 1.5–2.5 m past it, aimed
        /// sideways along the camera like the roll shot. Returns false if the ball isn't in reach yet.
        /// </summary>
        public bool HitShort(float quality = 1f)
        {
            if (!IsTossLive || !InHitWindow()) return false;
            Vector3 origin = ball.transform.position;
            float side = FarSide();
            float scatter = skills.Scale(SkillType.Serve, targetScatterRookie, targetScatterMaster) * 0.5f + (1f - quality) * 1.2f;
            Vector2 jitter = Random.insideUnitCircle * scatter;
            float depth = Mathf.Max(Random.Range(shortDepth.x, shortDepth.y) + jitter.y, 0.8f);
            Vector3 aim = contact != null ? contact.AimDirection : Flat(transform.forward);
            float x = BallContact.AimAcross(origin, aim, side, depth, sidelineMargin) + jitter.x;
            float half = CourtDimensions.HalfWidth - sidelineMargin;
            var target = new Vector3(Mathf.Clamp(x, -half, half), VolleyBall.Radius, side * depth);
            // Air drag shaves a little height off the solver's drag-free arc, hence the margin.
            float netTop = CourtDimensions.NetHeight + VolleyBall.Radius + 0.25f;
            Strike(ball.ArcOverNet(origin, target, CourtDimensions.NetHeight + shortNetApex, netTop), side);
            return true;
        }

        void Strike(Vector3 velocity, float side)
        {
            ball.Launch(velocity);
            tossed = false;
            if (contact != null) contact.PlayServeSwing();
            Hit?.Invoke(side);
        }

        /// <summary>Always serve over the net, whichever way the player happens to face.</summary>
        float FarSide() => transform.position.z <= 0f ? 1f : -1f;

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }
    }
}
