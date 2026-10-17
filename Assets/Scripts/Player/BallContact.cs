using System;
using BeachVolley.Ball;
using BeachVolley.Core;
using BeachVolley.Court;
using BeachVolley.Skills;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.InputSystem;

namespace BeachVolley.Player
{
    public enum HitType { None, Bump, Set, Spike, Dive, Roll, Block }

    /// <summary>Tuning for one hit other than the bump: where it reaches, how long it's live, how sloppy a rookie is.</summary>
    [Serializable]
    public class HitSettings
    {
        [Tooltip("Player-local centre of the contact zone.")]
        public Vector3 zoneCenter;
        public Vector3 zoneSize = Vector3.one;
        public float activeWindow = 0.25f;
        public float cooldown = 0.45f;
        public float aimErrorRookie = 15f;
        public float aimErrorMaster = 3f;
        [Range(0f, 0.5f)] public float powerErrorRookie = 0.18f;
        [Range(0f, 0.5f)] public float powerErrorMaster = 0.04f;
    }

    /// <summary>
    /// The player's hands/arms. Each hit opens a short contact window over its own zone:
    /// bump (forearms, chest/waist height), set (hands above the head), spike (airborne,
    /// high and in front, driven over the net), roll (a soft placed shot toward where the
    /// camera looks), block (a wall of hands above the net) and dive (a dash and layout for
    /// low, far balls). Rookie hits are imprecise; the matching skill tightens them.
    /// Standing near a ball in play turns the player to face it. Walking into the ball nudges it.
    /// One key per action: LMB bump (in the air: spike), RMB set, F roll, Q block, E dive, R reset.
    /// Spike and roll swing from the cocked arm (auto-raised in a jump near a high ball, or during
    /// a serve toss) and only strike at the animation's contact beat. While a serve toss (C) is in
    /// the air, LMB / F swing a power / short serve instead, standing or jumping.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class BallContact : MonoBehaviour
    {
        [SerializeField] VolleyBall ball;

        [Header("Contact zone (player-local)")]
        [SerializeField] Vector3 zoneCenter = new Vector3(0f, 0.75f, 0.55f);
        [SerializeField] Vector3 zoneSize = new Vector3(1f, 1.5f, 0.9f);

        [Header("Bump")]
        [SerializeField] float activeWindow = 0.25f;
        [SerializeField] float cooldown = 0.45f;
        [Tooltip("The bump goes to the setter spot: centre of your half, this far off the net...")]
        [SerializeField] float setterSpotOffNet = 2f;
        [Tooltip("...peaking this high (world), so a partner has time to get there.")]
        [SerializeField] float bumpApex = 4.5f;
        [Tooltip("Share of the player's own run speed that leaks into a badly timed bump.")]
        [SerializeField, Range(0f, 1f)] float carryPlayerVelocity = 0.5f;

        [Header("Pass skill at 100 (values above are the 0 baseline)")]
        [Tooltip("Contact-zone size multiplier: a better passer reaches more balls.")]
        [SerializeField] float passZoneScaleAtMax = 1.4f;
        [SerializeField] float passWindowAtMax = 0.4f;
        [SerializeField] float passCooldownAtMax = 0.3f;

        [Header("Timing (bump / set / dig)")]
        [Tooltip("Pressing within this many seconds of the ball reaching the sweet spot is perfect.")]
        [SerializeField] float perfectWindow = 0.06f;
        [Tooltip("Quality fades from perfect to zero at this timing error (s): bump, set, dig.")]
        [SerializeField] Vector3 goodWindow = new Vector3(0.2f, 0.18f, 0.22f);
        [Tooltip("Both windows grow by this factor at skill 100 (Pass / Set / Defense).")]
        [SerializeField] float windowScaleAtMax = 1.6f;
        [Tooltip("Sweet-spot height above the feet (m): bump, set, dig.")]
        [SerializeField] Vector3 sweetHeight = new Vector3(0.85f, 2.2f, 0.35f);
        [Tooltip("A dive should be pressed this long before the ball reaches dig height (the dash).")]
        [SerializeField] float diveLead = 0.25f;
        [Tooltip("Landing scatter (m) of a pass: perfect timing .. worst timing.")]
        [SerializeField] Vector2 passScatter = new Vector2(0.3f, 2.2f);
        [Tooltip("Apex error (m) of a pass: perfect timing .. worst timing.")]
        [SerializeField] Vector2 passApexError = new Vector2(0.2f, 1f);
        [SerializeField] bool showTimingFeedback = true;

        [Header("Timing (spike / roll / serve)")]
        [Tooltip("At the contact beat, a ball this close (m) to the hitting sweet spot is perfect...")]
        [SerializeField] float attackPerfectDistance = 0.35f;
        [Tooltip("...and quality fades to zero at this distance.")]
        [SerializeField] float attackGoodDistance = 1f;
        [Tooltip("Spike / roll press timing (s), like bump/set/dig: within x of the ideal press is perfect, fading to zero at y. Grows with Attack.")]
        [SerializeField] Vector2 attackTimingWindow = new Vector2(0.06f, 0.2f);

        [Header("Attack helper")]
        [Tooltip("Assisted jump: in the air you're steered to the ball and LMB/F swings auto-time themselves.")]
        [SerializeField] bool attackAssist = true;
        [Tooltip("How far (m) the helper will carry you sideways to meet the ball.")]
        [SerializeField] float assistReach = 2.2f;
        [Tooltip("Space near a high, hittable ball (within this many m) always gives the full approach jump.")]
        [SerializeField] float assistBoostRange = 4f;
        [SerializeField] float assistBoostMinBallHeight = 2.5f;
        [Tooltip("Pressing LMB/F up to this many seconds early waits for the right moment.")]
        [SerializeField] float queueMaxEarly = 0.8f;

        [Header("Realism")]
        [Tooltip("Only a floating ball can be spiked or rolled: slower than this (m/s)...")]
        [SerializeField] float attackMaxBallSpeed = 9f;
        [Tooltip("...and not coming at you faster than this horizontally (m/s).")]
        [SerializeField] float attackMaxIncoming = 6f;
        [Tooltip("An overhand set of a ball faster than this would be a lift.")]
        [SerializeField] float setMaxBallSpeed = 12f;
        [Tooltip("No spiking from further than this outside the court lines (m). Rolls still go, less accurately.")]
        [SerializeField] float attackMaxOutside = 2f;
        [Tooltip("Extra roll scatter per metre outside the court, capped.")]
        [SerializeField] float rollScatterPerMetreOutside = 0.5f;
        [SerializeField] float rollScatterOutsideMax = 2.5f;
        [Tooltip("Spikes from deeper than this (m from the net) turn progressively into a down-ball...")]
        [SerializeField] float fullAttackDepth = 4.5f;
        [Tooltip("...reaching this share of the speed (with extra net clearance) at downBallDepth.")]
        [SerializeField, Range(0.3f, 1f)] float downBallSpeed = 0.6f;
        [SerializeField] float downBallDepth = 8f;
        [Tooltip("Blocks only work this close to the net (m).")]
        [SerializeField] float blockMaxNetDistance = 1.5f;
        [Tooltip("Receiving a ball this fast (m/s) shrinks the bump/dig timing window to receiveWindowAtFast.")]
        [SerializeField] Vector2 receiveSpeedRange = new Vector2(8f, 16f);
        [SerializeField, Range(0.3f, 1f)] float receiveWindowAtFast = 0.65f;
        [Tooltip("Bumping or setting at a full sprint keeps this share of the accuracy.")]
        [SerializeField, Range(0.3f, 1f)] float movingPassAccuracy = 0.7f;
        [SerializeField] float sprintReference = 6f;

        [Header("Set (Set skill)")]
        [SerializeField] HitSettings set = new HitSettings
        {
            zoneCenter = new Vector3(0f, 2.05f, 0.35f), zoneSize = new Vector3(1.1f, 1.1f, 1f),
            activeWindow = 0.28f, cooldown = 0.4f,
        };
        [Tooltip("Sets come down this far off the net...")]
        [SerializeField] float setOffNet = 1f;
        [Tooltip("...this far to the other side of the court from you (m from the middle)...")]
        [SerializeField] float setAcross = 2.5f;
        [Tooltip("...peaking this high (world): above jump-spike reach (~3.35 m).")]
        [SerializeField] float setApex = 4.6f;
        [Tooltip("Closer to the middle than this, the set goes left.")]
        [SerializeField] float setMiddleZone = 0.6f;

        [Header("Spike (Attack skill)")]
        [SerializeField] HitSettings spike = new HitSettings
        {
            zoneCenter = new Vector3(0f, 2.45f, 0.6f), zoneSize = new Vector3(2f, 1.8f, 1.8f),
            activeWindow = 0.22f, cooldown = 0.5f, aimErrorRookie = 10f, aimErrorMaster = 2f,
            powerErrorRookie = 0.15f, powerErrorMaster = 0.03f,
        };
        [SerializeField] float spikeSpeed = 14f;
        [SerializeField] float spikeMinSpeed = 7f;
        [Tooltip("Aim point depth into the far half, from the net.")]
        [SerializeField] float spikeDepth = 4.5f;
        [SerializeField] float spikeScatterRookie = 1.4f;
        [SerializeField] float spikeScatterMaster = 0.3f;
        [Tooltip("How far above the tape the drive has to pass.")]
        [SerializeField] float netClearance = 0.15f;
        [Tooltip("Press to strike: matches the contact beat of the SpikeSwing clip.")]
        [SerializeField] float spikeContactDelay = 0.16f;

        [Header("Roll shot (Attack skill)")]
        [Tooltip("Zone is unused: grounded it reaches the set or bump zone, airborne the spike zone.")]
        [SerializeField] HitSettings roll = new HitSettings
        {
            activeWindow = 0.25f, cooldown = 0.45f, aimErrorRookie = 0f, aimErrorMaster = 0f,
            powerErrorRookie = 0f, powerErrorMaster = 0f,
        };
        [Tooltip("How far past the net the roll lands along the camera direction.")]
        [SerializeField] float rollDepth = 6f;
        [Tooltip("Landing spots are kept this far inside the sidelines.")]
        [SerializeField] float rollSidelineMargin = 0.5f;
        [SerializeField] float rollScatterRookie = 1.2f;
        [SerializeField] float rollScatterMaster = 0.25f;
        [Tooltip("The roll peaks at least this far above the net tape.")]
        [SerializeField] float rollNetApex = 1.2f;
        [Tooltip("Press to strike: matches the contact beat of the RollSwing clip.")]
        [SerializeField] float rollContactDelay = 0.18f;

        [Header("Cocked arm")]
        [Tooltip("In the air, the arm cocks when the ball is above this height (world)...")]
        [SerializeField] float cockMinBallHeight = 2f;
        [Tooltip("...and within this horizontal distance.")]
        [SerializeField] float cockRange = 3f;

        [Header("Block (Block skill)")]
        [SerializeField] HitSettings block = new HitSettings
        {
            zoneCenter = new Vector3(0f, 2.45f, 0.35f), zoneSize = new Vector3(1.4f, 1f, 0.8f),
            activeWindow = 0.45f, cooldown = 0.6f, aimErrorRookie = 0f, aimErrorMaster = 0f,
            powerErrorRookie = 0f, powerErrorMaster = 0f,
        };
        [SerializeField] float blockZoneScaleAtMax = 1.3f;
        [Tooltip("Blocked balls land this far past the net (min..max).")]
        [SerializeField] Vector2 blockLandDepth = new Vector2(1.5f, 2.5f);
        [SerializeField] float blockScatterRookie = 1f;
        [SerializeField] float blockScatterMaster = 0.2f;

        [Header("Dive (Defense skill)")]
        [Tooltip("The window is the motor's dash + layout, so activeWindow is unused here.")]
        [SerializeField] HitSettings dive = new HitSettings
        {
            zoneCenter = new Vector3(0f, 0.4f, 1.1f), zoneSize = new Vector3(1.8f, 1f, 2f),
            activeWindow = 0f, cooldown = 1.1f,
        };
        [Tooltip("A dig pops the ball up toward the middle of your half, this far off the net...")]
        [SerializeField] float digTargetOffNet = 4f;
        [Tooltip("...peaking this high (world) when perfect, this low when badly timed.")]
        [SerializeField] Vector2 digApex = new Vector2(3.5f, 2.4f);
        [Tooltip("How much a dive bends toward where the ball will land (0..1). A small help; skill later.")]
        [SerializeField, Range(0f, 1f)] float diveLockOn = 0.2f;
        [SerializeField] float diveLockOnRange = 5f;

        [Header("Auto-face the ball")]
        [Tooltip("Standing still, turn toward a ball in play within this distance (horizontal).")]
        [SerializeField] float autoFaceRange = 8f;

        [Header("Body nudge")]
        [SerializeField] float nudgeSpeed = 1.6f;
        [Tooltip("Extra gap beyond body + ball radius at which a walk-in counts as a touch.")]
        [SerializeField] float nudgeReach = 0.08f;
        [Tooltip("Only balls below this height (player-relative) get nudged; higher ones just collide.")]
        [SerializeField] float nudgeMaxHeight = 0.6f;

        [Header("Arms")]
        [SerializeField] Transform leftArmPivot;
        [SerializeField] Transform rightArmPivot;
        [SerializeField] float armBumpAngle = -70f;
        [SerializeField] float armRestTilt = 8f;
        [SerializeField] float armBumpTilt = -12f;
        [SerializeField] float armSpeed = 14f;

        [Header("Practice")]
        [SerializeField] float resetDistance = 0.8f;
        [SerializeField] float tossHeight = 1.8f;

        [Header("HUD")]
        [SerializeField] bool showControlsHint = true;

        PlayerMotor motor;
        PlayerSkills skills;
        PlayerServe serve;
        Transform cameraTransform;
        InputAction bumpAction;
        InputAction setAction;
        InputAction rollAction;
        InputAction blockAction;
        InputAction diveAction;
        InputAction resetAction;
        HitType swing;
        float swingStart = float.NegativeInfinity;
        float contactFrom = float.NegativeInfinity;
        float activeUntil = float.NegativeInfinity;
        // Set when this swing is a serve: the contact goes to PlayerServe instead of spike/roll.
        bool swingIsServe;
        // Attack helper: where the ball will meet the hand, and a swing waiting for that moment.
        bool hasPlan;
        float planContactIn;
        Vector3 planFeet;
        HitType queuedSwing;
        float queuedAt;
        // Spike / roll press timing: press time minus the ideal press time (− early, + late).
        float attackPressError;
        bool attackPressKnown;
        float cooldownUntil = float.NegativeInfinity;
        bool touchedThisSwing;
        // Timing of the current bump / set / dig: press time minus the ideal press time (− early, + late).
        float timingError;
        bool timingKnown;
        string feedbackText;
        Color feedbackColor;
        float feedbackUntil = float.NegativeInfinity;
        GUIStyle feedbackStyle;
        float armPose;
        float servePose;
        float setPose;
        float spikePose;
        float serveSwingUntil = float.NegativeInfinity;
        float bodyRadius;
        GUIStyle hintStyle;

        public VolleyBall Ball
        {
            get => ball;
            set => ball = value;
        }

        bool IsSwingActive => swing == HitType.Dive
            ? motor.IsDiveReaching
            : Time.time >= contactFrom && Time.time < activeUntil;

        /// <summary>
        /// Arms up in the bow-and-arrow, ready to swing: in the air near a high ball, or while a
        /// serve toss is up. Never while blocking or diving.
        /// </summary>
        public bool IsCocked
        {
            get
            {
                if (motor.IsDiving || ball == null || ball.IsDead) return false;
                if (swing == HitType.Block && Time.time < activeUntil) return false;
                if (serve != null && serve.IsTossLive) return true;
                if (motor.IsGrounded) return false;
                Vector3 d = ball.transform.position - transform.position;
                return ball.transform.position.y >= cockMinBallHeight
                    && new Vector2(d.x, d.z).magnitude <= cockRange;
            }
        }

        /// <summary>True while the forearm platform is out and a ball in the zone will be played.</summary>
        public bool IsBumping => swing == HitType.Bump && IsSwingActive;

        /// <summary>The hit currently (or most recently) swung.</summary>
        public HitType CurrentSwing => swing;

        /// <summary>Seconds since the current swing started.</summary>
        public float SwingTime => Time.time - swingStart;

        /// <summary>0 = arms at rest, 1 = full bump platform (also held through a dive).</summary>
        public float BumpPose => armPose;

        /// <summary>0 = rest, 1 = overhead serve swing.</summary>
        public float ServePose => servePose;

        /// <summary>0 = rest, 1 = hands up for a set (or a block / grounded roll).</summary>
        public float SetPose => setPose;

        /// <summary>0 = rest, 1 = hitting arm swinging for a spike.</summary>
        public float SpikePose => spikePose;

        /// <summary>
        /// Raised when a controlled pass (bump, set or dig) plays the ball.
        /// Argument: ball height at contact.
        /// </summary>
        public event Action<float> Bumped;

        /// <summary>Raised when any hit starts swinging, touch or not. Drives animation.</summary>
        public event Action<HitType> SwingStarted;

        /// <summary>Raised when any hit plays the ball: (type, ball height at contact).</summary>
        public event Action<HitType, float> Played;

        /// <summary>Raised with each graded touch: (type, quality 0..1). Story drills use it for praise.</summary>
        public event Action<HitType, float> HitGraded;

        /// <summary>
        /// Story helper (1 = normal): widens the attack assist's reach, the spike/roll timing window
        /// and the sweet spot, so a scripted "one more try" lands within a few attempts.
        /// </summary>
        public float AssistBoost { get; set; } = 1f;

        public bool ShowControlsHint { get => showControlsHint; set => showControlsHint = value; }

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            skills = GetComponent<PlayerSkills>();
            serve = GetComponent<PlayerServe>();
            bodyRadius = GetComponent<CharacterController>().radius;
            if (ball == null) ball = FindAnyObjectByType<VolleyBall>();
            if (Camera.main != null) cameraTransform = Camera.main.transform;

            // Mouse hits only count while the cursor is captured, so UI clicks never swing the arms.
            bumpAction = new InputAction("Bump", InputActionType.Button, "<Mouse>/leftButton");
            bumpAction.AddBinding("<Gamepad>/buttonWest");
            setAction = new InputAction("Set", InputActionType.Button, "<Mouse>/rightButton");
            setAction.AddBinding("<Gamepad>/buttonNorth");
            rollAction = new InputAction("Roll", InputActionType.Button, "<Keyboard>/f");
            rollAction.AddBinding("<Gamepad>/rightShoulder");
            blockAction = new InputAction("Block", InputActionType.Button, "<Keyboard>/q");
            blockAction.AddBinding("<Gamepad>/leftTrigger");
            diveAction = new InputAction("Dive", InputActionType.Button, "<Keyboard>/e");
            diveAction.AddBinding("<Gamepad>/buttonEast");
            resetAction = new InputAction("ResetBall", InputActionType.Button, "<Keyboard>/r");
            resetAction.AddBinding("<Gamepad>/select");
        }

