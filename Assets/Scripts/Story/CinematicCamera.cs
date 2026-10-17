using System.Collections;
using BeachVolley.CameraRig;
using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Story camera on the main camera. Three modes: cutscene shots (cut or smooth blend to a
    /// <see cref="CameraShot"/>), a fixed room camera that turns to keep the player in view, and
    /// handing back to <see cref="ThirdPersonFollow"/> for gameplay.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CinematicCamera : MonoBehaviour
    {
        [SerializeField] float trackSharpness = 4f;

        enum Mode { Shot, Track, Follow }

        Camera cam;
        ThirdPersonFollow follow;
        Mode mode = Mode.Shot;
        Transform trackTarget;
        float defaultFov;

        void Awake()
        {
            cam = GetComponent<Camera>();
            follow = GetComponent<ThirdPersonFollow>();
            defaultFov = cam.fieldOfView;
        }

        public void Cut(CameraShot shot)
        {
            if (shot == null) return;
            SetMode(Mode.Shot);
            transform.SetPositionAndRotation(shot.transform.position, shot.transform.rotation);
            cam.fieldOfView = shot.fieldOfView;
        }

        public IEnumerator Blend(CameraShot shot, float duration)
        {
            if (shot == null) yield break;
            yield return BlendTo(shot.transform.position, shot.transform.rotation, shot.fieldOfView, duration);
        }

        /// <summary>Cuts to a camera at <paramref name="position"/> looking at <paramref name="lookAt"/>.</summary>
        public void Frame(Vector3 position, Vector3 lookAt, float fov = 50f)
        {
            SetMode(Mode.Shot);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            cam.fieldOfView = fov;
        }

        public IEnumerator BlendFrame(Vector3 position, Vector3 lookAt, float duration, float fov = 50f) =>
            BlendTo(position, Quaternion.LookRotation(lookAt - position, Vector3.up), fov, duration);

        IEnumerator BlendTo(Vector3 position, Quaternion rotation, float fov, float duration)
        {
            SetMode(Mode.Shot);
            Vector3 p0 = transform.position;
            Quaternion r0 = transform.rotation;
            float f0 = cam.fieldOfView;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                transform.SetPositionAndRotation(Vector3.Lerp(p0, position, k), Quaternion.Slerp(r0, rotation, k));
                cam.fieldOfView = Mathf.Lerp(f0, fov, k);
                yield return null;
            }
            transform.SetPositionAndRotation(position, rotation);
            cam.fieldOfView = fov;
        }

        /// <summary>Stands at <paramref name="anchor"/> and keeps turning toward <paramref name="target"/>.</summary>
        public void Track(CameraShot anchor, Transform target)
        {
            Cut(anchor);
            trackTarget = target;
            SetMode(Mode.Track);
        }

        /// <summary>Back to the gameplay orbit camera behind the player.</summary>
        public void Follow()
        {
            cam.fieldOfView = defaultFov;
            SetMode(Mode.Follow);
        }

        void SetMode(Mode m)
        {
            mode = m;
            if (follow != null)
            {
                bool on = m == Mode.Follow;
                if (on && !follow.enabled) { follow.enabled = true; follow.Target = follow.Target; }
                else if (!on) follow.enabled = false;
            }
        }

        void LateUpdate()
        {
            if (mode != Mode.Track || trackTarget == null) return;
            Vector3 look = trackTarget.position + Vector3.up * 1.1f - transform.position;
            var want = Quaternion.LookRotation(look, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-trackSharpness * Time.deltaTime));
        }
    }
}
