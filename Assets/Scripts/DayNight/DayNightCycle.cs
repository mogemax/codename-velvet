using System;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Core.Events;
using CodenameVelvet.Multiplayer;
using UnityEngine;

namespace CodenameVelvet.DayNight
{
    /// <summary>
    /// Corre el ciclo día/noche de la partida (CYCLE-01/02). Anuncia cada cambio por <see cref="DayNightEvent"/>
    /// con la amenaza de esa noche, calculada con el número de noche y de jugadores de la sesión.
    /// Usa tiempo escalado: con el juego congelado (game over) el reloj se detiene.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        private const string LogCategory = "DayNight";

        [Header("Duración (segundos)")]
        [SerializeField, Min(1f)] private float daySeconds = 180f;
        [SerializeField, Min(1f)] private float nightSeconds = 120f;

        [Header("Amenaza nocturna")]
        [Tooltip("Cuánto sube la amenaza por cada noche después de la primera (0.25 = +25%).")]
        [SerializeField, Min(0f)] private float threatPerNight = 0.25f;
        [Tooltip("Cuánto sube por cada jugador además del primero.")]
        [SerializeField, Min(0f)] private float threatPerExtraPlayer = 0.35f;
        [SerializeField, Min(1f)] private float maxThreat = 3f;
        [Tooltip("Para contar jugadores. Vacío o sin jugadores = 1.")]
        [SerializeField] private SessionRosterAsset rosterAsset;

        [Header("Eventos")]
        [SerializeField] private DayNightEvent onPhaseChanged;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private DayNightClock clock;

        /// <summary>Mismo aviso que <see cref="onPhaseChanged"/>, para componentes de este mismo dominio.</summary>
        public event Action<DayNightState> PhaseChanged;

        public DayNightState State { get; private set; }
        public float Remaining => clock.Remaining;
        public float PhaseProgress => clock.PhaseProgress;

        private void Awake()
        {
            clock = new DayNightClock(daySeconds, nightSeconds);
            State = BuildState();
        }

        private void Start() => Announce();

        private void Update()
        {
            if (clock.Advance(Time.deltaTime))
                Announce();
        }

        /// <summary>Termina la fase actual ya (dev mode).</summary>
        public void SkipPhase()
        {
            clock.SkipPhase();
            Announce();
        }

        private void Announce()
        {
            State = BuildState();

            if (debugLogs)
                VelvetLog.Info(LogCategory, this, State.IsNight
                    ? $"Noche {State.NightNumber} (amenaza ×{State.ThreatMultiplier:0.##})"
                    : $"Día {State.NightNumber + 1}");

            if (onPhaseChanged != null) onPhaseChanged.Raise(State);
            PhaseChanged?.Invoke(State);
        }

        private DayNightState BuildState()
        {
            float threat = clock.Phase == DayPhase.Night
                ? NightThreat.Multiplier(clock.NightNumber, PlayerCount(), threatPerNight, threatPerExtraPlayer, maxThreat)
                : 1f;
            return new DayNightState(clock.Phase, clock.NightNumber, threat);
        }

        private int PlayerCount() => rosterAsset != null ? Mathf.Max(1, rosterAsset.Roster.Count) : 1;
    }
}