        InputAction[] Actions => new[] { bumpAction, setAction, rollAction, blockAction, diveAction, resetAction };

        void OnEnable() { foreach (var a in Actions) a.Enable(); }
        void OnDisable() { foreach (var a in Actions) a.Disable(); }
        void OnDestroy() { foreach (var a in Actions) a.Dispose(); }

        static bool Pressed(InputAction action)
        {
            if (!action.WasPressedThisFrame()) return false;
            // Mouse buttons only act while the game has the cursor.
            return !(action.activeControl?.device is Mouse) || Cursor.lockState == CursorLockMode.Locked;
        }

        void Update()
        {
            if (UiPointerGate.MenuOpen) { AnimateArms(Time.deltaTime); return; }
            if (ball != null && resetAction.WasPressedThisFrame())
                ResetBall();

            UpdateAttackPlan();
            FireQueuedSwing();

            bool serving = serve != null && serve.IsTossLive;
            // Left mouse is contextual: power serve during a toss, else bump on the ground, spike in the air.
            if (Pressed(bumpAction))
            {
                if (serving) PressServe(HitType.Spike);
                else if (motor.IsGrounded) PressBump();
                else PressSpike();
            }
            if (Pressed(setAction)) PressSet();
            if (rollAction.WasPressedThisFrame())
            {
                if (serving) PressServe(HitType.Roll);
                else PressRoll();
            }
            if (blockAction.WasPressedThisFrame()) PressBlock();
            if (diveAction.WasPressedThisFrame()) PressDive();

            if (swing != HitType.None && IsSwingActive && !touchedThisSwing && ball != null)
            {
                if (swingIsServe)
                {
                    // The serve has its own generous window around the player; timing is judged
                    // against the hitting sweet spot like an attack.
                    float q = AttackQuality(SkillType.Serve);
                    bool hit = swing == HitType.Spike ? serve.HitPower(q) : serve.HitShort(q);
                    if (hit)
                    {
                        touchedThisSwing = true;
                        ShowTiming(q);
                    }
                }
                else if (BallInZone(swing))
                {
                    // Realistic contact rules: a refused touch uses up the swing.
                    string refusal = Refusal(swing);
                    if (refusal != null) Say(refusal, new Color(1f, 0.45f, 0.35f));
                    else PlayHit(swing);
                    touchedThisSwing = true;
                }
            }

            if (ball != null)
            {
                motor.SetSoftFacing(BallInPlayNearby() ? ball.transform.position : (Vector3?)null);
                if (!motor.IsDiving) NudgeIfWalkingIntoBall();
            }

            AnimateArms(Time.deltaTime);
        }

