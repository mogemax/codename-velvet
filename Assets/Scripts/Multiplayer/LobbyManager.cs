using System.Linq;
using CodenameVelvet.Characters;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Multiplayer
{
    /// <summary>
    /// Runs the CharacterMenu join lobby. PlayerInputManager spawns a
    /// <see cref="LobbyPlayer"/> per device that presses Join; this class records
    /// it in the session roster, applies the <see cref="CharacterLobby"/> rules to
    /// what each player presses, and redraws the slot frames. Start writes every
    /// pick into the roster, locks it and loads the level; Back clears it.
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        private const string LogCategory = "Lobby";

        [Header("Data")]
        [SerializeField] private SessionRosterAsset rosterAsset;
        [SerializeField] private CharacterCatalog catalog;

        [Header("Input")]
        [SerializeField] private PlayerInputManager playerInputManager;
        [Tooltip("Returns to the main menu, but only while nobody has joined (B / Esc).")]
        [SerializeField] private InputActionReference backAction;

        [Header("Scene")]
        [Tooltip("One per roster slot, in slot order.")]
        [SerializeField] private LobbySlotView[] slotViews = new LobbySlotView[0];
        [SerializeField] private CambioEscenaAnimado startTransition;
        [SerializeField] private CambioEscenaAnimado backTransition;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private SessionRoster roster;
        private CharacterLobby lobby;
        private LobbyPlayer[] players;
        private bool isLeaving;

        private void Awake()
        {
            roster = rosterAsset.Roster;
            roster.Clear();
            lobby = new CharacterLobby(roster.MaxPlayers, catalog.Count);
            players = new LobbyPlayer[roster.MaxPlayers];

            if (slotViews.Length != roster.MaxPlayers)
                Debug.LogError($"{name}: needs {roster.MaxPlayers} slot views, has {slotViews.Length}.", this);
        }

        private void OnEnable()
        {
            playerInputManager.onPlayerJoined += HandlePlayerJoined;
            playerInputManager.onPlayerLeft += HandlePlayerLeft;
            backAction.action.performed += HandleBackAction;
            backAction.action.Enable();
        }

        private void OnDisable()
        {
            playerInputManager.onPlayerJoined -= HandlePlayerJoined;
            playerInputManager.onPlayerLeft -= HandlePlayerLeft;
            backAction.action.performed -= HandleBackAction;
            backAction.action.Disable();
        }

        private void Start() => Refresh();

        // ─────────────────────── Player commands (from LobbyPlayer) ───────────────────────

        public void Cycle(int slot, int direction)
        {
            if (!isLeaving && lobby.Cycle(slot, direction))
                Refresh();
        }

        public void Confirm(int slot)
        {
            if (!isLeaving && lobby.Confirm(slot))
                Refresh();
        }

        /// <summary>B / Esc: unlocks a locked pick, otherwise leaves the lobby.</summary>
        public void Cancel(int slot)
        {
            if (isLeaving)
                return;

            if (lobby.Unlock(slot))
            {
                Refresh();
                return;
            }

            LobbyPlayer player = players[slot];
            RemovePlayer(slot);
            if (player != null)
                Destroy(player.gameObject);
        }

        public void StartGame(int slot)
        {
            if (isLeaving || slot != lobby.HostSlot || !lobby.CanStart)
                return;

            foreach (RosterEntry entry in roster.Entries)
                roster.AssignCharacter(entry.SlotIndex, lobby.GetCharacter(entry.SlotIndex));
            roster.Lock();

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"Starting with {roster.Count} player(s)");

            LeaveScene(startTransition);
        }

        /// <summary>Hooked to the BackButton's OnClick; the mouse can always go back.</summary>
        public void GoBack()
        {
            if (isLeaving)
                return;

            roster.Clear();
            LeaveScene(backTransition);
        }

        // ─────────────────────── Join / leave ───────────────────────

        private void HandlePlayerJoined(PlayerInput input)
        {
            int[] deviceIds = input.devices.Select(device => device.deviceId).ToArray();

            if (isLeaving || !roster.TryAdd(deviceIds, input.currentControlScheme, out int slot))
            {
                Destroy(input.gameObject);
                return;
            }

            lobby.Join(slot);
            players[slot] = input.GetComponent<LobbyPlayer>();
            players[slot].Bind(this, slot);

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"P{slot + 1} joined with {input.currentControlScheme}");

            Refresh();
        }

        // Also fires when a join token is destroyed by Cancel (already removed by then)
        // or by the scene unloading (ignored while leaving).
        private void HandlePlayerLeft(PlayerInput input)
        {
            if (isLeaving)
                return;

            int slot = System.Array.FindIndex(players, player => player != null && player.gameObject == input.gameObject);
            if (slot >= 0)
                RemovePlayer(slot);
        }

        private void RemovePlayer(int slot)
        {
            lobby.Leave(slot);
            roster.Remove(slot);
            players[slot] = null;

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, $"P{slot + 1} left");

            Refresh();
        }

        private void HandleBackAction(InputAction.CallbackContext context)
        {
            // A joined player's B / Esc is their own Cancel, never Back.
            if (lobby.JoinedCount > 0 || PlayerInput.FindFirstPairedToDevice(context.control.device) != null)
                return;

            GoBack();
        }

        private void LeaveScene(CambioEscenaAnimado transition)
        {
            isLeaving = true;
            playerInputManager.DisableJoining();
            transition.IrASiguienteMenu();
        }

        // ─────────────────────── View ───────────────────────

        private void Refresh()
        {
            for (int slot = 0; slot < slotViews.Length && slot < lobby.SlotCount; slot++)
            {
                int character = lobby.GetCharacter(slot);
                slotViews[slot].Show(
                    slot,
                    lobby.GetState(slot),
                    character == CharacterLobby.NoCharacter ? null : catalog.Get(character),
                    slot == lobby.HostSlot,
                    lobby.CanStart);
            }
        }
    }
}
