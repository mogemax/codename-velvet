using CodenameVelvet.Player;
using UnityEngine;

namespace CodenameVelvet.Gathering
{
    /// <summary>
    /// A weapon lying on the ground: a living player who touches it takes it, unless they already carry one of
    /// its kind (then it stays for the others). Each kind says how a player takes it.
    /// </summary>
    public abstract class WeaponPickup : MonoBehaviour
    {
        private bool isTaken; // Destroy waits for the end of the frame: a second player may touch it before

        /// <summary>Hands the weapon to <paramref name="player"/>; false if they cannot take it.</summary>
        protected abstract bool TryGive(PlayerHealth player);

        private void OnTriggerStay(Collider other)
        {
            if (isTaken)
                return;

            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player == null || !player.IsAlive || !TryGive(player))
                return;

            isTaken = true;
            Destroy(gameObject);
        }
    }
}
