using UnityEngine;

namespace DecalMini.Runtime.Modules.Dynamic
{
    /// <summary>
    /// DECAL MODULE ANCHOR: A marker component placed by artists in the prefab hierarchy.
    /// Used by decal modules to auto-locate their center of influence without manual linking.
    /// </summary>
    public class DecalModuleAnchor : MonoBehaviour
    {
        [Tooltip("Optional ID to distinguish between multiple anchors (e.g., 'Feet', 'Hands')")]
        public string anchorId = "Default";

        private void OnDrawGizmos()
        {
            // Visualize the anchor in scene view for easier adjustment
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.2f);
            Gizmos.DrawIcon(transform.position, "d_Anchor@2x", true);
        }
    }
}
