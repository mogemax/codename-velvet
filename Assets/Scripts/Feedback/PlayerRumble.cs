using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// Gamepad rumble for this player only: their own gamepads (from their PlayerInput), never anyone else's.
    /// Pulses overlap through <see cref="RumbleMix"/>. Silent while the game is paused (time scale 0) and when the
    /// component goes away. Players on keyboard simply get nothing.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerRumble : MonoBehaviour
    {
        [Tooltip("Multiplies every pulse (0 = off).")]
        [SerializeField, Range(0f, 1f)] private float strength = 1f;

        private readonly RumbleMix mix = new RumbleMix();
        private PlayerInput input;
        private RumbleLevel applied;

        private void Awake() => input = GetComponent<PlayerInput>();

        /// <param name="low">Heavy motor (0-1).</param>
        /// <param name="high">Buzz motor (0-1).</param>
        public void Pulse(float low, float high, float seconds)
        {
            if (strength <= 0f)
                return;
            mix.Add(low * strength, high * strength, seconds, Time.unscaledTime);
        }

        private void Update()
        {
            RumbleLevel level = Time.timeScale > 0f ? mix.Sample(Time.unscaledTime) : default;
            if (!level.Equals(applied))
                Apply(level);
        }

        private void OnDisable()
        {
            mix.Clear();
            Apply(default);
        }

        private void Apply(RumbleLevel level)
        {
            applied = level;
            if (input == null)
                return;
            foreach (InputDevice device in input.devices)
                if (device is Gamepad gamepad)
                    gamepad.SetMotorSpeeds(level.Low, level.High);
        }
    }
}
