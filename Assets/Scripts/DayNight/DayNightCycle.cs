using System;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Core.Events;
using CodenameVelvet.Multiplayer;
using UnityEngine;

namespace CodenameVelvet.DayNight
{
    /// <summary>
    /// Runs the match's day/night cycle (CYCLE-01/02) and announces every change through
    /// <see cref="DayNightEvent"/>, with that night's threat computed from the night number and the
    /// session's player count. Uses scaled time, so the clock stops while the game is frozen.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        private const string LogCategory = "DayNight";

        [Header("Duration (seconds)")]
        [SerializeField, Min(1f)] private float daySeconds = 180f;
        [SerializeField, Min(1f)] private float nightSeconds = 120f;

        [Header("Night threat")]
        [Tooltip("Threat added per night after the first (0.25 = +25%).")]
        [SerializeField, Min(0f)] private float threatPerNight = 0.25f;
        [Tooltip("Threat added per player beyond the first.")]
        [SerializeField, Min(0f)] private float threatPerExtraPlayer = 0.35f;
        [SerializeField, Min(1f)] private float maxThreat = 3f;
        [Tooltip("Used to count players. Empty or no players = 1.")]
        [SerializeField] private SessionRosterAsset rosterAsset;

        [Header("Events")]
        [SerializeField] private DayNightEvent onPhaseChanged;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private DayNightClock clock;

        /// <summary>Same notification as <see cref="onPhaseChanged"/>, for components of this domain.</summary>
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

        /// <summary>Ends the current phase immediately (dev mode).</summary>
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
                    ? $"Night {State.NightNumber} (threat ×{State.ThreatMultiplier:0.##})"
                    : $"Day {State.NightNumber + 1}");

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
