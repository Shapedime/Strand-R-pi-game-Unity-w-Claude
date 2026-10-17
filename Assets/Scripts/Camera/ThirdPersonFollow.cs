using BeachVolley.Ball;
using BeachVolley.Core;
using BeachVolley.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeachVolley.CameraRig
{
    /// <summary>
    /// Third-person camera that sits behind the player. Mouse (while the cursor is
    /// locked) or right stick orbits; when the player walks forward and the view
    /// hasn't been touched for a moment, it eases back behind the player.
    /// When a live ball goes above the top of the screen (a high set), the view tilts up to keep
    /// it in frame and eases back once it drops; any manual look input cancels that.
    /// Click the Game view to lock the cursor (Esc releases it via the pause menu).
    /// </summary>
    public class ThirdPersonFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] float distance = 5f;
        [SerializeField] float pivotHeight = 1.5f;
        [SerializeField] float defaultPitch = 18f;
        [SerializeField] float minPitch = -5f;
        [SerializeField] float maxPitch = 60f;
        [SerializeField] float minCameraHeight = 0.4f;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float stickSpeed = 140f;
        [SerializeField] float recenterDelay = 1f;
        [SerializeField] float recenterSpeed = 120f;
        [SerializeField] float followSharpness = 12f;

        [Header("Keep a high ball in view")]
        [Tooltip("Start tilting up when the ball is above this fraction of the screen height...")]
        [SerializeField] float lookUpTrigger = 0.85f;
        [Tooltip("...and stop easing back down until it is below this.")]
        [SerializeField] float lookUpRelease = 0.65f;
        [Tooltip("Most the view tilts up (degrees subtracted from the pitch).")]
        [SerializeField] float lookUpMax = 38f;
        [SerializeField] float lookUpSpeed = 90f;
        [Tooltip("The pivot rises this much at full tilt so the player stays in frame.")]
        [SerializeField] float lookUpPivotLift = 0.8f;
        [Tooltip("Only for a ball within this horizontal distance of the player.")]
        [SerializeField] float lookUpRange = 12f;

        PlayerMotor motor;
        VolleyBall ball;
        Camera cam;
        float lookUp;
        InputAction lookMouse;
        InputAction lookStick;
        float yaw;
        float pitch;
        float lastManualLookTime = float.NegativeInfinity;
        Vector3 smoothedPivot;

        public Transform Target
        {
            get => target;
            set { target = value; Snap(); }
        }

        void Awake()
        {
            lookMouse = new InputAction("LookMouse", InputActionType.PassThrough, "<Mouse>/delta");
            lookStick = new InputAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");
            cam = GetComponent<Camera>();
            ball = FindAnyObjectByType<VolleyBall>();
        }

        void OnEnable()
        {
            lookMouse.Enable();
            lookStick.Enable();
        }

        void OnDisable()
        {
            lookMouse.Disable();
            lookStick.Disable();
        }

        void OnDestroy()
        {
            lookMouse.Dispose();
            lookStick.Dispose();
        }

        void Start() => Snap();

        void Snap()
        {
            if (target == null) return;
            motor = target.GetComponent<PlayerMotor>();
            yaw = target.eulerAngles.y;
            pitch = defaultPitch;
            smoothedPivot = target.position + Vector3.up * pivotHeight;
            Apply();
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            UpdateCursorLock();

            Vector2 look = lookStick.ReadValue<Vector2>() * (stickSpeed * dt);
            if (Cursor.lockState == CursorLockMode.Locked)
                look += lookMouse.ReadValue<Vector2>() * mouseSensitivity;

            if (look.sqrMagnitude > 1e-6f)
            {
                yaw += look.x;
                pitch -= look.y;
                lastManualLookTime = Time.time;
                // Manual control always wins over the high-ball assist.
                lookUp = 0f;
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Drift back behind the player only while walking forward, so strafing doesn't spin the view.
            if (motor != null && Time.time - lastManualLookTime > recenterDelay && motor.MoveInput.y > 0.1f)
                yaw = Mathf.MoveTowardsAngle(yaw, target.eulerAngles.y, recenterSpeed * motor.MoveInput.y * dt);

            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            smoothedPivot = Vector3.Lerp(smoothedPivot, pivot, 1f - Mathf.Exp(-followSharpness * dt));
            UpdateLookUp(dt);
            Apply();
        }

        // Tilts up while a live ball near the player is off the top of the screen, holds while it is
        // in the upper part, and eases back once it drops (or dies, or goes far away).
        void UpdateLookUp(float dt)
        {
            float target01 = 0f;
            bool hold = false;
            if (ball != null && cam != null && !ball.IsDead && Time.time - lastManualLookTime > recenterDelay)
            {
                Vector3 b = ball.transform.position;
                Vector3 d = b - this.target.position;
                if (new Vector2(d.x, d.z).magnitude <= lookUpRange)
                {
                    Vector3 vp = cam.WorldToViewportPoint(b);
                    bool above = vp.z <= 0f ? b.y > smoothedPivot.y : vp.y > lookUpTrigger;
                    hold = vp.z > 0f && vp.y >= lookUpRelease;
                    if (above) target01 = 1f;
                }
            }
            if (target01 > 0f) lookUp = Mathf.MoveTowards(lookUp, lookUpMax, lookUpSpeed * dt);
            else if (!hold) lookUp = Mathf.MoveTowards(lookUp, 0f, lookUpSpeed * 0.6f * dt);
        }

        void Apply()
        {
            Quaternion rotation = Quaternion.Euler(pitch - lookUp, yaw, 0f);
            Vector3 pivot = smoothedPivot + Vector3.up * (lookUpPivotLift * lookUp / Mathf.Max(lookUpMax, 1f));
            Vector3 position = pivot - rotation * Vector3.forward * distance;
            if (position.y < minCameraHeight)
                position.y = minCameraHeight;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(pivot - position, Vector3.up));
        }

        static void UpdateCursorLock()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked
                     && !UiPointerGate.IsPointerOverUi)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