        bool BallInPlayNearby()
        {
            // Once the ball has hit the sand the rally is over: stop turning toward it.
            if (motor.IsDiving || ball.IsDead) return false;
            Vector3 d = ball.transform.position - transform.position;
            float flat = new Vector2(d.x, d.z).magnitude;
            return flat <= autoFaceRange;
        }

        /// <summary>Starts a bump swing. Ignored during cooldown or mid-dive.</summary>
        public void PressBump()
        {
            if (motor.IsDiving) return;
            if (StartSwing(HitType.Bump, PassScale(activeWindow, passWindowAtMax), PassScale(cooldown, passCooldownAtMax)))
                MeasureTiming(sweetHeight.x, 0f, GoodWindow(HitType.Bump));
        }

        /// <summary>Hands up for an overhead set. Ignored during cooldown or mid-dive.</summary>
        public void PressSet()
        {
            if (motor.IsDiving) return;
            if (StartSwing(HitType.Set, set.activeWindow, set.cooldown))
                MeasureTiming(sweetHeight.y, 0f, GoodWindow(HitType.Set));
        }

        /// <summary>
        /// Records how early or late this press is against the moment the ball reaches the hit's
        /// sweet spot (plus <paramref name="lead"/>, e.g. the dive dash). Pressing a little early
        /// keeps the window open until the ball gets there, so it isn't a whiff.
        /// </summary>
        void MeasureTiming(float sweet, float lead, float good)
        {
            timingKnown = false;
            if (ball == null || ball.IsDead) return;
            if (!ball.PredictTimeToHeight(transform.position.y + sweet, 3f, out float untilSweet)) return;
            timingError = lead - untilSweet;
            timingKnown = true;
            if (untilSweet - lead > 0f && untilSweet - lead < good * 1.5f && swing != HitType.Dive)
                activeUntil = Mathf.Max(activeUntil, Time.time + untilSweet + 0.08f);
        }

