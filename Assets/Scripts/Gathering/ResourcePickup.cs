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
    /// Prefab setup: a Collider with "Is Trigger" on this GameObject (players carry the
    /// Rigidbody that makes trigger callbacks fire) and child SpriteRenderers for the visual.
    /// Spawn with <c>Instantiate(prefab, position, Quaternion.identity).Initialize(type, amount)</c>;
    /// pickups placed directly in a scene use the serialized type and amount.
    /// </summary>
    public class ResourcePickup : MonoBehaviour
    {
        private const string LogCategory = "Inventory";

        [SerializeField] private ResourceType type;
        [SerializeField] private int amount = 5;
        [Tooltip("Seconds after spawning before anyone can collect it, so drops are visible for a moment.")]
        [SerializeField] private float pickupDelaySeconds = 0.4f;

        [Header("Placeholder tint")]
        [SerializeField] private Color woodColor = new Color(0.55f, 0.35f, 0.17f);
        [SerializeField] private Color stoneColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color mineralColor = Color.cyan;

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
            ApplyTint();
        }

        /// <summary>Sets what this pickup holds. Call right after Instantiate. Amount must be positive.</summary>
        public void Initialize(ResourceType resourceType, int resourceAmount)
        {
            if (resourceAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(resourceAmount), resourceAmount, "Pickup amount must be positive.");

            type = resourceType;
            amount = resourceAmount;
            ApplyTint();
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

        private void ApplyTint()
        {
            Color tint = ColorFor(type);
            foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>())
                sprite.color = tint;
        }

        private Color ColorFor(ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.Wood: return woodColor;
                case ResourceType.Stone: return stoneColor;
                default: return mineralColor;
            }
        }
    }
}
