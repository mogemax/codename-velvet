using CodenameVelvet.Player;

namespace CodenameVelvet.Gathering
{
    /// <summary>Something on the ground a player can take (a resource, an apple). <see cref="PickupMotion"/> asks it who may.</summary>
    public interface IPickup
    {
        /// <summary>Whether this living player can take it now (room in the inventory, health to restore...).</summary>
        bool CanBeTakenBy(PlayerHealth player);
    }
}
