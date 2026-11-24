using UnityEngine;

namespace CodenameVelvet.Map
{
    /// <summary>
    /// Vertical ladder (thin along its local Z). Its trigger lets a nearby player climb it with Interact; the
    /// path up is worked out from the collider box and the ground found behind the top.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(BoxCollider))]
    public class Ladder : MonoBehaviour
    {
        private const float LandingProbeHeight = 4f; // how far above the top the ledge may be
        private const float StandDistance = 1.8f; // the sprite leans back toward the ladder: closer, the rungs cut through it

        [Tooltip("How close (world units) the player must be to interact.")]
        [SerializeField, Min(0f)] private float reach = 1.5f;
        [Tooltip("How far past the ladder the player ends up, on the ledge.")]
        [SerializeField, Min(0f)] private float exitDistance = 1.5f;

        /// <summary>Someone is on it: nobody else can start until they are done.</summary>
        public bool IsOccupied { get; set; }

        private Vector3 center;
        private float halfHeight;

        // The box keeps the exact mesh size: the world-size reach is added in Awake, when the final scale is known.
        private void Reset()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null) return;
            box.center = mesh.bounds.center;
            box.size = mesh.bounds.size;
        }

        private void Awake()
        {
            var box = GetComponent<BoxCollider>();
            Vector3 scale = transform.lossyScale;
            center = transform.TransformPoint(box.center);
            halfHeight = box.size.y * scale.y * 0.5f;
            box.size += new Vector3(2f * reach / scale.x, 2f * reach / scale.y, 2f * reach / scale.z);
        }

        /// <summary>
        /// Points to walk through, in order (foot, top, ledge), for a player at <paramref name="feet"/>.
        /// False while occupied, from the upper half or when there is no ground past the top.
        /// </summary>
        public bool TryGetPath(Vector3 feet, out Vector3[] path)
        {
            path = null;
            if (IsOccupied) return false;

            float topY = center.y + halfHeight;
            if (!LadderRules.CanStart(feet.y, center.y - halfHeight, topY)) return false;

            Vector3 outward = LadderRules.OutwardSide(center, transform.forward, feet);
            Vector3 probe = center - outward * exitDistance;
            probe.y = topY + LandingProbeHeight;
            // Only down to the middle of the ladder: lower ground is not its ledge.
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, LandingProbeHeight + halfHeight,
                    LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore))
                return false;

            Vector3 stand = center + outward * StandDistance;
            path = new[]
            {
                new Vector3(stand.x, feet.y, stand.z),
                new Vector3(stand.x, hit.point.y, stand.z),
                hit.point
            };
            return true;
        }
    }
}
