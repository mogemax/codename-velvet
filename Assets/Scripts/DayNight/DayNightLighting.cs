using UnityEngine;

namespace CodenameVelvet.DayNight
{
    /// <summary>
    /// Darkens the scene at night: blends the directional light and the ambient intensity between their
    /// day values (whatever the scene has) and their night values, with a smooth transition on each change.
    /// The default sprite material may ignore lights; in that case only the map darkens.
    /// </summary>
    public class DayNightLighting : MonoBehaviour
    {
        [SerializeField] private DayNightCycle cycle;
        [SerializeField] private Light sun;

        [Header("Night")]
        [SerializeField] private Color nightSunColor = new Color(0.45f, 0.55f, 0.9f);
        [Tooltip("Night light intensity relative to the day one.")]
        [SerializeField, Range(0f, 1f)] private float nightSunFactor = 0.25f;
        [Tooltip("Night ambient intensity relative to the day one.")]
        [SerializeField, Range(0f, 1f)] private float nightAmbientFactor = 0.35f;
        [SerializeField, Min(0.01f)] private float transitionSeconds = 8f;

        private Color daySunColor;
        private float daySunIntensity;
        private float dayAmbientIntensity;
        private float nightness; // 0 = day, 1 = night

        private void Awake()
        {
            if (sun != null)
            {
                daySunColor = sun.color;
                daySunIntensity = sun.intensity;
            }
            dayAmbientIntensity = RenderSettings.ambientIntensity;
        }

        private void Start()
        {
            nightness = Target();
            Apply();
        }

        private void Update()
        {
            nightness = Mathf.MoveTowards(nightness, Target(), Time.deltaTime / transitionSeconds);
            Apply();
        }

        private float Target() => cycle != null && cycle.State.IsNight ? 1f : 0f;

        private void Apply()
        {
            if (sun != null)
            {
                sun.color = Color.Lerp(daySunColor, nightSunColor, nightness);
                sun.intensity = Mathf.Lerp(daySunIntensity, daySunIntensity * nightSunFactor, nightness);
            }
            RenderSettings.ambientIntensity = Mathf.Lerp(dayAmbientIntensity, dayAmbientIntensity * nightAmbientFactor, nightness);
        }
    }
}
