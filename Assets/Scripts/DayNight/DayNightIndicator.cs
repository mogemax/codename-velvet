using TMPro;
using UnityEngine;

namespace CodenameVelvet.DayNight
{
    /// <summary>On-screen label with the current phase and the time left until it changes (CYCLE-01).</summary>
    public class DayNightIndicator : MonoBehaviour
    {
        [SerializeField] private DayNightCycle cycle;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Color dayColor = new Color(1f, 0.9f, 0.55f);
        [SerializeField] private Color nightColor = new Color(0.6f, 0.75f, 1f);

        private int shownSeconds = -1;
        private bool shownNight;

        private void Update()
        {
            if (cycle == null || label == null) return;

            DayNightState state = cycle.State;
            int seconds = Mathf.CeilToInt(cycle.Remaining);
            if (seconds == shownSeconds && state.IsNight == shownNight) return; // rewrite only on change

            shownSeconds = seconds;
            shownNight = state.IsNight;
            string time = $"{seconds / 60}:{seconds % 60:00}";
            label.text = state.IsNight
                ? $"NIGHT {state.NightNumber}  ·  dawn in {time}"
                : $"DAY {state.NightNumber + 1}  ·  night falls in {time}";
            label.color = state.IsNight ? nightColor : dayColor;
        }
    }
}
