using System;

namespace CodenameVelvet.DayNight
{
    /// <summary>
    /// How dangerous a night is: grows with every night survived and every extra player (CYCLE-02).
    /// <c>(1 + perNight·(night−1)) · (1 + perPlayer·(players−1))</c>, capped.
    /// </summary>
    public static class NightThreat
    {
        public static float Multiplier(int nightNumber, int playerCount, float perNight, float perExtraPlayer, float max)
        {
            float byNight = 1f + Math.Max(0f, perNight) * Math.Max(0, nightNumber - 1);
            float byPlayers = 1f + Math.Max(0f, perExtraPlayer) * Math.Max(0, playerCount - 1);
            return Math.Min(Math.Max(1f, max), byNight * byPlayers);
        }

        /// <summary>Enemies to keep alive for that threat (rounded half away from zero, never negative).</summary>
        public static int EnemyCount(int baseCount, float multiplier)
        {
            return (int)Math.Round(Math.Max(0, baseCount) * Math.Max(0f, multiplier), MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>Announced on every phase change.</summary>
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

        /// <summary>1 by day; at night, the result of <see cref="NightThreat.Multiplier"/>.</summary>
        public float ThreatMultiplier { get; }

        public bool IsNight => Phase == DayPhase.Night;
    }
}
