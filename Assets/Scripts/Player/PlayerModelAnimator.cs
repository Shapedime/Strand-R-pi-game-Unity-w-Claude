using UnityEngine;

namespace BeachVolley.Player
{
    /// <summary>
    /// Procedural motion for the player's static character mesh: lean into acceleration,
    /// a run bob and sway, squash and stretch on jumps and landings, a low crouch while
    /// bumping, an upward reach while setting/serving, a whip forward on a spike and a
    /// layout on a dive. Only touches the visual model transform. Fallback for an unrigged
    /// mesh: PlayerAnimatorDriver switches this off once a humanoid Animator drives the body.
    /// </summary>
    public class PlayerModelAnimator : MonoBehaviour
    {
        [Tooltip("The visual child to animate. Never the CharacterController root.")]
        [SerializeField] Transform model;

        [Header("Lean")]
        [Tooltip("Degrees of forward lean per m/s of forward speed.")]
        [SerializeField] float leanPerSpeed = 2.2f;
        [Tooltip("Degrees of lean per m/s² of acceleration (forward and sideways).")]
        [SerializeField] float leanPerAccel = 0.9f;
        [SerializeField] float maxLean = 14f;
        [SerializeField] float leanSmoothing = 10f;

        [Header("Run bob")]
        [SerializeField] float strideLength = 1.1f;
        [SerializeField] float bobHeight = 0.045f;
        [SerializeField] float swayAngle = 3.5f;

        [Header("Squash & stretch")]
        [SerializeField] float jumpStretch = 0.08f;
        [SerializeField] float landSquash = 0.14f;
        [SerializeField] float squashRecovery = 9f;

        [Header("Volley poses")]
        [SerializeField] float bumpCrouch = 0.16f;
        [SerializeField] float bumpLean = 16f;
        [SerializeField] float serveReach = 0.06f;
        [SerializeField] float serveLeanBack = -8f;
        [SerializeField] float setReach = 0.08f;
        [SerializeField] float setLeanBack = -6f;
        [SerializeField] float spikeLean = 22f;
        [Tooltip("Forward pitch when laid out on the sand.")]
        [SerializeField] float diveLayoutPitch = 80f;
        [Tooltip("The pivot is at the feet, so the body already lies near the sand; this only settles it.")]
        [SerializeField] float diveDrop = 0.1f;

        PlayerMotor motor;
        BallContact contact;
        Vector3 baseLocalPos;
        Quaternion baseLocalRot;
        Vector3 baseScale;
        Vector3 lastVelocity;
        Vector2 lean;
        float stridePhase;
        float runBlend;
        float squash;
        bool wasGrounded = true;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            contact = GetComponent<BallContact>();
            if (model == null) model = transform.Find("Model");
            if (model == null) { enabled = false; return; }
            baseLocalPos = model.localPosition;
            baseLocalRot = model.localRotation;
            baseScale = model.localScale;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || motor == null) return;

            Vector3 velocity = motor.PlanarVelocity;
            Vector3 accel = (velocity - lastVelocity) / dt;
            lastVelocity = velocity;

            // Local frame: x = right, y = forward.
            float fwdSpeed = Vector3.Dot(velocity, transform.forward);
            Vector2 localAccel = new Vector2(Vector3.Dot(accel, transform.right), Vector3.Dot(accel, transform.forward));
            Vector2 targetLean = new Vector2(
                -localAccel.x * leanPerAccel,
                fwdSpeed * leanPerSpeed + localAccel.y * leanPerAccel);
            targetLean = Vector2.ClampMagnitude(targetLean, maxLean);
            lean = Vector2.Lerp(lean, targetLean, 1f - Mathf.Exp(-leanSmoothing * dt));

            // Two bobs per stride cycle (one per footfall), only while grounded and moving.
            float speed = velocity.magnitude;
            bool grounded = motor.IsGrounded;
            runBlend = Mathf.MoveTowards(runBlend, grounded ? Mathf.Clamp01(speed / 2.5f) : 0f, 6f * dt);
            stridePhase += speed / strideLength * Mathf.PI * dt;
            float bob = Mathf.Abs(Mathf.Sin(stridePhase)) * bobHeight * runBlend;
            float sway = Mathf.Sin(stridePhase) * swayAngle * runBlend;

            if (grounded && !wasGrounded) squash = -landSquash;
            else if (!grounded && wasGrounded) squash = jumpStretch;
            wasGrounded = grounded;
            squash = Mathf.Lerp(squash, 0f, 1f - Mathf.Exp(-squashRecovery * dt));

            float bumpPose = contact != null ? contact.BumpPose : 0f;
            float servePose = contact != null ? contact.ServePose : 0f;
            float setPose = contact != null ? contact.SetPose : 0f;
            float spikePose = contact != null ? contact.SpikePose : 0f;
            float divePose = motor.DivePose;
            // Arms out flat on a dive, so drop the bump crouch while laid out.
            bumpPose *= 1f - divePose;

            float stretch = squash + servePose * serveReach + setPose * setReach - bumpPose * bumpCrouch * 0.5f;
            float height = bob - bumpPose * bumpCrouch + servePose * serveReach * 0.5f + setPose * setReach * 0.5f
                - divePose * diveDrop;
            float pitch = lean.y + bumpPose * bumpLean + servePose * serveLeanBack + setPose * setLeanBack
                + spikePose * spikeLean;
            pitch = Mathf.Lerp(pitch, diveLayoutPitch, divePose);

            model.localPosition = baseLocalPos + Vector3.up * height;
            model.localRotation = Quaternion.Euler(pitch, 0f, lean.x + sway) * baseLocalRot;
            // Volume-preserving: taller means thinner.
            float sy = 1f + stretch;
            float sxz = 1f / Mathf.Sqrt(sy);
            model.localScale = Vector3.Scale(baseScale, new Vector3(sxz, sy, sxz));
        }
    }
}
