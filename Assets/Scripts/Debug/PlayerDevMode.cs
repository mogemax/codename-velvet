using System;
using CodenameVelvet.Inventory;
using CodenameVelvet.Player;
using UnityEngine;

namespace CodenameVelvet.DebugTools
{
    /// <summary>
    /// Dev mode effects on ONE player, added at runtime by <see cref="DevMode"/> and following its global state:
    /// extra speed, invulnerability, full inventory and "ghost" (passes through enemies and players but still
    /// collides with the map and collects pickups). Restores exactly what it changed when turned off.
    /// </summary>
    public class PlayerDevMode : MonoBehaviour
    {
        private const float SpeedMultiplier = 2.5f;

        private PlayerMovement movement;
        private PlayerHealth health;
        private PlayerInventory inventory;
        private Collider body;
        private LayerMask originalExcludeLayers;
        private int ghostLayers;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<PlayerHealth>();
            inventory = GetComponent<PlayerInventory>();
            body = GetComponent<Collider>();
            if (body != null) originalExcludeLayers = body.excludeLayers;
            ghostLayers = LayerMask.GetMask("ENEMY", "Player");
        }

        private void OnEnable()
        {
            DevMode.Changed += Apply;
            Apply(DevMode.IsActive);
        }

        private void OnDisable()
        {
            DevMode.Changed -= Apply;
            Apply(false);
        }

        private void Apply(bool active)
        {
            if (movement != null) movement.SpeedMultiplier = active ? SpeedMultiplier : 1f;
            if (health != null) health.Invulnerable = active;
            // Only contacts are excluded: enemies still detect (OverlapSphere) and chase the player.
            if (body != null) body.excludeLayers = active ? originalExcludeLayers | ghostLayers : originalExcludeLayers;
            if (active) FillInventory();
        }

        private void FillInventory()
        {
            if (inventory == null) return;
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                int missing = inventory.MaxPerSlot - inventory.CountOf(type);
                if (missing > 0) inventory.TryAdd(type, missing);
            }
        }
    }
}
