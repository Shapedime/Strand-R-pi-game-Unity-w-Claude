using System.Collections;
using BeachVolley.CameraRig;
using Unity.Cinemachine;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Story camera, built on Cinemachine. Two virtual cameras take turns: a shot is posed on the
    /// idle one and the brain cuts or blends to it, so every move is a real Cinemachine blend with
    /// subtle handheld noise. A shot can stay fixed or keep a person framed (look target). For
    /// gameplay the brain switches off and <see cref="ThirdPersonFollow"/> takes the camera back.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CinematicCamera : MonoBehaviour
    {
        [Tooltip("Handheld noise profile for cutscene shots (Cinemachine preset).")]
        [SerializeField] NoiseSettings handheld;
        [SerializeField] float handheldAmplitude = 0.35f;
        [SerializeField] float handheldFrequency = 0.6f;

        enum Mode { Shot, Follow }

        class Rig
        {
            public CinemachineCamera vcam;
            public CinemachineRotationComposer composer;
            public CinemachineBasicMultiChannelPerlin noise;
        }

        Camera cam;
        CinemachineBrain brain;
        ThirdPersonFollow follow;
        Rig a, b, live;
        Mode mode = Mode.Shot;
        float defaultFov;
        int priority = 10;

        void Awake()
        {
            cam = GetComponent<Camera>();
            follow = GetComponent<ThirdPersonFollow>();
            defaultFov = cam.fieldOfView;
            brain = GetComponent<CinemachineBrain>();
            if (brain == null) brain = gameObject.AddComponent<CinemachineBrain>();
            var rigRoot = new GameObject("CinematicRig").transform;
            a = MakeRig(rigRoot, "ShotA");
            b = MakeRig(rigRoot, "ShotB");
            Pose(a, transform.position, transform.rotation, cam.fieldOfView, null, Vector3.zero, 0f);
            live = a;
            a.vcam.Priority = priority;
            // Story scenes open on a cutscene, not on the gameplay camera.
            if (follow != null) follow.enabled = false;
        }

        Rig MakeRig(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rig = new Rig
            {
                vcam = go.AddComponent<CinemachineCamera>(),
                composer = go.AddComponent<CinemachineRotationComposer>(),
                noise = go.AddComponent<CinemachineBasicMultiChannelPerlin>(),
            };
            rig.vcam.Priority = 0;
            rig.composer.Damping = new Vector2(0.6f, 0.6f);
            rig.noise.NoiseProfile = handheld;
            return rig;
        }

        void Pose(Rig rig, Vector3 position, Quaternion rotation, float fov, Transform lookTarget, Vector3 lookOffset, float shake)
        {
            rig.vcam.transform.SetPositionAndRotation(position, rotation);
            rig.vcam.Lens.FieldOfView = fov;
            rig.vcam.LookAt = lookTarget;
            rig.composer.enabled = lookTarget != null;
            rig.composer.TargetOffset = lookOffset;
            rig.noise.enabled = handheld != null && shake > 0f;
            rig.noise.AmplitudeGain = handheldAmplitude * shake;
            rig.noise.FrequencyGain = handheldFrequency;
            rig.vcam.PreviousStateIsValid = false;
        }

        /// <summary>Poses the idle camera and makes it live, cutting (duration 0) or blending.</summary>
        void Go(Vector3 position, Quaternion rotation, float fov, Transform lookTarget, Vector3 lookOffset, float duration, float shake = 1f)
        {
            SetMode(Mode.Shot);
            var next = live == a ? b : a;
            Pose(next, position, rotation, fov, lookTarget, lookOffset, shake);
            brain.DefaultBlend = duration > 0f
                ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, duration)
                : new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            next.vcam.Priority = ++priority;
            // Scripts pose shots from Update/coroutines, before the brain's LateUpdate, so a cut
            // shows on this same frame.
            live = next;
        }

        static Quaternion Look(Vector3 from, Vector3 to) => Quaternion.LookRotation(to - from, Vector3.up);

        // ---------- Fixed shots ----------

        public void Cut(CameraShot shot)
        {
            if (shot == null) return;
            Go(shot.transform.position, shot.transform.rotation, shot.fieldOfView, null, Vector3.zero, 0f, shot.shake);
        }

        public IEnumerator Blend(CameraShot shot, float duration)
        {
            if (shot == null) yield break;
            Go(shot.transform.position, shot.transform.rotation, shot.fieldOfView, null, Vector3.zero, duration, shot.shake);
            yield return new WaitForSeconds(duration);
        }

        /// <summary>Cuts to a camera at <paramref name="position"/> looking at <paramref name="lookAt"/>.</summary>
        public void Frame(Vector3 position, Vector3 lookAt, float fov = 50f) =>
            Go(position, Look(position, lookAt), fov, null, Vector3.zero, 0f);

        public IEnumerator BlendFrame(Vector3 position, Vector3 lookAt, float duration, float fov = 50f)
        {
            Go(position, Look(position, lookAt), fov, null, Vector3.zero, duration);
            yield return new WaitForSeconds(duration);
        }

        // ---------- Shots that keep someone framed ----------

        /// <summary>Camera at <paramref name="position"/> that keeps <paramref name="target"/> framed (offset = aim point above its feet).</summary>
        public void FrameOn(Vector3 position, Transform target, Vector3 offset, float fov = 45f, float blend = 0f)
        {
            Vector3 aim = target != null ? target.position + offset : position + transform.forward;
            Go(position, Look(position, aim), fov, target, offset, blend);
        }

        /// <summary>Stands at <paramref name="anchor"/> and keeps turning toward <paramref name="target"/>.</summary>
        public void Track(CameraShot anchor, Transform target)
        {
            if (anchor == null) return;
            Go(anchor.transform.position, anchor.transform.rotation, anchor.fieldOfView, target, Vector3.up * 1.1f, 0f, 0.5f);
        }

        /// <summary>A slow dolly move from → to over <paramref name="duration"/>, keeping <paramref name="target"/> framed.</summary>
        public IEnumerator Dolly(Vector3 from, Vector3 to, Transform target, Vector3 offset, float duration, float fov = 50f)
        {
            Go(from, Look(from, target.position + offset), fov, target, offset, 0f, 0.6f);
            var t = live.vcam.transform;
            for (float e = 0f; e < duration; e += Time.deltaTime)
            {
                t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, e / duration));
                yield return null;
            }
            t.position = to;
        }

        /// <summary>Back to the gameplay orbit camera behind the player.</summary>
        public void Follow()
        {
            cam.fieldOfView = defaultFov;
            SetMode(Mode.Follow);
        }

        void SetMode(Mode m)
        {
            bool wasFollow = mode == Mode.Follow;
            mode = m;
            bool on = m == Mode.Follow;
            if (follow != null)
            {
                if (on && !follow.enabled) { follow.enabled = true; follow.Target = follow.Target; }
                else if (!on) follow.enabled = false;
            }
            brain.enabled = !on;
            if (wasFollow && !on)
            {
                // Coming out of gameplay: start from where the gameplay camera is, no blend from a stale shot.
                Pose(live, transform.position, transform.rotation, cam.fieldOfView, null, Vector3.zero, 0f);
            }
        }
    }
}
