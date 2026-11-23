using CodenameVelvet.DayNight;
using UnityEngine;

namespace CodenameVelvet.Core.Events
{
    /// <summary>Raised when the match starts and on every Day ↔ Night change.</summary>
    [CreateAssetMenu(menuName = "Events/Day Night Event", fileName = "DayNightEvent")]
    public class DayNightEvent : GameEvent<DayNightState>
    {
    }
}
