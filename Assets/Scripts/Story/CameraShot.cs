using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// A named camera position for cutscenes. Its transform is the camera pose; the director finds
    /// shots by GameObject name ("Shot_TV", "Shot_Steve"...).
    /// </summary>
    public class CameraShot : MonoBehaviour
    {
        public float fieldOfView = 50f;
        [Tooltip("Handheld noise strength for this shot (0 = locked off, 1 = normal).")]
        public float shake = 1f;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, fieldOfView, 1.2f, 0.1f, 16f / 9f);
        }
    }
}
