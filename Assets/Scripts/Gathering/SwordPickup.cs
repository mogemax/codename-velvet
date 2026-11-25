using CodenameVelvet.Player;
using UnityEngine;

namespace CodenameVelvet.Gathering
{
    /// <summary>
    /// A sword lying on the ground: a living, unarmed player who touches it takes it in hand. Someone who already
    /// has one leaves it for the others.
    /// </summary>
    public class SwordPickup : MonoBehaviour
    {
        private bool isTaken; // Destroy waits for the end of the frame: a second player may touch it before

        private void OnTriggerStay(Collider other)
        {
            if (isTaken)
                return;

            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player == null || !player.IsAlive || !player.TryGetComponent(out PlayerSword sword) || sword.HasSword)
                return;

            isTaken = true;
            sword.Equip();
            Destroy(gameObject);
        }
    }
}
