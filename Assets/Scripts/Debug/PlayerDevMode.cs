using System;
using CodenameVelvet.Inventory;
using CodenameVelvet.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.DebugTools
{
    /// <summary>
    /// Dev mode effects on ONE player, added at runtime by <see cref="DevMode"/> and following its global state:
    /// extra speed, invulnerability, full inventory, "ghost" (passes through enemies and players but still
    /// collides with the map and collects pickups) and free flight (passes through everything; up/down with
    /// Space/Ctrl or the player's own gamepad shoulders). Restores exactly what it changed when turned off.
    /// </summary>
    public class PlayerDevMode : MonoBehaviour
    {
        private const float SpeedMultiplier = 2.5f;
        private const float FlightVerticalSpeed = 12f;
        private const float LandingProbeHeight = 200f;

        private PlayerMovement movement;
        private PlayerHealth health;
        private PlayerInventory inventory;
        private PlayerInput input;
        private Collider body;
        private LayerMask originalExcludeLayers;
        private int ghostLayers;
        private int flightLayers;
        private int groundMask;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<PlayerHealth>();
            inventory = GetComponent<PlayerInventory>();
            input = GetComponent<PlayerInput>();
            body = GetComponent<Collider>();
            if (body != null) originalExcludeLayers = body.excludeLayers;
            ghostLayers = LayerMask.GetMask("ENEMY", "Player");
            groundMask = LayerMask.GetMask("Ground");
            flightLayers = ghostLayers | groundMask | LayerMask.GetMask("Default", "Water");
        }

        private void OnEnable()
        {
            DevMode.Changed += Apply;
            DevMode.FlightChanged += ApplyFlight;
            Apply(DevMode.IsActive);
        }

        private void OnDisable()
        {
            DevMode.Changed -= Apply;
            DevMode.FlightChanged -= ApplyFlight;
            if (movement != null) movement.SpeedMultiplier = 1f;
            if (health != null) health.Invulnerable = false;
            SetCollisions(false, false);
        }

        private void Update()
        {
            if (movement != null && movement.IsFlying)
                movement.FlightVerticalSpeed = ReadVerticalInput() * FlightVerticalSpeed * movement.SpeedMultiplier;
        }

        private void Apply(bool active)
        {
            if (movement != null) movement.SpeedMultiplier = active ? SpeedMultiplier : 1f;
            if (health != null) health.Invulnerable = active;
            if (active) FillInventory();
            SetCollisions(active, active && DevMode.IsFlying);
        }

        private void ApplyFlight(bool flying) => SetCollisions(DevMode.IsActive, DevMode.IsActive && flying);

        private void SetCollisions(bool ghost, bool flying)
        {
            // Only contacts are excluded: enemies still detect (OverlapSphere) and chase the player.
            int excluded = flying ? flightLayers : ghost ? ghostLayers : 0;
            if (body != null) body.excludeLayers = originalExcludeLayers | excluded;

            if (movement == null) return;
            bool wasFlying = movement.IsFlying;
            movement.IsFlying = flying;
            movement.FlightVerticalSpeed = 0f;
            if (wasFlying && !flying) LandOnGround();
        }

        /// <summary>After flying through the terrain, moves the player back on top of it before gravity returns.</summary>
        private void LandOnGround()
        {
            Vector3 position = transform.position;
            if (!Physics.Raycast(position + Vector3.up * LandingProbeHeight, Vector3.down, out RaycastHit hit,
                    LandingProbeHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
                return;
            if (hit.point.y <= position.y) return; // already above the ground: just fall

            transform.position = hit.point;
            if (TryGetComponent(out Rigidbody rb))
            {
                rb.position = hit.point;
                rb.linearVelocity = Vector3.zero;
            }
        }

        private float ReadVerticalInput()
        {
            if (input == null) return 0f;
            float vertical = 0f;
            foreach (InputDevice device in input.devices)
            {
                if (device is Keyboard keyboard)
                    vertical += (keyboard.spaceKey.isPressed ? 1f : 0f) - (keyboard.leftCtrlKey.isPressed ? 1f : 0f);
                else if (device is Gamepad gamepad)
                    vertical += (gamepad.rightShoulder.isPressed ? 1f : 0f) - (gamepad.leftShoulder.isPressed ? 1f : 0f);
            }
            return Mathf.Clamp(vertical, -1f, 1f);
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
