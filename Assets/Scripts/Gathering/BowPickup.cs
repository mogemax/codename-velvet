using CodenameVelvet.Player;

namespace CodenameVelvet.Gathering
{
    /// <summary>A bow on the ground: a player without one who touches it takes it.</summary>
    public class BowPickup : WeaponPickup
    {
        protected override bool TryGive(PlayerHealth player)
        {
            if (!player.TryGetComponent(out PlayerBow bow) || bow.HasBow)
                return false;

            bow.Equip();
            return true;
        }
    }
}
