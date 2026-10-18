using System.Collections;
using System.Collections.Generic;
using BeachVolley.Player;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// A person in a story scene: Steve, Dave, Marlon, extras, the TV players, and the protagonist
    /// during cutscenes. With a rigged humanoid it drives the StoryActor controller (locomotion from
    /// real movement, gestures, sitting, talking, volleyball touches, head look-at). With the
    /// primitive placeholder body it fakes the same calls with its arm pivots, so scripts never need
    /// to know which one they have. A gesture whose clip isn't in the controller is skipped.
    /// </summary>
    public class StoryActor : MonoBehaviour
    {
        public string displayName;
        [SerializeField] Animator animator;
        [Tooltip("Controller used for acting (built by Build Story Characters).")]
        [SerializeField] RuntimeAnimatorController actingController;
        [Tooltip("Placeholder body root (primitive figure) for procedural sway and gestures.")]
        [SerializeField] Transform body;
        [SerializeField] TextMesh nameTag;
        [SerializeField] float turnSpeed = 300f;
        [SerializeField] float walkSpeed = 1.6f;
        [Tooltip("Held pose from the start (e.g. Sitting_Cheer for fans in the stands).")]
        [SerializeField] string idlePose;
        [Tooltip("True on the player: its own motor moves it, the actor only animates during cutscenes.")]
        [SerializeField] bool externallyMoved;

        static readonly int SpeedId = Animator.StringToHash("Speed");

        Transform lookTarget;
        Vector3? walkTarget;
        float phase;
        Vector3 lastPos;
        float speed;
        float lookWeight;
        bool acting;
        RuntimeAnimatorController gameplayController;
        readonly Dictionary<string, AnimatorControllerParameterType> parameters = new Dictionary<string, AnimatorControllerParameterType>();
        Transform armL, armR;
        Coroutine fakeGesture;
        bool fakeTalking, fakeSitting, seated;
        Transform held;
        Transform autoProp;         // a bottle or phone made for a gesture (destroyed when it ends)
        string autoFor;
        Transform heldParent;
        Vector3 heldPos;
        Quaternion heldRot;

        /// <summary>
        /// How far (m) the sit-down clip carries the hips back: someone about to sit stands this far
        /// in front of the seat, and their hips end up on it.
        /// </summary>
        public const float SitTravel = 0.48f;

        public bool IsWalking => walkTarget.HasValue;
        public bool HasRig => animator != null && animator.avatar != null && animator.avatar.isHuman;
        public Transform Head => HasRig ? animator.GetBoneTransform(HumanBodyBones.Head) : transform;

        /// <summary>World point a camera should aim at for this person's face.</summary>
        public Vector3 FacePoint => HasRig && Head != null ? Head.position : transform.position + Vector3.up * 1.6f;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            phase = Random.value * 10f;
            lastPos = transform.position;
            if (body != null)
            {
                armL = FindDeep(body, "ArmPivot_L");
                armR = FindDeep(body, "ArmPivot_R");
            }
            if (!externallyMoved) BeginActing();
        }

        void Start()
        {
            if (!string.IsNullOrEmpty(idlePose)) Gesture(idlePose);
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // ---------- Acting on/off (the player switches between gameplay and story animation) ----------

        /// <summary>Puts the acting controller on (for the player: and pauses the gameplay animator driver).</summary>
        public void BeginActing()
        {
            if (acting) return;
            acting = true;
            if (externallyMoved && TryGetComponent(out PlayerAnimatorDriver driver)) driver.enabled = false;
            if (animator != null && actingController != null)
            {
                if (gameplayController == null) gameplayController = animator.runtimeAnimatorController;
                animator.runtimeAnimatorController = actingController;
            }
            CacheParameters();
        }

        /// <summary>Back to gameplay animation (player only).</summary>
        public void EndActing()
        {
            if (!acting) return;
            acting = false;
            Release();
            if (animator != null && gameplayController != null) animator.runtimeAnimatorController = gameplayController;
            if (externallyMoved && TryGetComponent(out PlayerAnimatorDriver driver)) driver.enabled = true;
            CacheParameters();
        }

        void CacheParameters()
        {
            parameters.Clear();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters) parameters[p.name] = p.type;
        }

        // ---------- Script API ----------

        public void LookAt(Transform target) => lookTarget = target;
        public void WalkTo(Vector3 point) => walkTarget = new Vector3(point.x, transform.position.y, point.z);
        public void Stop() => walkTarget = null;
        public void ShowName(bool show) { if (nameTag != null) nameTag.gameObject.SetActive(show); }

        /// <summary>One-shot gesture ("Wave", "Clap", "Nod"...) or a held pose ("ArmsCrossed", "Texting").</summary>
        public void Gesture(string name)
        {
            if (!acting) return;
            AutoProp(name);
            if (HasRig && parameters.TryGetValue(name, out var type))
            {
                if (type == AnimatorControllerParameterType.Trigger) animator.SetTrigger(name);
                else if (type == AnimatorControllerParameterType.Bool) animator.SetBool(name, true);
                return;
            }
            if (!HasRig)
            {
                if (HeldPoses.Contains(name)) heldPoses.Add(name);
                Fake(name, true);
            }
        }

        /// <summary>Poses that stay up until EndPose (the rest are one-shots).</summary>
        public static readonly HashSet<string> HeldPoses = new HashSet<string> { "ArmsCrossed", "Texting", "Tired_Idle" };

        /// <summary>Ends a held pose started with <see cref="Gesture"/>.</summary>
        public void EndPose(string name)
        {
            if (autoFor == name) DropAutoProp();
            if (HasRig && parameters.TryGetValue(name, out var type) && type == AnimatorControllerParameterType.Bool)
                animator.SetBool(name, false);
            if (!HasRig) heldPoses.Remove(name);
        }

        public void Talk(bool talking)
        {
            if (!acting) return;
            if (HasRig) SetBool("Talking", talking);
            else fakeTalking = talking;
        }

        public void Sit(bool sitting)
        {
            // Seated people turn their head (IK) toward whoever they look at, not their whole body.
            seated = sitting;
            if (HasRig) SetBool("Sitting", sitting);
            else fakeSitting = sitting;
        }

        /// <summary>A volleyball touch for scripted play: Bump, Set, Spike, Block, Dive.</summary>
        public void Volley(HitType type)
        {
            string trigger = type == HitType.Roll ? "Spike" : type.ToString();
            if (HasRig && parameters.ContainsKey(trigger)) animator.SetTrigger(trigger);
            else if (!HasRig) Fake(trigger, true);
        }

        void SetBool(string name, bool value)
        {
            if (parameters.ContainsKey(name)) animator.SetBool(name, value);
        }

        /// <summary>Puts a prop (phone, bottle, ball) in the right hand until <see cref="Release"/>.</summary>
        public void Hold(Transform prop, Vector3 localPosition, Vector3 localEuler, bool leftHand = false)
        {
            Release();
            Transform hand = HasRig ? animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand) : leftHand ? armL : armR;
            if (prop == null || hand == null) return;
            held = prop;
            heldParent = prop.parent;
            heldPos = prop.localPosition;
            heldRot = prop.localRotation;
            prop.SetParent(hand, true);
            // Offsets are in metres along the hand's axes, whatever scale the rig's bones have
            // (the cast's models import with different bone scales).
            Vector3 offset = HasRig ? localPosition : new Vector3(0f, -0.6f, 0.05f);
            prop.SetPositionAndRotation(hand.position + hand.rotation * offset, hand.rotation * Quaternion.Euler(localEuler));
        }

        /// <summary>Puts the held prop back where it came from.</summary>
        public void Release()
        {
            if (held == null) return;
            if (held == autoProp) { DropAutoProp(); return; }
            held.SetParent(heldParent, false);
            held.localPosition = heldPos;
            held.localRotation = heldRot;
            held = null;
        }

        // ---------- Props for gestures ----------

        /// <summary>
        /// Drinking and texting need something in the hand. If the script didn't give the actor a
        /// prop, a water bottle or a phone appears for the gesture and goes away after it.
        /// </summary>
        void AutoProp(string gesture)
        {
            if (held != null || !HasRig) return;
            bool drink = gesture == "Drink", phone = gesture == "Texting";
            if (!drink && !phone) return;
            var prop = drink ? MakeProp("Bottle (prop)", PrimitiveType.Cylinder, new Vector3(0.07f, 0.11f, 0.07f), new Color(0.3f, 0.8f, 0.95f))
                             : MakeProp("Phone (prop)", PrimitiveType.Cube, new Vector3(0.075f, 0.012f, 0.15f), new Color(0.06f, 0.06f, 0.07f));
            // The Drinking clip raises the left hand: bottle in the left fist. The phone sits flat in
            // the right palm, screen up (same as the bedroom phone).
            if (drink) Hold(prop, new Vector3(-0.03f, 0.08f, 0.04f), new Vector3(0f, 0f, -90f), leftHand: true);
            else Hold(prop, new Vector3(0.05f, 0.08f, 0.03f), new Vector3(0f, 90f, 0f));
            autoProp = prop;
            autoFor = gesture;
            if (drink) StartCoroutine(DropAfterGesture("Drink"));
        }

        static Material propMaterial;

        Transform MakeProp(string name, PrimitiveType type, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (propMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = r.sharedMaterial.shader;
                propMaterial = new Material(shader);
            }
            var mat = new Material(propMaterial);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>Drops the prop once the gesture's clip has played (its length, from the controller).</summary>
        IEnumerator DropAfterGesture(string clipName)
        {
            float length = 3f;
            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (var c in animator.runtimeAnimatorController.animationClips)
                    if (c != null && c.name == clipName) { length = c.length; break; }
            yield return new WaitForSeconds(length + 0.2f);
            if (autoFor == clipName) DropAutoProp();
        }

        void DropAutoProp()
        {
            if (autoProp == null) { autoFor = null; return; }
            if (held == autoProp) held = null;
            Destroy(autoProp.gameObject);
            autoProp = null;
            autoFor = null;
        }

        // ---------- Per frame ----------

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!externallyMoved)
            {
                Vector3 face = transform.forward;
                if (walkTarget.HasValue)
                {
                    Vector3 to = walkTarget.Value - transform.position;
                    to.y = 0f;
                    if (to.magnitude < 0.05f) walkTarget = null;
                    else
                    {
                        transform.position += Vector3.ClampMagnitude(to.normalized * walkSpeed * dt, to.magnitude);
                        face = to;
                    }
                }
                else if (lookTarget != null && !seated)
                {
                    face = lookTarget.position - transform.position;
                    face.y = 0f;
                }
                if (face.sqrMagnitude > 1e-4f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), turnSpeed * dt);
            }

            // Locomotion speed from how the actor really moved (scripted walks, ScriptedPlay runs, the motor).
            Vector3 delta = transform.position - lastPos;
            delta.y = 0f;
            lastPos = transform.position;
            speed = Mathf.Lerp(speed, delta.magnitude / dt, 1f - Mathf.Exp(-10f * dt));
            if (acting && HasRig && parameters.ContainsKey("Speed")) animator.SetFloat(SpeedId, speed);

            if (!HasRig) AnimatePlaceholder(dt);

            if (nameTag != null && nameTag.gameObject.activeSelf && Camera.main != null)
                nameTag.transform.rotation = Quaternion.LookRotation(nameTag.transform.position - Camera.main.transform.position);
        }

        void OnAnimatorIK(int layer)
        {
            if (!acting || animator == null || layer != 0) return;
            bool want = lookTarget != null;
            lookWeight = Mathf.MoveTowards(lookWeight, want ? 1f : 0f, Time.deltaTime * 2f);
            animator.SetLookAtWeight(lookWeight, 0.15f, 0.65f, 1f, 0.5f);
            if (lookTarget != null)
            {
                var other = lookTarget.GetComponent<StoryActor>();
                animator.SetLookAtPosition(other != null ? other.FacePoint : lookTarget.position + Vector3.up * 1.5f);
            }
        }

        // ---------- Placeholder (primitive body) acting ----------

        void AnimatePlaceholder(float dt)
        {
            if (body == null) return;
            bool moving = speed > 0.3f;
            phase += dt * (moving ? 9f : 1.6f);
            float bob = moving ? Mathf.Abs(Mathf.Sin(phase)) * 0.05f : Mathf.Sin(phase) * 0.01f;
            float sit = fakeSitting ? -0.42f : 0f;
            body.localPosition = new Vector3(0f, bob + sit, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.5f) * (moving ? 3f : 1f));
            if (fakeGesture == null && armL != null && armR != null)
            {
                // Talking: small hand movement; otherwise arms swing a little with the walk.
                float swing = moving ? Mathf.Sin(phase) * 25f : fakeTalking ? Mathf.Sin(Time.time * 6f) * 12f - 20f : 0f;
                armL.localRotation = Quaternion.Euler(-swing, 0f, -8f);
                armR.localRotation = Quaternion.Euler(fakeTalking ? swing : swing, 0f, 8f);
            }
        }

        void Fake(string name, bool on)
        {
            if (armL == null || armR == null) return;
            if (fakeGesture != null) StopCoroutine(fakeGesture);
            fakeGesture = on ? StartCoroutine(FakeGesture(name)) : null;
        }

        IEnumerator FakeGesture(string name)
        {
            // (left, right) shoulder pitch in degrees: negative raises the arm forward/up.
            float duration = 0.9f;
            for (float t = 0f; t < duration || IsHeld(name); t += Time.deltaTime)
            {
                float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                if (IsHeld(name)) k = 1f;
                float l = 0f, r = 0f, lz = -8f, rz = 8f;
                switch (name)
                {
                    case "Wave": r = -160f * k; rz = 8f + Mathf.Sin(t * 14f) * 20f * k; break;
                    case "Clap": l = r = -80f * k; lz = -8f + Mathf.Sin(t * 18f) * 18f; rz = 8f - Mathf.Sin(t * 18f) * 18f; break;
                    case "Cheer": case "FistPump": l = r = -170f * k; break;
                    case "Point": r = -90f * k; break;
                    case "Shrug": lz = -8f - 30f * k; rz = 8f + 30f * k; break;
                    case "ArmsCrossed": l = r = -60f * k; lz = 50f * k; rz = -50f * k; break;
                    case "Texting": case "Drink": r = -70f * k; break;
                    case "Bump": l = r = -60f * k; lz = 10f * k; rz = -10f * k; break;
                    case "Set": l = r = -170f * k; lz = -15f; rz = 15f; break;
                    case "Spike": r = Mathf.Lerp(-170f, 30f, Mathf.Clamp01(t / duration)); l = -60f * k; break;
                    case "Block": l = r = -175f * k; break;
                    default: k = 0f; break;
                }
                armL.localRotation = Quaternion.Euler(l, 0f, lz);
                armR.localRotation = Quaternion.Euler(r, 0f, rz);
                yield return null;
            }
            fakeGesture = null;
        }

        readonly HashSet<string> heldPoses = new HashSet<string>();
        bool IsHeld(string name) => heldPoses.Contains(name);
    }
}
