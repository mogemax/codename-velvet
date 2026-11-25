using System;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Inventory;
using CodenameVelvet.Player;
using UnityEngine;

namespace CodenameVelvet.Gathering
{
    /// <summary>
    /// A stack of one resource lying on the ground. Any living player who walks over it
    /// collects as much as fits in their inventory; the rest stays on the ground, and the
    /// pickup is destroyed once empty.
    /// Detection relies on a trigger Collider on this object; the players' Rigidbody fires the callbacks.
    /// Spawned pickups get their contents from <see cref="Initialize"/>; scene-placed ones use the serialized values.
    /// </summary>
    public class ResourcePickup : MonoBehaviour, IPickup
    {
        private const string LogCategory = "Inventory";

        [SerializeField] private ResourceType type;
        [SerializeField] private int amount = 5;
        [Tooltip("Seconds after spawning before anyone can collect it, so drops are visible for a moment.")]
        [SerializeField] private float pickupDelaySeconds = 0.4f;

        [Tooltip("Drawing, tint and size of each resource.")]
        [SerializeField] private ResourceVisuals visuals;

        [Header("Debug")]
        [Tooltip("Logs who collected how much and what remains.")]
        [SerializeField] private bool debugLogs = true;

        private float collectableAt;

        public ResourceType Type => type;
        public int Amount => amount;

        private void Awake()
        {
            collectableAt = Time.time + pickupDelaySeconds;
        }

        private void Start()
        {
            ApplyLook();
        }

        /// <summary>Sets what this pickup holds. Call right after Instantiate. Amount must be positive.</summary>
        public void Initialize(ResourceType resourceType, int resourceAmount)
        {
            if (resourceAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(resourceAmount), resourceAmount, "Pickup amount must be positive.");

            type = resourceType;
            amount = resourceAmount;
            ApplyLook();
        }

        private void OnTriggerStay(Collider other)
        {
            // Amount is 0 between collecting the last unit and the deferred Destroy.
            if (amount <= 0 || Time.time < collectableAt)
                return;

            PlayerInventory inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory == null)
                return;

            PlayerHealth health = inventory.GetComponent<PlayerHealth>();
            if (health != null && !health.IsAlive)
                return;

            int added = inventory.TryAdd(type, amount);
            if (added == 0)
                return;

            amount -= added;

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"{inventory.name} collected {added} {type}, {amount} left on the ground");

            if (amount == 0)
                Destroy(gameObject);
        }

        public bool CanBeTakenBy(PlayerHealth player) =>
            player.TryGetComponent(out PlayerInventory inventory) && inventory.SpaceFor(type) > 0;

        private void ApplyLook()
        {
            if (visuals != null && visuals.TryGet(type, out ResourceVisuals.Entry look))
                PickupVisual.Apply(gameObject, look.sprite, look.tint, look.worldHeight);
        }
    }
}
