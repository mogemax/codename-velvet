using CodenameVelvet.DayNight;
using UnityEngine;

namespace CodenameVelvet.Core.Events
{
    /// <summary>
    /// Se lanza al iniciar la partida y en cada cambio Día ↔ Noche. Las SpawnZones (y lo que venga después)
    /// escuchan este asset sin conocer al <c>DayNightCycle</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Events/Day Night Event", fileName = "DayNightEvent")]
    public class DayNightEvent : GameEvent<DayNightState>
    {
    }
}
