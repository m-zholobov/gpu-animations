using UnityEngine;

namespace Crowd
{
    public class DestinationPoint : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);
        }
#endif
    }
}
