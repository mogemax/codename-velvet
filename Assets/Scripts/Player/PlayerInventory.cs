using System;
using System.Collections.Generic;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Gathering;
using CodenameVelvet.Inventory;
using UnityEngine;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// Per-player resource inventory (4 slots x 20). Wraps <see cref="ResourceInventory"/>
    /// and, when this player's <see cref="PlayerHealth"/> dies, drops every stack as a
    /// <see cref="ResourcePickup"/> scattered around the death spot.
    /// Rule: every reference here is to this GameObject's own components.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        private const string LogCategory = "Inventory";
        private const int SlotCount = 4;
        private const int StackCap = 20;

        [Tooltip("Spawned once per stack when this player dies.")]
        [SerializeField] private ResourcePickup pickupPrefab;
        [Tooltip("Max XZ distance from the death spot for dropped stacks, in units.")]
        [SerializeField] private float dropScatterRadius = 1f;

        [Header("Debug")]
        [Tooltip("Logs added resources, rejections for lack of space and death drops.")]
        [SerializeField] private bool debugLogs = true;

        private readonly ResourceInventory inventory = new ResourceInventory(SlotCount, StackCap);
        private PlayerHealth health;
        private bool rejectionLogged; // Pickups retry every physics step; log a full inventory once per change.

        /// <summary>Raised after any change to the slots.</summary>
        public event Action Changed;

        /// <summary>Something was picked up: what and how much.</summary>
        public event Action<ResourceType, int> Added;

        public IReadOnlyList<ResourceInventory.Slot> Slots => inventory.Slots;
        public int MaxPerSlot => inventory.StackCap;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
        }

        private void OnEnable()
        {
            if (health != null)
                health.Died += DropEverything;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Died -= DropEverything;
        }

        /// <summary>Adds as much as fits and returns how much was added. Amount must be positive.</summary>
        public int TryAdd(ResourceType type, int amount)
        {
            int added = inventory.TryAdd(type, amount);

            if (added > 0)
            {
                if (debugLogs)
                    VelvetLog.Info(LogCategory, this, $"Added {added} {type}, now {inventory.CountOf(type)}");
                NotifyChanged();
                Added?.Invoke(type, added);
            }

            if (debugLogs && added < amount && !rejectionLogged)
            {
                VelvetLog.Info(LogCategory, this, $"No space for {amount - added} {type}");
                rejectionLogged = true;
            }

            return added;
        }

        /// <summary>Puts back stacks brought from another level, without announcing them as pickups.</summary>
        public void Restore(IEnumerable<ResourceInventory.Slot> stacks)
        {
            foreach (ResourceInventory.Slot stack in stacks)
                inventory.TryAdd(stack.Type, stack.Count);
            NotifyChanged();
        }

        /// <summary>Removes exactly <paramref name="amount"/> or nothing. Amount must be positive.</summary>
        public bool TryRemove(ResourceType type, int amount)
        {
            if (!inventory.TryRemove(type, amount))
                return false;

            NotifyChanged();
            return true;
        }

        /// <summary>Hands over everything <paramref name="build"/> still needs. False when there was nothing to give.</summary>
        public bool DeliverTo(BuildProgress build)
        {
            if (!build.TakeFrom(inventory))
                return false;

            NotifyChanged();
            return true;
        }

        public int CountOf(ResourceType type) => inventory.CountOf(type);

        /// <summary>How much more of <paramref name="type"/> fits.</summary>
        public int SpaceFor(ResourceType type) => inventory.SpaceFor(type);

        private void DropEverything()
        {
            List<ResourceInventory.Slot> stacks = inventory.Drain();
            if (stacks.Count == 0)
                return;

            if (pickupPrefab == null)
            {
                Debug.LogError($"{name}: PlayerInventory has no pickup prefab, {stacks.Count} stacks lost on death.", this);
            }
            else
            {
                foreach (ResourceInventory.Slot stack in stacks)
                {
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * dropScatterRadius;
                    Vector3 position = transform.position + new Vector3(offset.x, 0f, offset.y);
                    Instantiate(pickupPrefab, position, Quaternion.identity).Initialize(stack.Type, stack.Count);

                    if (debugLogs)
                        VelvetLog.Info(LogCategory, this, $"Dropped {stack.Count} {stack.Type} on death");
                }
            }

            NotifyChanged();
        }

        private void NotifyChanged()
        {
            rejectionLogged = false;
            Changed?.Invoke();
        }
    }
}
