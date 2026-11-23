using System;

namespace CodenameVelvet.DayNight
{
    public enum DayPhase
    {
        Day,
        Night,
    }

    /// <summary>
    /// Pure day/night clock: alternates Day → Night → Day… with fixed durations and counts the nights.
    /// The caller feeds it the elapsed time.
    /// </summary>
    public sealed class DayNightClock
    {
        private readonly float daySeconds;
        private readonly float nightSeconds;

        public DayNightClock(float daySeconds, float nightSeconds)
        {
            if (daySeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(daySeconds));
            if (nightSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(nightSeconds));

            this.daySeconds = daySeconds;
            this.nightSeconds = nightSeconds;
            Remaining = daySeconds;
        }

        public DayPhase Phase { get; private set; } = DayPhase.Day;

        /// <summary>0 during the first day; becomes 1 when the first night falls.</summary>
        public int NightNumber { get; private set; }

        /// <summary>Seconds left until the phase changes.</summary>
        public float Remaining { get; private set; }

        public float PhaseDuration => Phase == DayPhase.Day ? daySeconds : nightSeconds;

        /// <summary>0 when the phase starts, 1 when it ends.</summary>
        public float PhaseProgress => 1f - Remaining / PhaseDuration;

        /// <summary>Advances the clock. Returns true when the phase changed; leftover time carries into the next phase.</summary>
        public bool Advance(float seconds)
        {
            if (seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));

            bool changed = false;
            Remaining -= seconds;
            while (Remaining <= 0f)
            {
                float overflow = -Remaining;
                EnterNextPhase();
                Remaining -= overflow;
                changed = true;
            }
            return changed;
        }

        /// <summary>Ends the current phase immediately.</summary>
        public void SkipPhase() => EnterNextPhase();

        private void EnterNextPhase()
        {
            if (Phase == DayPhase.Day)
            {
                Phase = DayPhase.Night;
                NightNumber++;
            }
            else
            {
                Phase = DayPhase.Day;
            }
            Remaining = PhaseDuration;
        }
    }
}
