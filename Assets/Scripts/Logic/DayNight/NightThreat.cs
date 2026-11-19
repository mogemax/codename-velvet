using System;

namespace CodenameVelvet.DayNight
{
    /// <summary>
    /// Qué tan peligrosa es una noche: crece con cada noche sobrevivida y con cada jugador extra (CYCLE-02).
    /// <c>(1 + porNoche·(noche−1)) · (1 + porJugador·(jugadores−1))</c>, con tope.
    /// </summary>
    public static class NightThreat
    {
        public static float Multiplier(int nightNumber, int playerCount, float perNight, float perExtraPlayer, float max)
        {
            float byNight = 1f + Math.Max(0f, perNight) * Math.Max(0, nightNumber - 1);
            float byPlayers = 1f + Math.Max(0f, perExtraPlayer) * Math.Max(0, playerCount - 1);
            return Math.Min(Math.Max(1f, max), byNight * byPlayers);
        }

        /// <summary>Enemigos a mantener con esa amenaza (redondeo normal, nunca negativo).</summary>
        public static int EnemyCount(int baseCount, float multiplier)
        {
            return (int)Math.Round(Math.Max(0, baseCount) * Math.Max(0f, multiplier), MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>Lo que se anuncia en cada cambio de fase.</summary>
    public readonly struct DayNightState
    {
        public DayNightState(DayPhase phase, int nightNumber, float threatMultiplier)
        {
            Phase = phase;
            NightNumber = nightNumber;
            ThreatMultiplier = threatMultiplier;
        }

        public DayPhase Phase { get; }
        public int NightNumber { get; }

        /// <summary>1 de día; de noche, el resultado de <see cref="NightThreat.Multiplier"/>.</summary>
        public float ThreatMultiplier { get; }

        public bool IsNight => Phase == DayPhase.Night;
    }
}
