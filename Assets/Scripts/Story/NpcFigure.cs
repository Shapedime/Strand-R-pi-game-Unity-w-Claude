using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// A story character: a primitive body (for now) with a floating name tag, a little idle sway,
    /// and a smooth turn toward whoever it's looking at. <see cref="WalkTo"/> slides it across the sand.
    /// </summary>
    public class NpcFigure : MonoBehaviour
    {
        public string displayName;
        [SerializeField] Transform body;
        [SerializeField] TextMesh nameTag;
        [SerializeField] float turnSpeed = 240f;
        [SerializeField] float walkSpeed = 2.2f;

        Transform lookTarget;
        Vector3? walkTarget;
        float phase;

        public bool IsWalking => walkTarget.HasValue;

        void Awake() => phase = Random.value * 10f;

        public void LookAt(Transform target) => lookTarget = target;
        public void WalkTo(Vector3 point) => walkTarget = new Vector3(point.x, transform.position.y, point.z);
        public void ShowName(bool show) { if (nameTag != null) nameTag.gameObject.SetActive(show); }

        void Update()
        {
            float dt = Time.deltaTime;
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
            else if (lookTarget != null)
            {
                face = lookTarget.position - transform.position;
                face.y = 0f;
            }
            if (face.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), turnSpeed * dt);

            if (body != null)
            {
                // Breathing sway while standing, a bob while walking.
                phase += dt * (walkTarget.HasValue ? 9f : 1.6f);
                float bob = walkTarget.HasValue ? Mathf.Abs(Mathf.Sin(phase)) * 0.05f : Mathf.Sin(phase) * 0.01f;
                body.localPosition = new Vector3(0f, bob, 0f);
                body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.5f) * (walkTarget.HasValue ? 3f : 1f));
            }

            // Name tags always face the camera.
            if (nameTag != null && nameTag.gameObject.activeSelf && Camera.main != null)
                nameTag.transform.rotation = Quaternion.LookRotation(nameTag.transform.position - Camera.main.transform.position);
        }
    }
}
