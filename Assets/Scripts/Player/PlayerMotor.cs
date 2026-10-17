using BeachVolley.Court;
using BeachVolley.Skills;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeachVolley.Player
{
    /// <summary>
    /// Movement on sand: a hint of drag when starting and turning, a spike-height jump,
    /// an optional sprint and a dive (dash, layout on the sand, get up). Movement is
    /// relative to the camera's yaw.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Ground (sand)")]
        [SerializeField] float walkSpeed = 4f;
        [SerializeField] float sprintSpeed = 6f;
        [Tooltip("How fast speed builds up.")]
        [SerializeField] float acceleration = 16f;
        [Tooltip("How fast speed bleeds off when releasing the stick.")]
        [SerializeField] float deceleration = 20f;
        [Tooltip("Braking when the input points against the current run: plant and go the other way.")]
        [SerializeField] float reverseDeceleration = 35f;
        [Tooltip("After a plant, keep pushing off at the plant rate for this long (s).")]
        [SerializeField] float reversePushTime = 0.2f;
        [Tooltip("Speed multiplier when moving opposite to where the body faces (eases to 1 when aligned). 1 = no penalty.")]
        [SerializeField, Range(0.2f, 1f)] float misalignedSpeedFactor = 1f;

        [Header("Turning")]
        [SerializeField] float turnSmoothTime = 0.05f;
        [SerializeField] float maxTurnSpeed = 1440f;
        [Tooltip("Turns bigger than this (degrees) pivot at a constant fast rate; only the last bit eases in.")]
        [SerializeField] float pivotAngle = 15f;
        [SerializeField] float pivotTurnSpeed = 2400f;

        [Header("Jump")]
        [Tooltip("Jump height from a standstill (m).")]
        [SerializeField] float standingJumpHeight = 0.7f;
        [Tooltip("Jump height off a full approach run (m): a real spike approach jumps higher.")]
        [SerializeField] float approachJumpHeight = 0.95f;
        [Tooltip("Run speed (m/s) at takeoff that counts as a full approach.")]
        [SerializeField] float fullApproachSpeed = 4f;
        [SerializeField] float gravity = -20f;
        [Tooltip("Fraction of ground acceleration available in the air.")]
        [SerializeField, Range(0f, 1f)] float airControl = 0.3f;
        [SerializeField] float coyoteTime = 0.1f;
        [SerializeField] float jumpBufferTime = 0.12f;

        [Header("Landing")]
        [SerializeField] float landingRecoveryTime = 0.2f;
        [SerializeField, Range(0f, 1f)] float landingSpeedFactor = 0.6f;
        [Tooltip("Falls faster than this (m/s) trigger landing recovery.")]
        [SerializeField] float hardLandingSpeed = 3f;

        [Header("Footwork skill (values above are the rookie/0 baseline)")]
        [Tooltip("Acceleration and deceleration multiplier at Footwork 100.")]
        [SerializeField] float footworkAccelAtMax = 1.5f;
        [Tooltip("Turn smoothing time at Footwork 100 (lower = snappier).")]
        [SerializeField] float footworkTurnTimeAtMax = 0.035f;
        [Tooltip("Walk/sprint speed multiplier at Footwork 100.")]
        [SerializeField] float footworkSpeedAtMax = 1.12f;

        [Header("Dive")]
        [SerializeField] float diveSpeed = 7f;
        [Tooltip("Flat-out dash before the body hits the sand.")]
        [SerializeField] float diveDashTime = 0.3f;
        [Tooltip("Lying on the sand, arms still out (the dig can still happen here).")]
        [SerializeField] float diveLayoutTime = 0.3f;
        [Tooltip("Getting back up. No movement until it ends.")]
        [SerializeField] float diveGetUpTime = 1f;
        [SerializeField] float diveSandDrag = 14f;
        [Tooltip("Most the dive can drift sideways toward the ball's landing point (m), at the default lock-on.")]
        [SerializeField] float diveAssistMaxShift = 0.6f;

        [Header("Face the ball")]
        [Tooltip("Turn rate when a hit snaps the body toward the ball.")]
        [SerializeField] float faceTurnSpeed = 1080f;
        [Header("Attack helper (in the air)")]
        [Tooltip("Fastest sideways speed the helper steers you at in the air (m/s).")]
        [SerializeField] float airAssistMaxSpeed = 5f;
        [SerializeField] float airAssistAccel = 30f;
        [SerializeField] float airAssistTurnSpeed = 900f;

        [Tooltip("Turn rate of the gentle auto-face while standing (nearly) still.")]
        [SerializeField] float softFaceSpeed = 600f;
        [Tooltip("Below this stick deflection the player counts as standing, and auto-faces the ball.")]
        [SerializeField] float softFaceMaxInput = 0.3f;

        [Tooltip("Movement is relative to this transform's yaw. Defaults to Camera.main.")]
        [SerializeField] Transform cameraTransform;

        CharacterController controller;
        PlayerSkills skills;
        SoftBounds bounds;
        InputAction moveAction;
        InputAction sprintAction;
        InputAction jumpAction;
        Vector3 planarVelocity;
        float verticalVelocity;
        float yawVelocity;
        float lastGroundedTime = float.NegativeInfinity;
        float lastJumpPressedTime = float.NegativeInfinity;
        float landingTimer;
        bool wasGrounded;
        float diveStart = float.NegativeInfinity;
        Vector3 diveDir;
        // Optional landing point the dive drifts sideways toward during the dash (a small lock-on).
        Vector3? diveAssist;
        float diveAssistShift;
        float diveAssistMax;
        float faceYaw;
        float faceUntil = float.NegativeInfinity;
        Vector3? softFacePoint;
        Vector3? airAssistTarget;
        float airAssistTime;
        Vector3 airAssistFace = Vector3.forward;
        float lastReverseTime = float.NegativeInfinity;

        /// <summary>Raw stick/WASD input this frame (x = strafe, y = forward).</summary>
        public Vector2 MoveInput { get; private set; }
        public Vector3 PlanarVelocity => planarVelocity;
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        /// <summary>How high a jump started right now would go: more run-up, higher jump.</summary>
        public float JumpHeightNow => AttackJumpBoost ? approachJumpHeight : Mathf.Lerp(standingJumpHeight, approachJumpHeight,
            Mathf.InverseLerp(1f, fullApproachSpeed, planarVelocity.magnitude));

        /// <summary>Upward speed (m/s), negative while falling.</summary>
        public float VerticalVelocity => verticalVelocity;

        float DiveElapsed => Time.time - diveStart;
        float DiveTotal => diveDashTime + diveLayoutTime + diveGetUpTime;
        /// <summary>From launch until the player is back on their feet.</summary>
        public bool IsDiving => DiveElapsed < DiveTotal;
        /// <summary>Dash and layout: the window where a dig can still be played.</summary>
        public bool IsDiveReaching => DiveElapsed < diveDashTime + diveLayoutTime;
        public bool IsGettingUp => IsDiving && !IsDiveReaching;

        /// <summary>0 = upright, 1 = laid out on the sand. Rises through the dash, eases back during the get-up.</summary>
        public float DivePose
        {
            get
            {
                float t = DiveElapsed;
                if (t >= DiveTotal) return 0f;
                if (t < diveDashTime) return Mathf.SmoothStep(0f, 1f, t / diveDashTime);
                if (t < diveDashTime + diveLayoutTime) return 1f;
                return 1f - Mathf.SmoothStep(0f, 1f, (t - diveDashTime - diveLayoutTime) / diveGetUpTime);
            }
        }

        /// <summary>Launches a dive along <paramref name="direction"/>. Ignored in the air or mid-dive.</summary>
        public bool StartDive(Vector3 direction, Vector3? assistPoint = null, float assistStrength = 0f)
        {
            if (IsDiving || !IsGrounded) return false;
            direction.y = 0f;
            diveDir = direction.sqrMagnitude > 1e-4f ? direction.normalized : transform.forward;
            diveAssist = assistPoint;
            diveAssistShift = 0f;
            diveAssistMax = diveAssistMaxShift * Mathf.Clamp01(assistStrength / 0.2f);
            diveStart = Time.time;
            transform.rotation = Quaternion.LookRotation(diveDir);
            planarVelocity = diveDir * diveSpeed;
            faceUntil = float.NegativeInfinity;
            return true;
        }

        /// <summary>Turns the body quickly toward <paramref name="point"/> for a moment, so a hit reads.</summary>
        public void FaceTowards(Vector3 point, float duration = 0.2f)
        {
            if (IsDiving) return;
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            faceYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            faceUntil = Time.time + duration;
        }

        /// <summary>
        /// A point to turn toward while standing (nearly) still, e.g. the ball. Null clears it.
        /// Moving always turns toward the move direction instead, and a hit snap overrides it.
        /// </summary>
        public void SetSoftFacing(Vector3? point) => softFacePoint = point;

        /// <summary>Downward acceleration on the player (positive, m/s²), for predicting the jump.</summary>
        public float Gravity => -gravity;

        /// <summary>Set by the attack helper: a jump started now gets the full approach height.</summary>
        public bool AttackJumpBoost { get; set; }

        /// <summary>
        /// Attack helper, while airborne: steer so the feet arrive over <paramref name="feetTarget"/>
        /// in <paramref name="timeLeft"/> seconds and face <paramref name="face"/>. Null clears it.
        /// </summary>
        public void SetAirAssist(Vector3? feetTarget, float timeLeft, Vector3 face)
        {
            airAssistTarget = feetTarget;
            airAssistTime = timeLeft;
            face.y = 0f;
            if (face.sqrMagnitude > 1e-4f) airAssistFace = face.normalized;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            skills = GetComponent<PlayerSkills>();
            bounds = FindAnyObjectByType<SoftBounds>();
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");

            sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            sprintAction.AddBinding("<Gamepad>/leftStickPress");

            jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");
        }

        void OnEnable()
        {
            moveAction.Enable();
            sprintAction.Enable();
            jumpAction.Enable();
        }

        void OnDisable()
        {
            moveAction.Disable();
            sprintAction.Disable();
            jumpAction.Disable();
        }

        void OnDestroy()
        {
            moveAction.Dispose();
            sprintAction.Dispose();
            jumpAction.Dispose();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float now = Time.time;
            Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
            MoveInput = input;

            bool grounded = controller.isGrounded;
            IsGrounded = grounded;
            if (grounded)
            {
                lastGroundedTime = now;
                if (!wasGrounded && verticalVelocity < -hardLandingSpeed)
                    landingTimer = landingRecoveryTime;
            }
            if (jumpAction.WasPressedThisFrame() && !IsDiving)
                lastJumpPressedTime = now;

            if (IsDiving)
            {
                UpdateDive(dt, grounded);
                wasGrounded = grounded;
                return;
            }

            Vector3 moveDir = CameraRelative(input);
            bool moving = moveDir.sqrMagnitude > 0.0001f;
            IsSprinting = moving && sprintAction.IsPressed();

            // Better footwork = quicker to get going, quicker to turn, a touch faster.
            float accelScale = SkillScale(1f, footworkAccelAtMax);
            float turnTime = SkillScale(turnSmoothTime, footworkTurnTimeAtMax);
            float speedScale = SkillScale(1f, footworkSpeedAtMax);

            // Facing priority: a hit snap toward the ball, then the move direction, then (standing
            // still) a gentle turn toward the ball.
            bool standing = input.magnitude < softFaceMaxInput;
            float currentYaw = transform.eulerAngles.y;
            if (!grounded && airAssistTarget.HasValue)
            {
                // Attack helper: face the attack direction so the swing reads and aims right.
                TurnAt(Mathf.Atan2(airAssistFace.x, airAssistFace.z) * Mathf.Rad2Deg, airAssistTurnSpeed, dt);
            }
            else if (now < faceUntil)
            {
                TurnAt(faceYaw, faceTurnSpeed, dt);
            }
            else if (moving && !(standing && softFacePoint.HasValue))
            {
                float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, targetYaw)) > pivotAngle)
                {
                    // Big turn: pivot at a constant fast rate; SmoothDamp only eases the last few degrees.
                    TurnAt(targetYaw, pivotTurnSpeed, dt);
                }
                else
                {
                    float yaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, turnTime, maxTurnSpeed);
                    transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }
            else if (standing && softFacePoint.HasValue)
            {
                Vector3 d = softFacePoint.Value - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.04f)
                    TurnAt(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, softFaceSpeed, dt);
            }

            float speed = (IsSprinting ? sprintSpeed : walkSpeed) * speedScale;
            if (moving)
            {
                // Moving where you're not facing is slow until the body comes round.
                float alignment = Vector3.Dot(transform.forward, moveDir.normalized);
                speed *= Mathf.Lerp(misalignedSpeedFactor, 1f, (alignment + 1f) * 0.5f);
            }
            if (landingTimer > 0f)
            {
                landingTimer -= dt;
                speed *= landingSpeedFactor;
            }

            Vector3 desired = moveDir * speed;
            bool reversing = moving && Vector3.Dot(desired, planarVelocity) < 0f;
            if (reversing) lastReverseTime = now;
            bool pushingOff = moving && now - lastReverseTime < reversePushTime;
            bool slowing = desired.sqrMagnitude < planarVelocity.sqrMagnitude;
            float rate = (reversing || pushingOff ? reverseDeceleration : slowing ? deceleration : acceleration) * accelScale;
            if (!grounded) rate *= airControl;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desired, rate * dt);
            if (grounded) airAssistTarget = null;
            else if (airAssistTarget.HasValue)
            {
                // Attack helper: carry the player over the spot where the ball will meet the hand.
                Vector3 to = airAssistTarget.Value - transform.position;
                to.y = 0f;
                Vector3 assist = Vector3.ClampMagnitude(to / Mathf.Max(airAssistTime, 0.08f), airAssistMaxSpeed);
                planarVelocity = Vector3.MoveTowards(planarVelocity, assist, airAssistAccel * dt);
            }

            if (bounds != null)
                planarVelocity = bounds.LimitOutward(transform.position, planarVelocity);

            if (grounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            bool canJump = now - lastGroundedTime <= coyoteTime && verticalVelocity <= 0f;
            if (canJump && now - lastJumpPressedTime <= jumpBufferTime)
            {
                verticalVelocity = Mathf.Sqrt(2f * JumpHeightNow * -gravity);
                lastJumpPressedTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
            }
            verticalVelocity += gravity * dt;

            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            wasGrounded = grounded;
        }

        void TurnAt(float targetYaw, float degreesPerSecond, float dt)
        {
            float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetYaw, degreesPerSecond * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            yawVelocity = 0f;
        }

        void UpdateDive(float dt, bool grounded)
        {
            float t = DiveElapsed;
            if (t < diveDashTime)
            {
                planarVelocity = diveDir * diveSpeed * Mathf.Lerp(1f, 0.7f, t / diveDashTime);
                if (diveAssist.HasValue && diveAssistShift < diveAssistMax)
                {
                    // Drift sideways toward the landing point, capped to a small total shift.
                    Vector3 side = Vector3.Cross(Vector3.up, diveDir);
                    float offset = Vector3.Dot(diveAssist.Value - transform.position, side);
                    float remaining = Mathf.Max(diveDashTime - t, 0.05f);
                    float lateral = Mathf.Clamp(offset / remaining, -diveAssistMax / diveDashTime * 2f, diveAssistMax / diveDashTime * 2f);
                    float step = Mathf.Min(Mathf.Abs(lateral) * dt, diveAssistMax - diveAssistShift);
                    diveAssistShift += step;
                    planarVelocity += side * (Mathf.Sign(lateral) * step / Mathf.Max(dt, 1e-4f));
                }
            }
            else
                planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, diveSandDrag * dt);

            if (bounds != null)
                planarVelocity = bounds.LimitOutward(transform.position, planarVelocity);
            if (grounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
        }

        float SkillScale(float atZero, float atMax) =>
            skills != null ? skills.Scale(SkillType.Footwork, atZero, atMax) : atZero;

        Vector3 CameraRelative(Vector2 input)
        {
            Vector3 forward = Vector3.forward;
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f)
                    forward = transform.forward;
                forward.Normalize();
            }
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return forward * input.y + right * input.x;
        }
    }
}
