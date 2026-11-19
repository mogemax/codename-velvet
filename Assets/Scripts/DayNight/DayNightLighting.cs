using UnityEngine;

namespace CodenameVelvet.DayNight
{
    /// <summary>
    /// Oscurece la escena de noche: mezcla la luz direccional y la luz ambiental entre sus valores de día
    /// (los que tiene la escena) y los de noche, en una transición suave al cambiar de fase.
    /// Nota: el material de sprites por defecto puede no reaccionar a la luz; entonces solo se oscurece el mapa.
    /// </summary>
    public class DayNightLighting : MonoBehaviour
    {
        [SerializeField] private DayNightCycle cycle;
        [SerializeField] private Light sun;

        [Header("Noche")]
        [SerializeField] private Color nightSunColor = new Color(0.45f, 0.55f, 0.9f);
        [Tooltip("Intensidad de la luz de noche respecto a la de día.")]
        [SerializeField, Range(0f, 1f)] private float nightSunFactor = 0.25f;
        [Tooltip("Luz ambiental de noche respecto a la de día.")]
        [SerializeField, Range(0f, 1f)] private float nightAmbientFactor = 0.35f;
        [SerializeField, Min(0.01f)] private float transitionSeconds = 8f;

        private Color daySunColor;
        private float daySunIntensity;
        private float dayAmbientIntensity;
        private float nightness; // 0 = día, 1 = noche

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
