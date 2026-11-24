using UnityEngine;

namespace CodenameVelvet.DayNight
{
    /// <summary>
    /// Darkens the scene at night, with a smooth transition on each change, between the day values (whatever the
    /// scene has) and the night values of:
    /// <list type="bullet">
    /// <item>the directional light and the ambient intensity;</item>
    /// <item>the sky colors, when the skybox uses "Velvet/Toon Sky" (on a runtime copy: the asset never changes),
    /// and the fog color;</item>
    /// <item>the global <c>_VelvetDarkness</c> color, which the unlit "Velvet/Sprite" and "Velvet/Toon Water"
    /// shaders apply, since they ignore lights.</item>
    /// </list>
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
        [SerializeField] private Color nightSkyTop = new Color(0.055f, 0.102f, 0.227f);
        [SerializeField] private Color nightSkyHorizon = new Color(0.169f, 0.247f, 0.42f);
        [SerializeField] private Color nightClouds = new Color(0.36f, 0.43f, 0.6f, 0.6f);
        [SerializeField] private Color nightFog = new Color(0.12f, 0.17f, 0.3f);
        [Tooltip("How much unlit sprites and water lose at night, per channel (0 = unchanged, 1 = black). " +
                 "It applies in linear color, so it looks milder than the number (0.85 ≈ 40% of the day brightness). " +
                 "More on red and green than on blue gives a moonlit tint.")]
        [SerializeField] private Color nightDarkness = new Color(0.85f, 0.82f, 0.7f, 0f);
        [SerializeField, Min(0.01f)] private float transitionSeconds = 8f;

        private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
        private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
        private static readonly int CloudColorId = Shader.PropertyToID("_CloudColor");
        private static readonly int DarknessId = Shader.PropertyToID("_VelvetDarkness");

        private Color daySunColor;
        private float daySunIntensity;
        private float dayAmbientIntensity;
        private Material sky;
        private Color daySkyTop;
        private Color daySkyHorizon;
        private Color dayClouds;
        private Color dayFog;
        private float nightness; // 0 = day, 1 = night

        private void Awake()
        {
            if (sun != null)
            {
                daySunColor = sun.color;
                daySunIntensity = sun.intensity;
            }
            dayAmbientIntensity = RenderSettings.ambientIntensity;
            dayFog = RenderSettings.fogColor;

            Material skybox = RenderSettings.skybox;
            if (skybox != null && skybox.HasProperty(TopColorId) && skybox.HasProperty(HorizonColorId))
            {
                sky = new Material(skybox);
                RenderSettings.skybox = sky;
                daySkyTop = sky.GetColor(TopColorId);
                daySkyHorizon = sky.GetColor(HorizonColorId);
                dayClouds = sky.HasProperty(CloudColorId) ? sky.GetColor(CloudColorId) : Color.clear;
            }
        }

        private void OnDestroy()
        {
            if (sky != null) Destroy(sky);
            Shader.SetGlobalColor(DarknessId, Color.clear); // the next scene (or the Editor) starts untouched
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
            RenderSettings.fogColor = Color.Lerp(dayFog, nightFog, nightness);
            Shader.SetGlobalColor(DarknessId, Color.Lerp(Color.clear, nightDarkness, nightness));

            if (sky != null)
            {
                sky.SetColor(TopColorId, Color.Lerp(daySkyTop, nightSkyTop, nightness));
                sky.SetColor(HorizonColorId, Color.Lerp(daySkyHorizon, nightSkyHorizon, nightness));
                if (sky.HasProperty(CloudColorId)) sky.SetColor(CloudColorId, Color.Lerp(dayClouds, nightClouds, nightness));
            }
        }
    }
}