        float GoodWindow(HitType type)
        {
            float w = type == HitType.Set ? goodWindow.y : type == HitType.Dive ? goodWindow.z : goodWindow.x;
            return w * SkillScale(SkillFor(type), 1f, windowScaleAtMax) * ReceiveFactor(type);
        }

        /// <summary>A hard incoming ball is harder to pass: bump/dig windows shrink with its speed.</summary>
        float ReceiveFactor(HitType type)
        {
            if (ball == null || (type != HitType.Bump && type != HitType.Dive)) return 1f;
            float t = Mathf.InverseLerp(receiveSpeedRange.x, receiveSpeedRange.y, ball.Body.linearVelocity.magnitude);
            return Mathf.Lerp(1f, receiveWindowAtFast, t);
        }

        static SkillType SkillFor(HitType type) =>
            type == HitType.Set ? SkillType.Set : type == HitType.Dive ? SkillType.Defense : SkillType.Pass;

        /// <summary>Touch quality 0..1 from this swing's timing; an unknown timing counts as average.</summary>
        float TimingQuality(HitType type)
        {
            if (!timingKnown) return 0.5f;
            float perfect = perfectWindow * SkillScale(SkillFor(type), 1f, windowScaleAtMax) * ReceiveFactor(type);
            float good = Mathf.Max(GoodWindow(type), perfect + 0.01f);
            float e = Mathf.Abs(timingError);
            return e <= perfect ? 1f : Mathf.Clamp01(1f - (e - perfect) / (good - perfect));
        }

        void ShowTiming(float quality)
        {
            HitGraded?.Invoke(swing, quality);
            if (!showTimingFeedback || !timingKnown) return;
            if (quality >= 0.9f) Say("Perfect!", new Color(1f, 0.85f, 0.25f));
            else if (quality >= 0.5f) Say("Good", Color.white);
            else Say(timingError < 0f ? "Early" : "Late", new Color(1f, 0.55f, 0.35f));
            feedbackUntil = Time.time + 0.7f;
        }

        /// <summary>Spike swing. Only does anything in the air; strikes at the swing's contact beat.</summary>
        public void PressSpike()
        {
            if (motor.IsGrounded || motor.IsDiving) return;
            MeasureAttackPress(spike.zoneCenter.y, spikeContactDelay);
            if (TryQueue(HitType.Spike, spikeContactDelay)) return;
            StartSwing(HitType.Spike, spike.activeWindow, spike.cooldown, faceBall: false, contactDelay: spikeContactDelay);
        }

        /// <summary>Soft placed shot toward where the camera looks, standing or jumping.</summary>
        public void PressRoll()
        {
            if (motor.IsDiving) return;
            MeasureAttackPress(motor.IsGrounded ? set.zoneCenter.y : spike.zoneCenter.y, rollContactDelay);
            if (TryQueue(HitType.Roll, rollContactDelay)) return;
            StartSwing(HitType.Roll, roll.activeWindow, roll.cooldown, faceBall: motor.IsGrounded, contactDelay: rollContactDelay);
        }

        /// <summary>
        /// Records how early or late a spike / roll press is: the ideal press is the swing's
        /// <paramref name="contactDelay"/> before the ball reaches the hand (the attack plan's
        /// contact, or the ball coming down through <paramref name="handHeight"/> without one).
        /// </summary>
        void MeasureAttackPress(float handHeight, float contactDelay)
        {
            attackPressKnown = false;
            if (ball == null || ball.IsDead) return;
            float untilContact = planContactIn;
            if (!hasPlan && !ball.PredictTimeToHeight(transform.position.y + handHeight, 3f, out untilContact)) return;
            attackPressError = contactDelay - untilContact;
            attackPressKnown = true;
        }

