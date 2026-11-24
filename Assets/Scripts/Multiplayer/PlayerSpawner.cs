using System.Collections.Generic;
using System.Linq;
using CodenameVelvet.Characters;
using CodenameVelvet.Core;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Multiplayer
{
    /// <summary>
    /// Creates the level's players from the roster the lobby left: each with their own devices, slot
    /// (PlayerIdentity), spawn point and camera covering their part of the screen (<see cref="SplitScreenLayout"/>).
    /// When the scene is opened directly (empty roster) it creates one player on the first available device.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        private const string LogCategory = "Spawner";
        private const float GroundProbeUp = 10f;
        private const float GroundProbeDown = 100f;
        private const float SpawnLift = 0.1f;

        [Header("Data")]
        [SerializeField] private SessionRosterAsset rosterAsset;
        [SerializeField] private CharacterCatalog catalog;

        [Header("Prefabs")]
        [SerializeField] private PlayerInput playerPrefab;
        [SerializeField] private CameraFollow cameraPrefab;

        [Header("Scene")]
        [Tooltip("One per slot, in slot order.")]
        [SerializeField] private Transform[] spawnPoints = new Transform[0];
        [SerializeField] private MatchFlow matchFlow;

        private readonly List<PlayerInput> spawned = new List<PlayerInput>();

        public IReadOnlyList<PlayerInput> Players => spawned;

        /// <summary>Where the first player appears.</summary>
        public Transform FirstSpawnPoint => spawnPoints.Length > 0 && spawnPoints[0] != null ? spawnPoints[0] : transform;

        private void Start()
        {
            List<RosterEntry> entries = rosterAsset.Roster.Entries.ToList();
            Rect[] viewports = SplitScreenLayout.ComputeRects(Mathf.Max(1, entries.Count));

            if (entries.Count == 0)
            {
                VelvetLog.Info(LogCategory, this, "Empty roster: test player on the first device");
                Spawn(slot: 0, characterIndex: 0, devices: new InputDevice[0], scheme: null, viewports[0]);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RosterEntry entry = entries[i];
                InputDevice[] devices = entry.DeviceIds
                    .Select(InputSystem.GetDeviceById)
                    .Where(device => device != null) // unplugged between the lobby and the level
                    .ToArray();
                Spawn(entry.SlotIndex, entry.CharacterIndex, devices, entry.ControlScheme, viewports[i]);
            }
        }

        private void Spawn(int slot, int characterIndex, InputDevice[] devices, string scheme, Rect viewport)
        {
            Transform point = spawnPoints.Length > 0 ? spawnPoints[slot % spawnPoints.Length] : transform;

            PlayerInput player = devices.Length > 0
                ? PlayerInput.Instantiate(playerPrefab.gameObject, slot, scheme, -1, devices)
                : PlayerInput.Instantiate(playerPrefab.gameObject, slot);
            // The player's Rigidbody interpolates, so it ignores Transform-only moves: place both.
            Vector3 position = OnGround(point.position);
            player.transform.SetPositionAndRotation(position, playerPrefab.transform.rotation);
            if (player.TryGetComponent(out Rigidbody body))
            {
                body.position = position;
                body.linearVelocity = Vector3.zero;
            }
            player.name = $"Player{slot + 1}";

            CharacterDefinition character = characterIndex >= 0 && characterIndex < catalog.Count
                ? catalog.Get(characterIndex)
                : null;
            if (player.TryGetComponent(out PlayerIdentity identity))
                identity.Initialize(slot, character != null ? character.Color : Color.white);

            if (matchFlow != null && player.TryGetComponent(out PlayerHealth health))
                matchFlow.RegisterPlayer(health);

            player.camera = CreateCamera(player.transform, viewport, isFirst: spawned.Count == 0);
            spawned.Add(player);

            VelvetLog.Info(LogCategory, this,
                $"P{slot + 1} ({character?.DisplayName ?? "no character"}) with {string.Join(", ", player.devices.Select(d => d.displayName))}");
        }

        /// <summary>
        /// The ground right below (or slightly above) a spawn point, so a badly placed point never leaves a player
        /// inside the terrain or floating. PlayerHealth respawns there too, since it records its start position.
        /// </summary>
        private static Vector3 OnGround(Vector3 point)
        {
            return Physics.Raycast(point + Vector3.up * GroundProbeUp, Vector3.down, out RaycastHit hit,
                GroundProbeUp + GroundProbeDown, LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * SpawnLift
                : point;
        }

        private Camera CreateCamera(Transform target, Rect viewport, bool isFirst)
        {
            CameraFollow follow = Instantiate(cameraPrefab);
            follow.name = $"{target.name}Camera";
            follow.SetTarget(target);

            Camera cam = follow.GetComponent<Camera>();
            cam.rect = viewport;

            // Unity supports a single active AudioListener.
            if (!isFirst && follow.TryGetComponent(out AudioListener listener))
                Destroy(listener);

            return cam;
        }
    }
}
