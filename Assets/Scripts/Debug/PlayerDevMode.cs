using System;
using CodenameVelvet.Inventory;
using CodenameVelvet.Player;
using UnityEngine;

namespace CodenameVelvet.DebugTools
{
    /// <summary>
    /// Efectos del dev mode sobre UN jugador. Lo agrega <see cref="DevMode"/> en runtime (no va en el prefab)
    /// y sigue su estado global: más velocidad, invulnerable, todos los recursos y "fantasma"
    /// (atraviesa enemigos y jugadores, pero sigue chocando con el mapa y recogiendo recursos).
    /// Guarda lo que cambia para dejarlo exactamente igual al desactivarse.
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
            // Solo excluye contactos: los enemigos lo siguen detectando (OverlapSphere) y persiguiendo.
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
