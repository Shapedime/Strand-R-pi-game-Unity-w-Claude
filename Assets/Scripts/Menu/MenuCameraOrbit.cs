using UnityEngine;

namespace BeachVolley.Menu
{
    /// <summary>Slow orbit around the court behind the main menu.</summary>
    public class MenuCameraOrbit : MonoBehaviour
    {
        [SerializeField] Vector3 center = new Vector3(0f, 1.2f, 0f);
        [SerializeField] float radius = 15f;
        [SerializeField] float height = 4.5f;
        [SerializeField] float degreesPerSecond = 5f;

        float angle = 200f;

        void LateUpdate()
        {
            angle += degreesPerSecond * Time.unscaledDeltaTime;
            float a = angle * Mathf.Deg2Rad;
            transform.position = center + new Vector3(Mathf.Sin(a) * radius, height, Mathf.Cos(a) * radius);
            transform.LookAt(center);
        }
    }
}