        /// <summary>
        /// Spike / roll touch quality: the press timing (same kind of window as bump/set/dig), capped by
        /// how close the ball actually is to the sweet spot at contact.
        /// </summary>
        float AttackHitQuality(Vector3 localSweet)
        {
            float q = AttackQuality(SkillType.Attack, localSweet);
            if (!attackPressKnown) return q;
            float scale = SkillScale(SkillType.Attack, 1f, windowScaleAtMax);
            float perfect = attackTimingWindow.x * scale * AssistBoost;
            float good = Mathf.Max(attackTimingWindow.y * scale * AssistBoost, perfect + 0.01f);
            float e = Mathf.Abs(attackPressError);
            float timing = e <= perfect ? 1f : Mathf.Clamp01(1f - (e - perfect) / (good - perfect));
            if (timing < q)
            {
                q = timing;
                timingError = attackPressError;
            }
            return q;
        }

        // ---------- Attack helper ----------

        /// <summary>
        /// Where (and when) a hittable ball will meet the hand. In the air: at the spike sweet spot
        /// for where your jump will carry you; on the ground: at the overhead roll height. Drives the
        /// jump boost, the air magnet and the auto-timed swings.
        /// </summary>
        void UpdateAttackPlan()
        {
            hasPlan = false;
            bool grounded = motor.IsGrounded;
            Vector3 dir = AttackDirection();
            bool usable = attackAssist && ball != null && !ball.IsDead && !motor.IsDiving && Floating();

            motor.AttackJumpBoost = usable && grounded && ball.transform.position.y >= assistBoostMinBallHeight
                && Flat2(ball.transform.position - transform.position) <= assistBoostRange;

            if (usable)
            {
                if (grounded)
                {
                    // Grounded roll: the ball coming down through overhead height in front of you.
                    if (ball.PredictTimeToHeight(transform.position.y + set.zoneCenter.y, 2.5f, out float t) && t > -0.05f)
                    {
                        planContactIn = t;
                        planFeet = transform.position;
                        hasPlan = Flat2(ball.PredictPositionAt(Mathf.Max(t, 0f)) - transform.position) <= 1.6f;
                    }
                }
                else
                {
                    // Airborne: find when the ball crosses the height the hand will be at, allowing for
                    // the jump arc (two refinement passes).
                    float g = motor.Gravity, vy = motor.VerticalVelocity, y0 = transform.position.y;
                    float t = vy > 0f ? vy / g : 0f;
                    bool found = false;
                    for (int i = 0; i < 2; i++)
                    {
                        float feet = y0 + vy * t - 0.5f * g * t * t;
                        if (!ball.PredictTimeToHeight(feet + spike.zoneCenter.y, 2.5f, out float tb)) { found = false; break; }
                        t = Mathf.Max(tb, 0f);
                        found = tb > -0.05f;
                    }
                    if (found)
                    {
                        Vector3 meet = ball.PredictPositionAt(t);
                        Vector3 feet = meet - dir * spike.zoneCenter.z;
                        feet.y = y0;
                        if (Flat2(feet - transform.position) <= assistReach * AssistBoost)
                        {
                            hasPlan = true;
                            planContactIn = t;
                            planFeet = feet;
                        }
                    }
                }
            }

            if (!grounded && hasPlan) motor.SetAirAssist(planFeet, planContactIn, dir);
            else motor.SetAirAssist(null, 0f, dir);
        }

        /// <summary>
        /// With a plan, a press that comes early waits so the swing reaches contact exactly when the
        /// ball arrives. Returns true if the swing was queued (or is already queued).
        /// </summary>
        bool TryQueue(HitType type, float contactDelay)
        {
            if (!attackAssist || !hasPlan) return false;
            float wait = planContactIn - contactDelay;
            if (wait <= 0f || wait > queueMaxEarly) return false;
            queuedSwing = type;
            queuedAt = Time.time + wait;
            return true;
        }

        void FireQueuedSwing()
        {
            if (queuedSwing == HitType.None || Time.time < queuedAt) return;
            var type = queuedSwing;
            queuedSwing = HitType.None;
            if (motor.IsDiving || (type == HitType.Spike && motor.IsGrounded)) return;
            if (type == HitType.Spike)
                StartSwing(HitType.Spike, spike.activeWindow, spike.cooldown, faceBall: false, contactDelay: spikeContactDelay);
            else
                StartSwing(HitType.Roll, roll.activeWindow, roll.cooldown, faceBall: motor.IsGrounded, contactDelay: rollContactDelay);
        }

        /// <summary>A ball you could attack: floating, not a hard ball coming at you.</summary>
        bool Floating()
        {
            Vector3 v = ball.Body.linearVelocity;
            Vector3 toMe = transform.position - ball.transform.position;
            toMe.y = 0f;
            float incoming = toMe.sqrMagnitude > 1e-4f ? Vector3.Dot(new Vector3(v.x, 0f, v.z), toMe.normalized) : 0f;
            return v.magnitude <= attackMaxBallSpeed && incoming <= attackMaxIncoming;
        }

        /// <summary>Where attacks go: the camera's aim, or straight over the net if it looks away from the far court.</summary>
        Vector3 AttackDirection()
        {
            Vector3 dir = AimDirection;
            var toFar = new Vector3(0f, 0f, FarSide());
            return Vector3.Dot(dir, toFar) < 0.2f ? toFar : dir;
        }

