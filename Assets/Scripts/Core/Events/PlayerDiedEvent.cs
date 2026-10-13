using UnityEngine;

namespace CodenameVelvet.Core.Events
{
    /// <summary>
    /// Raised when a player dies. Payload is the dying player's slot index,
    /// never a direct reference to the player.
    /// </summary>
    [CreateAssetMenu(menuName = "Events/Player Died Event", fileName = "New PlayerDiedEvent")]
    public class PlayerDiedEvent : GameEvent<int>
    {
    }
}
