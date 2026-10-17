using UnityEngine;

namespace BeachVolley.Player
{
    /// <summary>
    /// Feeds the rigged character's Animator from gameplay: locomotion speed, grounded/air,
    /// and a trigger for each hit. Root motion stays off; PlayerMotor owns movement.
    /// Faster than the run cycle travels, the cycle is sped up instead of sliding the feet.
    /// The attack arm (cock, spike swing, roll swing; serves use the same swings) and the block play
    /// on the controller's upper-body layer, faded in only while they are up.
    /// While it drives a humanoid rig it switches off PlayerModelAnimator's whole-mesh
    /// lean/squash so the two never fight. Does nothing if no usable Animator is found.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerAnimatorDriver : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly int DivingId = Animator.StringToHash("Diving");
        static readonly int BumpId = Animator.StringToHash("Bump");
        static readonly int SetId = Animator.StringToHash("Set");
        static readonly int SpikeId = Animator.StringToHash("Spike");
        static readonly int DiveId = Animator.StringToHash("Dive");
        static readonly int RollId = Animator.StringToHash("Roll");
        static readonly int BlockId = Animator.StringToHash("Block");
        static readonly int CockedId = Animator.StringToHash("Cocked");
        static readonly int LocoSpeedMulId = Animator.StringToHash("LocoSpeedMul");
        static readonly int CockStateId = Animator.StringToHash("Cock");
        static readonly int SpikeSwingStateId = Animator.StringToHash("SpikeSwing");
        static readonly int RollSwingStateId = Animator.StringToHash("RollSwing");
        static readonly int BlockStateId = Animator.StringToHash("Block");

        [Tooltip("Defaults to the first Animator under the player.")]
        [SerializeField] Animator animator;
        [SerializeField] float speedDamping = 0.08f;
        [Tooltip("Speed (m/s) the run cycle travels at on this character. Written by MixamoSetup.")]
        [SerializeField] float runCycleSpeed = 5.5f;
        [Tooltip("Upper-body layer fade in/out time (s).")]
        [SerializeField] float upperBodyFade = 0.12f;
        [Tooltip("Normalized time in the swing at which the upper-body layer starts fading out.")]
        [SerializeField, Range(0f, 1f)] float upperBodyRelease = 0.8f;

        PlayerMotor motor;
        BallContact contact;
        int upperLayer = -1;
        float upperWeight;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            contact = GetComponent<BallContact>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            bool usable = animator != null && animator.runtimeAnimatorController != null
                && animator.avatar != null && animator.avatar.isHuman;
            if (!usable) { enabled = false; return; }

            animator.applyRootMotion = false;
            upperLayer = animator.GetLayerIndex("UpperBody");
            var procedural = GetComponent<PlayerModelAnimator>();
            if (procedural != null) procedural.enabled = false;
        }

        void OnEnable()
        {
            if (contact != null) contact.SwingStarted += OnSwing;
        }

        void OnDisable()
        {
            if (contact != null) contact.SwingStarted -= OnSwing;
        }

        void Update()
        {
            float speed = motor.PlanarVelocity.magnitude;
            animator.SetFloat(SpeedId, speed, speedDamping, Time.deltaTime);
            animator.SetFloat(LocoSpeedMulId, Mathf.Max(1f, speed / Mathf.Max(runCycleSpeed, 0.1f)));
            animator.SetBool(GroundedId, motor.IsGrounded);
            animator.SetFloat(VerticalSpeedId, motor.VerticalVelocity);
            animator.SetBool(DivingId, motor.IsDiving);
            animator.SetBool(CockedId, contact != null && contact.IsCocked);
            UpdateUpperBodyWeight(Time.deltaTime);
        }

        // Full weight while the arm is cocked, swinging or blocking; fades out near the end of a
        // swing unless the arm goes straight back to the cock.
        void UpdateUpperBodyWeight(float dt)
        {
            if (upperLayer < 0) return;
            bool swinging = IsSwing(animator.GetCurrentAnimatorStateInfo(upperLayer), true)
                || (animator.IsInTransition(upperLayer) && IsSwing(animator.GetNextAnimatorStateInfo(upperLayer), false));
            float rate = dt / Mathf.Max(upperBodyFade, 0.01f);
            upperWeight = Mathf.MoveTowards(upperWeight, swinging ? 1f : 0f, rate);
            animator.SetLayerWeight(upperLayer, upperWeight);
        }

        bool IsSwing(AnimatorStateInfo info, bool releaseNearEnd)
        {
            int h = info.shortNameHash;
            if (h == CockStateId) return true;
            bool swing = h == SpikeSwingStateId || h == RollSwingStateId || h == BlockStateId;
            bool recocking = h != BlockStateId && contact != null && contact.IsCocked;
            return swing && (!releaseNearEnd || recocking || info.normalizedTime < upperBodyRelease);
        }

        void OnSwing(HitType type)
        {
            int id = type switch
            {
                HitType.Bump => BumpId,
                HitType.Set => SetId,
                HitType.Spike => SpikeId,
                HitType.Dive => DiveId,
                HitType.Block => BlockId,
                HitType.Roll => RollId,
                _ => 0,
            };
            if (id == 0) return;
            ResetHitTriggers();
            animator.SetTrigger(id);
        }

        // A stale trigger from a swing that never reached its state would fire later at the wrong time.
        void ResetHitTriggers()
        {
            animator.ResetTrigger(BumpId);
            animator.ResetTrigger(SetId);
            animator.ResetTrigger(SpikeId);
            animator.ResetTrigger(DiveId);
            animator.ResetTrigger(RollId);
            animator.ResetTrigger(BlockId);
        }
    }
}
