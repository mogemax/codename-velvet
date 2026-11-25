using CodenameVelvet.Player;

namespace CodenameVelvet.Gathering
{
    /// <summary>A sword on the ground: an unarmed player who touches it takes it in hand.</summary>
    public class SwordPickup : WeaponPickup
    {
        protected override bool TryGive(PlayerHealth player)
        {
            if (!player.TryGetComponent(out PlayerSword sword) || sword.HasSword)
                return false;

            sword.Equip();
            return true;
        }
    }
}
