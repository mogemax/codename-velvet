using CodenameVelvet.Map;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Player
{
    /// <summary>Uses the <see cref="Boat"/> in reach (deliver materials, set sail) when Interact is pressed.</summary>
    [RequireComponent(typeof(PlayerInventory))]
    public class PlayerBoatBuilder : MonoBehaviour
    {
        private PlayerInventory inventory;
        private PlayerHealth health;
        private Boat boat;

        /// <summary>The boat in reach; null when there is none or the player is down.</summary>
        public Boat NearbyBoat => health == null || health.IsAlive ? boat : null;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            health = GetComponent<PlayerHealth>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out Boat entered)) boat = entered;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out Boat exited) && exited == boat) boat = null;
        }

        private void OnInteract(InputValue value)
        {
            if (value.isPressed && NearbyBoat != null) boat.Use(inventory);
        }
    }
}
