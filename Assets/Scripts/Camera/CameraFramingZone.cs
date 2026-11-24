using System.Collections.Generic;
using UnityEngine;

namespace CodenameVelvet.Cameras
{
    /// <summary>
    /// Area (this object's BoxCollider, on the XZ plane) where the camera of each player inside pulls back and
    /// shifts its focus toward <see cref="focus"/>, e.g. a boss arena. Every split-screen camera reacts on its own.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CameraFramingZone : MonoBehaviour
    {
        private static readonly List<CameraFramingZone> Active = new List<CameraFramingZone>();

        [Tooltip("Extra camera distance while inside.")]
        [SerializeField, Min(0f)] private float extraDistance = 12f;
        [Tooltip("Optional: while it exists, the camera's focus shifts toward it (e.g. the boss).")]
        [SerializeField] private Transform focus;
        [Tooltip("Fraction of the way from the player to the focus (0.5 = midpoint).")]
        [SerializeField, Range(0f, 1f)] private float focusWeight = 0.4f;
        [Tooltip("Max focus shift, so the player never leaves the view.")]
        [SerializeField, Min(0f)] private float maxFocusShift = 8f;

        private BoxCollider area;

        public float ExtraDistance => extraDistance;

        /// <summary>The active zone containing <paramref name="position"/> on the XZ plane, or null.</summary>
        public static CameraFramingZone Find(Vector3 position)
        {
            foreach (CameraFramingZone zone in Active)
                if (zone.Contains(position)) return zone;
            return null;
        }

        /// <summary>Offset to add to the camera focus of a player standing at <paramref name="playerPosition"/>.</summary>
        public Vector3 FocusShift(Vector3 playerPosition)
        {
            if (focus == null || !focus.gameObject.activeInHierarchy) return Vector3.zero;

            Vector3 shift = (focus.position - playerPosition) * focusWeight;
            shift.y = 0f;
            return Vector3.ClampMagnitude(shift, maxFocusShift);
        }

        private void Awake() => area = GetComponent<BoxCollider>();
        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private bool Contains(Vector3 position)
        {
            Bounds bounds = area.bounds;
            return position.x >= bounds.min.x && position.x <= bounds.max.x
                && position.z >= bounds.min.z && position.z <= bounds.max.z;
        }
    }
}
