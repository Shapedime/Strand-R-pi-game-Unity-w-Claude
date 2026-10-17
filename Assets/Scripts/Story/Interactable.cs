using UnityEngine;

namespace BeachVolley.Story
{
    /// <summary>
    /// Something in the room the player can use: standing within <see cref="radius"/> shows the
    /// prompt and E (or gamepad South) uses it. The director decides what happens; this only
    /// reports <see cref="Used"/>. Optional <see cref="hideOnUse"/> object disappears (picked up).
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        public string id;
        public string prompt = "Use";
        public float radius = 1.4f;
        public GameObject hideOnUse;

        public bool Used { get; private set; }
        public bool Available { get; set; } = true;

        public bool InRange(Vector3 playerPos)
        {
            Vector3 d = transform.position - playerPos;
            d.y = 0f;
            return Available && d.sqrMagnitude <= radius * radius;
        }

        public void Use()
        {
            Used = true;
            if (hideOnUse != null) hideOnUse.SetActive(false);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