        static float Flat2(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        /// <summary>
        /// Serve swing during a live toss: Spike = power serve, Roll = short serve. Standing or
        /// jumping; the contact beat hands the hit to PlayerServe.
        /// </summary>
        void PressServe(HitType type)
        {
            if (motor.IsDiving) return;
            bool power = type == HitType.Spike;
            if (StartSwing(type, power ? spike.activeWindow : roll.activeWindow, power ? spike.cooldown : roll.cooldown,
                    faceBall: false, contactDelay: power ? spikeContactDelay : rollContactDelay))
                swingIsServe = true;
        }

        /// <summary>Hands up as a wall over the net, standing or jumping. Doesn't turn the body.</summary>
        public void PressBlock()
        {
            if (motor.IsDiving) return;
            if (Mathf.Abs(transform.position.z) > blockMaxNetDistance)
            {
                Say("Block at the net", new Color(1f, 0.45f, 0.35f));
                return;
            }
            StartSwing(HitType.Block, block.activeWindow, block.cooldown, faceBall: false);
        }

        /// <summary>
        /// Why this touch isn't allowed, or null if it is: no spiking or rolling a hard ball, no
        /// overhand set of one, no spiking from far off the court. Bumps, digs and blocks take anything.
        /// </summary>
        string Refusal(HitType type)
        {
            Vector3 v = ball.Body.linearVelocity;
            Vector3 toMe = transform.position - ball.transform.position;
            toMe.y = 0f;
            float incoming = toMe.sqrMagnitude > 1e-4f ? Vector3.Dot(new Vector3(v.x, 0f, v.z), toMe.normalized) : 0f;
            switch (type)
            {
                case HitType.Spike:
                case HitType.Roll:
                    if (v.magnitude > attackMaxBallSpeed || incoming > attackMaxIncoming) return "Too fast \u2013 dig it";
                    if (type == HitType.Spike && OutsideCourt() > attackMaxOutside) return "Too far";
                    return null;
                case HitType.Set:
                    return v.magnitude > setMaxBallSpeed ? "Too fast \u2013 bump it" : null;
                default:
                    return null;
            }
        }

        /// <summary>How far (m) the player stands outside the court lines (0 inside).</summary>
        float OutsideCourt()
        {
            Vector3 p = transform.position;
            float dx = Mathf.Max(0f, Mathf.Abs(p.x) - CourtDimensions.HalfWidth);
            float dz = Mathf.Max(0f, Mathf.Abs(p.z) - CourtDimensions.SideLength);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Attack / serve timing: how close the ball is to the hitting sweet spot (high, just in
        /// front) at the contact beat. Also sets the Early/Late sign for the feedback word.
        /// </summary>
        float AttackQuality(SkillType skill) => AttackQuality(skill, spike.zoneCenter);

        float AttackQuality(SkillType skill, Vector3 localSweet)
        {
            Vector3 sweet = transform.TransformPoint(localSweet);
            Vector3 d = ball.transform.position - sweet;
            float scale = SkillScale(skill, 1f, windowScaleAtMax);
            float perfect = attackPerfectDistance * scale * AssistBoost;
            float good = Mathf.Max(attackGoodDistance * scale * AssistBoost, perfect + 0.01f);
            // Still above the sweet spot = the ball hasn't come down yet = early.
            timingError = d.y > 0f ? -1f : 1f;
            timingKnown = true;
            float dist = d.magnitude;
            return dist <= perfect ? 1f : Mathf.Clamp01(1f - (dist - perfect) / (good - perfect));
        }

        void Say(string text, Color color)
        {
            if (!showTimingFeedback) return;
            feedbackText = text;
            feedbackColor = color;
            feedbackUntil = Time.time + 0.9f;
        }

        /// <summary>
        /// Dives toward the ball if it's roughly ahead, otherwise straight forward, bending a little
        /// toward where the ball will land (<see cref="diveLockOn"/>).
        /// </summary>
        public void PressDive()
        {
            if (Time.time < cooldownUntil || motor.IsDiving) return;
            Vector3 dir = transform.forward;
            Vector3? assist = null;
            if (ball != null && !ball.IsDead)
            {
                Vector3 toBall = ball.transform.position - transform.position;
                toBall.y = 0f;
                if (toBall.sqrMagnitude < 25f && Vector3.Dot(toBall.normalized, transform.forward) > 0.2f)
                    dir = toBall;
                Vector3 toLanding = ball.PredictLanding() - transform.position;
                toLanding.y = 0f;
                if (toLanding.magnitude <= diveLockOnRange && Vector3.Dot(toLanding.normalized, dir.normalized) > 0f)
                {
                    dir = Vector3.Slerp(dir.normalized, toLanding.normalized, diveLockOn);
                    assist = transform.position + toLanding;
                }
            }
            if (!motor.StartDive(dir, assist, diveLockOn)) return;
            StartSwing(HitType.Dive, 0f, dive.cooldown, faceBall: false);
            MeasureTiming(sweetHeight.z, diveLead, GoodWindow(HitType.Dive));
        }

        bool StartSwing(HitType type, float window, float cool, bool faceBall = true, float contactDelay = 0f)
        {
            float now = Time.time;
            if (now < cooldownUntil) return false;
            swing = type;
            swingStart = now;
            contactFrom = now + contactDelay;
            activeUntil = contactFrom + window;
            cooldownUntil = now + Mathf.Max(cool, contactDelay + window);
            touchedThisSwing = false;
            swingIsServe = false;
            timingKnown = false;
            if (faceBall && ball != null)
                motor.FaceTowards(ball.transform.position);
            SwingStarted?.Invoke(type);
            return true;
        }

        float PassScale(float atZero, float atMax) =>
            skills != null ? skills.Scale(SkillType.Pass, atZero, atMax) : atZero;

        float SkillScale(SkillType type, float atZero, float atMax) =>
            skills != null ? skills.Scale(type, atZero, atMax) : atZero;

        /// <summary>The bump contact zone as it is right now (grows with Pass). Player-local.</summary>
        public Vector3 CurrentZoneSize => zoneSize * PassScale(1f, passZoneScaleAtMax);

        bool BallInZone(HitType type)
        {
            // A ball that has touched the sand is out of play: nothing can be played off it.
            if (ball.IsDead) return false;
            switch (type)
            {
                case HitType.Bump:
                {
                    Vector3 size = CurrentZoneSize;
                    // Grow forward and sideways, but keep the zone sitting on the sand.
                    Vector3 center = zoneCenter + new Vector3(0f, (size.y - zoneSize.y) * 0.5f, (size.z - zoneSize.z) * 0.5f);
                    return InBox(center, size);
                }
                case HitType.Roll:
                    return motor.IsGrounded
                        ? BallInZone(HitType.Set) || BallInZone(HitType.Bump)
                        : BallInZone(HitType.Spike);
                case HitType.Block:
                    return InBox(block.zoneCenter, block.zoneSize * SkillScale(SkillType.Block, 1f, blockZoneScaleAtMax));
                default:
                {
                    HitSettings h = Settings(type);
                    return InBox(h.zoneCenter, type == HitType.Spike ? h.zoneSize * AssistBoost : h.zoneSize);
                }
            }
        }

        bool InBox(Vector3 center, Vector3 size)
        {
            Vector3 local = transform.InverseTransformPoint(ball.transform.position);
            Vector3 half = size * 0.5f + Vector3.one * VolleyBall.Radius;
            Vector3 d = local - center;
            return Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y && Mathf.Abs(d.z) <= half.z;
        }

        HitSettings Settings(HitType type) => type switch
        {
            HitType.Set => set,
            HitType.Spike => spike,
            HitType.Roll => roll,
            HitType.Block => block,
            _ => dive,
        };

        void PlayHit(HitType type)
        {
            float contactHeight = ball.transform.position.y - transform.position.y;
            switch (type)
            {
                case HitType.Bump: Bump(TimingQuality(type)); break;
                case HitType.Set: Set(TimingQuality(type)); break;
                case HitType.Dive: Dig(TimingQuality(type)); break;
                case HitType.Spike: Spike(AttackHitQuality(spike.zoneCenter)); break;
                case HitType.Roll: Roll(AttackHitQuality(motor.IsGrounded ? set.zoneCenter : spike.zoneCenter)); break;
                case HitType.Block: Block(); break;
            }
            // Only controlled passes count for the passing drill.
            if (type == HitType.Bump || type == HitType.Set || type == HitType.Dive) Bumped?.Invoke(contactHeight);
            Played?.Invoke(type, contactHeight);
        }

        /// <summary>−1 if the player is on the −Z half, +1 otherwise: the sign of z on their own side.</summary>
        float OwnSide => transform.position.z <= 0f ? -1f : 1f;

        /// <summary>
        /// Bump: forward and high to the setter spot (centre of your half, in front of the net) so a
        /// partner can set it. Timing decides how close it lands; it never aims over the net.
        /// </summary>
        void Bump(float quality)
        {
            var target = new Vector3(0f, VolleyBall.Radius, OwnSide * setterSpotOffNet);
            float accuracy = quality * MovingPenalty();
            Vector3 v = PassTo(target, bumpApex, accuracy);
            v += motor.PlanarVelocity * (carryPlayerVelocity * (1f - accuracy));
            ball.Launch(v);
            ShowTiming(quality);
        }

        /// <summary>
        /// Set: high, close to the net, on the other side of the court from you (right → left and
        /// vice versa), where it can be attacked.
        /// </summary>
        void Set(float quality)
        {
            // "Right" as seen facing the net from your own half.
            float right = OwnSide < 0f ? transform.position.x : -transform.position.x;
            float targetRight = right < -setMiddleZone ? setAcross : -setAcross;
            float x = OwnSide < 0f ? targetRight : -targetRight;
            var target = new Vector3(x, VolleyBall.Radius, OwnSide * setOffNet);
            ball.Launch(PassTo(target, setApex, quality * MovingPenalty()));
            ShowTiming(quality);
        }

        /// <summary>Passing on the run is less accurate: full accuracy standing, movingPassAccuracy at a sprint.</summary>
        float MovingPenalty()
        {
            float t = Mathf.Clamp01(motor.PlanarVelocity.magnitude / Mathf.Max(sprintReference, 0.1f));
            return Mathf.Lerp(1f, movingPassAccuracy, t);
        }

        /// <summary>Dig: pancake the ball back up toward the middle of your half; a late or early one goes low.</summary>
        void Dig(float quality)
        {
            var target = new Vector3(0f, VolleyBall.Radius, OwnSide * digTargetOffNet);
            ball.Launch(PassTo(target, Mathf.Lerp(digApex.y, digApex.x, quality), quality));
            ShowTiming(quality);
        }

        /// <summary>
        /// Launch toward <paramref name="target"/> on your own half peaking at <paramref name="apex"/>,
        /// with landing scatter and apex error that grow as timing quality drops.
        /// </summary>
        Vector3 PassTo(Vector3 target, float apex, float quality)
        {
            Vector2 miss = Random.insideUnitCircle * Mathf.Lerp(passScatter.y, passScatter.x, quality);
            float half = CourtDimensions.HalfWidth - 0.3f;
            target.x = Mathf.Clamp(target.x + miss.x, -half, half);
            // Stay on your own side of the net.
            target.z = OwnSide * Mathf.Clamp(Mathf.Abs(target.z) + miss.y, 0.6f, CourtDimensions.SideLength - 0.3f);
            float apexErr = Mathf.Lerp(passApexError.y, passApexError.x, quality);
            float peak = Mathf.Max(apex + Random.Range(-apexErr, apexErr), ball.transform.position.y + 0.5f);
            return ball.VelocityToReach(ball.transform.position, target, peak);
        }

        /// <summary>
        /// Drives the ball down into the far court, hard but clearing the net. Timing sets the pace
        /// and accuracy; from deep in the court it becomes a slower down-ball.
        /// </summary>
        void Spike(float quality)
        {
            Vector3 origin = ball.transform.position;
            float side = FarSide();
            Vector3 fwd = AttackDirection();
            // Aim where the player is facing, kept inside the far court.
            float scatter = SkillScale(SkillType.Attack, spikeScatterRookie, spikeScatterMaster) + (1f - quality) * 1.6f;
            Vector2 jitter = Random.insideUnitCircle * scatter;

            float deep = Mathf.InverseLerp(fullAttackDepth, downBallDepth, Mathf.Abs(transform.position.z));
            float maxSpeed = Mathf.Lerp(spikeMinSpeed, spikeSpeed, quality) * Mathf.Lerp(1f, downBallSpeed, deep);
            float minSpeed = Mathf.Min(spikeMinSpeed, maxSpeed) * 0.7f;
            float netY = CourtDimensions.NetHeight + VolleyBall.Radius + netClearance + deep * 0.4f;

            // A hard spike that still clears the tape has to go deep: aim from spikeDepth back toward
            // the baseline (leaving room for scatter) and take the first spot where the full pace clears.
            Vector3 velocity = Vector3.zero;
            float bestPace = -1f;
            for (float depth = spikeDepth; depth <= CourtDimensions.SideLength - 1.2f + 0.01f; depth += 0.5f)
            {
                float x = AimAcross(origin, fwd, side, depth, 0.8f);
                var target = new Vector3(x + jitter.x, VolleyBall.Radius, side * (depth + jitter.y));
                Vector3 v = ball.DriveOverNet(origin, target, netY, maxSpeed, minSpeed);
                float pace = new Vector2(v.x, v.z).magnitude;
                if (pace > bestPace) { bestPace = pace; velocity = v; }
                if (pace >= maxSpeed - 0.01f) break;
            }

            float aimErr = SkillScale(SkillType.Attack, spike.aimErrorRookie, spike.aimErrorMaster) + (1f - quality) * 8f;
            float powerErr = SkillScale(SkillType.Attack, spike.powerErrorRookie, spike.powerErrorMaster);
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            horizontal = Quaternion.Euler(0f, Random.Range(-aimErr, aimErr), 0f) * horizontal * (1f + Random.Range(-powerErr, powerErr));
            ball.Launch(horizontal + Vector3.up * velocity.y);
            ShowTiming(quality);
        }

        /// <summary>
        /// Soft, high shot that drops in where the camera is looking: turn the camera to pick a
        /// corner. Facing away from the far court sends it deep centre. Timing and how far outside
        /// the court you are decide how close it lands.
        /// </summary>
        void Roll(float quality)
        {
            Vector3 origin = ball.transform.position;
            float side = FarSide();
            float x = AimAcross(origin, AimDirection, side, rollDepth, rollSidelineMargin);
            float scatter = SkillScale(SkillType.Attack, rollScatterRookie, rollScatterMaster) + (1f - quality) * 1.5f
                + Mathf.Min(rollScatterOutsideMax, OutsideCourt() * rollScatterPerMetreOutside);
            Vector2 jitter = Random.insideUnitCircle * scatter;
            float maxDepth = CourtDimensions.SideLength - rollSidelineMargin;
            Vector3 target = new Vector3(x + jitter.x, VolleyBall.Radius,
                side * Mathf.Clamp(rollDepth + jitter.y, 1f, maxDepth));
            if (quality >= 0.9f)
            {
                // A perfect roll drops into the deep corner on the side you're aiming at; straight
                // down the middle goes cross-court, away from where you stand.
                Vector3 aim = AttackDirection();
                float cornerSide = Mathf.Abs(aim.x) > 0.15f ? Mathf.Sign(aim.x)
                    : (transform.position.x > 0.1f ? -1f : transform.position.x < -0.1f ? 1f : -1f);
                Vector2 tiny = Random.insideUnitCircle * 0.2f;
                target = new Vector3(cornerSide * (CourtDimensions.HalfWidth - 0.7f) + tiny.x, VolleyBall.Radius,
                    side * (CourtDimensions.SideLength - 0.9f) + tiny.y);
            }
            float apex = Mathf.Max(origin.y + 1f, CourtDimensions.NetHeight + rollNetApex);
            float netTop = CourtDimensions.NetHeight + VolleyBall.Radius + 0.25f;
            ball.Launch(ball.ArcOverNet(origin, target, apex, netTop));
            ShowTiming(quality);
        }

        /// <summary>Pushes the ball straight back down just past the net.</summary>
        void Block()
        {
            Vector3 origin = ball.transform.position;
            float side = FarSide();
            Vector2 jitter = Random.insideUnitCircle * SkillScale(SkillType.Block, blockScatterRookie, blockScatterMaster);
            float depth = Random.Range(blockLandDepth.x, blockLandDepth.y) + jitter.y;
            float half = CourtDimensions.HalfWidth - rollSidelineMargin;
            Vector3 target = new Vector3(Mathf.Clamp(origin.x + jitter.x, -half, half), VolleyBall.Radius,
                side * Mathf.Max(depth, 0.5f));
            ball.Launch(ball.VelocityToReach(origin, target, origin.y + 0.15f));
        }

        /// <summary>+1 if the opponent's half is toward +Z from where the player stands, −1 otherwise.</summary>
        float FarSide() => transform.position.z <= 0f ? 1f : -1f;

        /// <summary>Horizontal direction the camera looks (body facing if there is no camera). Used to aim placed shots.</summary>
        public Vector3 AimDirection => cameraTransform != null ? Flat(cameraTransform.forward) : Flat(transform.forward);

        /// <summary>
        /// Where a shot along <paramref name="dir"/> lands across the court at <paramref name="depth"/>
        /// past the net, kept <paramref name="margin"/> inside the sidelines. Straight over if
        /// the direction doesn't point at the far side.
        /// </summary>
        public static float AimAcross(Vector3 origin, Vector3 dir, float side, float depth, float margin)
        {
            float run = Mathf.Abs(origin.z) + depth;
            float lateral = dir.z * side > 0.2f ? dir.x / Mathf.Abs(dir.z) * run : 0f;
            float half = CourtDimensions.HalfWidth - margin;
            return Mathf.Clamp(origin.x + lateral, -half, half);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        public void ResetBall()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 ground = new Vector3(transform.position.x, 0f, transform.position.z);
            ball.Place(ground + forward * resetDistance + Vector3.up * (VolleyBall.Radius + 0.01f));
        }

        /// <summary>Tosses the ball straight up in front of the player (practice helper, no key).</summary>
        public void TossBall()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            ball.Place(transform.position + forward * 0.6f + Vector3.up * 1.2f);
            ball.Launch(Vector3.up * Mathf.Sqrt(2f * ball.GravityMagnitude * tossHeight));
        }

        // Walking into a low ball pushes it along the sand instead of stopping dead against it.
        // Done by proximity rather than OnControllerColliderHit, which is unreliable for a ball this small.
        void NudgeIfWalkingIntoBall()
        {
            Vector3 toBall = ball.transform.position - transform.position;
            if (toBall.y > nudgeMaxHeight) return;

            Vector3 flat = new Vector3(toBall.x, 0f, toBall.z);
            float reach = bodyRadius + VolleyBall.Radius + nudgeReach;
            if (flat.sqrMagnitude > reach * reach || flat.sqrMagnitude < 1e-6f) return;

            Vector3 dir = flat.normalized;
            float approach = Vector3.Dot(motor.PlanarVelocity, dir);
            if (approach < 0.3f) return;
            if (Vector3.Dot(ball.Body.linearVelocity, dir) >= approach) return;

            // A dead ball rolls along but stays dead; a live one stays live.
            ball.Launch(dir * Mathf.Max(approach * 1.3f, nudgeSpeed) + Vector3.up * 0.6f, revive: false);
        }

        /// <summary>Raises the right arm briefly for an overhead serve hit.</summary>
        public void PlayServeSwing() => serveSwingUntil = Time.time + 0.3f;

        void AnimateArms(float dt)
        {
            bool held = IsSwingActive || Time.time < activeUntil + 0.1f;
            bool overhead = swing == HitType.Set || swing == HitType.Block || (swing == HitType.Roll && motor.IsGrounded);
            bool hitting = swing == HitType.Spike || (swing == HitType.Roll && !motor.IsGrounded);
            float bumpTarget = (swing == HitType.Bump && held) || (swing == HitType.Dive && motor.IsDiveReaching) ? 1f : 0f;
            armPose = Mathf.MoveTowards(armPose, bumpTarget, armSpeed * dt);
            setPose = Mathf.MoveTowards(setPose, overhead && held ? 1f : 0f, armSpeed * dt);
            spikePose = Mathf.MoveTowards(spikePose, hitting && held ? 1f : 0f, armSpeed * dt);
            servePose = Mathf.MoveTowards(servePose, Time.time < serveSwingUntil ? 1f : 0f, armSpeed * dt);
            // A single-mesh character has no separate arms; the poses still drive PlayerModelAnimator.
            if (leftArmPivot == null || rightArmPivot == null) return;
            float swingAngle = Mathf.Lerp(0f, armBumpAngle, armPose);
            float tilt = Mathf.Lerp(armRestTilt, armBumpTilt, armPose);
            Quaternion raised = Quaternion.Euler(-165f, 0f, 5f);
            Quaternion left = Quaternion.Euler(swingAngle, 0f, -tilt);
            Quaternion right = Quaternion.Euler(swingAngle, 0f, tilt);
            leftArmPivot.localRotation = Quaternion.Slerp(left, Quaternion.Euler(-160f, 0f, -12f), setPose);
            right = Quaternion.Slerp(right, Quaternion.Euler(-160f, 0f, 12f), setPose);
            rightArmPivot.localRotation = Quaternion.Slerp(right, raised, Mathf.Max(servePose, spikePose));
        }

        void OnGUI()
        {
            if (Time.time < feedbackUntil)
            {
                feedbackStyle ??= new GUIStyle(GUI.skin.label)
                {
                    fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                };
                feedbackStyle.normal.textColor = feedbackColor;
                GUI.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, 40f), feedbackText, feedbackStyle);
            }
            if (!showControlsHint) return;
            hintStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(1f, 1f, 1f, 0.85f) },
            };
            GUI.Label(new Rect(12f, Screen.height - 28f, 980f, 22f),
                "LMB bump (air: spike)   RMB set   F roll   Q block   Space jump   E dive   C serve (then LMB / F)   R reset   Tab practice   Esc menu", hintStyle);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(zoneCenter, zoneSize);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(set.zoneCenter, set.zoneSize);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(spike.zoneCenter, spike.zoneSize);
            Gizmos.color = new Color(0.7f, 0.4f, 1f, 0.6f);
            Gizmos.DrawWireCube(block.zoneCenter, block.zoneSize);
            Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.6f);
            Gizmos.DrawWireCube(dive.zoneCenter, dive.zoneSize);
        }
    }
}
