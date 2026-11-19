using System;

namespace CodenameVelvet.DayNight
{
    public enum DayPhase
    {
        Day,
        Night,
    }

    /// <summary>
    /// Reloj puro del ciclo: alterna Día → Noche → Día… con duraciones fijas y cuenta las noches.
    /// No sabe nada de Unity; quien lo usa le pasa el tiempo transcurrido.
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

        /// <summary>0 durante el primer día; sube a 1 al caer la primera noche.</summary>
        public int NightNumber { get; private set; }

        /// <summary>Segundos que faltan para el cambio de fase.</summary>
        public float Remaining { get; private set; }

        public float PhaseDuration => Phase == DayPhase.Day ? daySeconds : nightSeconds;

        /// <summary>0 al empezar la fase, 1 al terminarla.</summary>
        public float PhaseProgress => 1f - Remaining / PhaseDuration;

        /// <summary>Avanza el reloj. Devuelve true si cambió de fase (el sobrante pasa a la fase siguiente).</summary>
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

        /// <summary>Termina la fase actual de inmediato (dev mode).</summary>
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
