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
    /// Crea a los jugadores del nivel a partir del roster que dejó el lobby: cada uno con sus propios
    /// dispositivos, su slot (PlayerIdentity), su spawn point y su cámara con su parte de la pantalla
    /// (<see cref="SplitScreenLayout"/>). Si la escena se abre directo (roster vacío) crea un jugador
    /// con el primer dispositivo disponible para poder probar sin pasar por el lobby.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        private const string LogCategory = "Spawner";

        [Header("Data")]
        [SerializeField] private SessionRosterAsset rosterAsset;
        [SerializeField] private CharacterCatalog catalog;

        [Header("Prefabs")]
        [SerializeField] private PlayerInput playerPrefab;
        [SerializeField] private CameraFollow cameraPrefab;

        [Header("Scene")]
        [Tooltip("Uno por slot, en orden de slot.")]
        [SerializeField] private Transform[] spawnPoints = new Transform[0];
        [SerializeField] private MatchFlow matchFlow;

        private readonly List<PlayerInput> spawned = new List<PlayerInput>();

        public IReadOnlyList<PlayerInput> Players => spawned;

        private void Start()
        {
            List<RosterEntry> entries = rosterAsset.Roster.Entries.ToList();
            Rect[] viewports = SplitScreenLayout.ComputeRects(Mathf.Max(1, entries.Count));

            if (entries.Count == 0)
            {
                VelvetLog.Info(LogCategory, this, "Roster vacío: jugador de prueba con el primer dispositivo");
                Spawn(slot: 0, characterIndex: 0, devices: new InputDevice[0], scheme: null, viewports[0]);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RosterEntry entry = entries[i];
                InputDevice[] devices = entry.DeviceIds
                    .Select(InputSystem.GetDeviceById)
                    .Where(device => device != null) // se desconectó entre el lobby y el nivel
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
            player.transform.SetPositionAndRotation(point.position, playerPrefab.transform.rotation);
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
                $"P{slot + 1} ({character?.DisplayName ?? "sin personaje"}) con {string.Join(", ", player.devices.Select(d => d.displayName))}");
        }

        private Camera CreateCamera(Transform target, Rect viewport, bool isFirst)
        {
            CameraFollow follow = Instantiate(cameraPrefab);
            follow.name = $"{target.name}Camera";
            follow.SetTarget(target);

            Camera cam = follow.GetComponent<Camera>();
            cam.rect = viewport;

            // Unity solo admite un AudioListener activo.
            if (!isFirst && follow.TryGetComponent(out AudioListener listener))
                Destroy(listener);

            return cam;
        }
    }
}
