using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Multiplayer
{
    /// <summary>
    /// Join token spawned by the lobby's PlayerInputManager: one per paired device
    /// set, no character, no physics. It only forwards its own "Lobby" map input
    /// (PlayerInput · Send Messages) to the lobby, tagged with its roster slot.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class LobbyPlayer : MonoBehaviour
    {
        private LobbyManager lobby;
        private int slot;

        public void Bind(LobbyManager lobbyManager, int slotIndex)
        {
            lobby = lobbyManager;
            slot = slotIndex;
        }

        private void OnPrevious()
        {
            if (lobby != null) lobby.Cycle(slot, -1);
        }

        private void OnNext()
        {
            if (lobby != null) lobby.Cycle(slot, 1);
        }

        private void OnConfirm()
        {
            if (lobby != null) lobby.Confirm(slot);
        }

        private void OnCancel()
        {
            if (lobby != null) lobby.Cancel(slot);
        }

        private void OnStartGame()
        {
            if (lobby != null) lobby.StartGame(slot);
        }
    }
}
