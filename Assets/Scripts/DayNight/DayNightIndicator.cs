using TMPro;
using UnityEngine;

namespace CodenameVelvet.DayNight
{
    /// <summary>Texto en pantalla: en qué fase vamos y cuánto falta para el cambio (CYCLE-01).</summary>
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
            if (seconds == shownSeconds && state.IsNight == shownNight) return; // solo reescribe al cambiar

            shownSeconds = seconds;
            shownNight = state.IsNight;
            string time = $"{seconds / 60}:{seconds % 60:00}";
            label.text = state.IsNight
                ? $"NOCHE {state.NightNumber}  ·  amanece en {time}"
                : $"DÍA {state.NightNumber + 1}  ·  la noche llega en {time}";
            label.color = state.IsNight ? nightColor : dayColor;
        }
    }
}
