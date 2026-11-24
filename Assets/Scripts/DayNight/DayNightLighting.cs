using UnityEngine;
using UnityEngine.Rendering;

namespace CodenameVelvet.DayNight
{
    /// <summary>
    /// Colors the world through the cycle with three palettes: day (whatever the scene has), dusk/dawn (warm, at the
    /// end and the start of each day) and night. Every change is smooth. It drives:
    /// <list type="bullet">
    /// <item>the directional light and the ambient intensity;</item>
    /// <item>the sky colors, when the skybox uses "Velvet/Toon Sky" (on a runtime copy: the asset never changes),
    /// and the fog color;</item>
    /// <item>the sprite globals read by "Velvet/Sprite" and "Velvet/Toon Water": night darkness, how much of the
    /// sun's hue sprites take, how dark they get in shadow and how strong the shadow they cast is;</item>
    /// <item>optional post-processing volumes for dusk and night, by weight.</item>
    /// </list>
    /// </summary>
    public class DayNightLighting : MonoBehaviour
    {
        [SerializeField] private DayNightCycle cycle;
        [SerializeField] private Light sun;
        [SerializeField, Min(0.01f)] private float transitionSeconds = 8f;

        [Header("Dusk / dawn")]
        [Tooltip("Last seconds of the day that turn warm.")]
        [SerializeField, Min(0f)] private float duskSeconds = 35f;
        [Tooltip("First seconds of the day that are still warm.")]
        [SerializeField, Min(0f)] private float dawnSeconds = 20f;
        [SerializeField] private Color duskSunColor = new Color(1f, 0.62f, 0.4f);
        [Tooltip("Dusk light intensity relative to the day one.")]
        [SerializeField, Range(0f, 1f)] private float duskSunFactor = 0.8f;
        [SerializeField] private Color duskSkyTop = new Color(0.36f, 0.42f, 0.72f);
        [SerializeField] private Color duskSkyHorizon = new Color(1f, 0.66f, 0.48f);
        [SerializeField] private Color duskClouds = new Color(1f, 0.78f, 0.7f, 0.85f);
        [SerializeField] private Color duskFog = new Color(0.95f, 0.7f, 0.58f);
        [SerializeField] private Volume duskVolume;

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
        [SerializeField] private Volume nightVolume;

        [Header("Sprites")]
        [Tooltip("How much of the sun's hue sprites take (0 = never tinted, 1 = full sun color).")]
        [SerializeField, Range(0f, 1f)] private float spriteSunTint = 0.6f;
        [Tooltip("How much a sprite darkens in full shadow (0 = never, 1 = black).")]
        [SerializeField, Range(0f, 1f)] private float spriteShadowStrength = 0.45f;
        [Tooltip("Opacity of the shadow sprites cast in full daylight; it weakens with the sun.")]
        [SerializeField, Range(0f, 1f)] private float spriteCastShadow = 0.35f;

        private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
        private static readonly int HorizonColorId = Shader.PropertyToID("_HorizonColor");
        private static readonly int CloudColorId = Shader.PropertyToID("_CloudColor");
        private static readonly int DarknessId = Shader.PropertyToID("_VelvetDarkness");
        private static readonly int SunTintId = Shader.PropertyToID("_VelvetSunTint");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_VelvetShadowStrength");
        private static readonly int CastShadowId = Shader.PropertyToID("_VelvetCastShadow");

        private Color daySunColor;
        private float daySunIntensity;
        private float dayAmbientIntensity;
        private Material sky;
        private Color daySkyTop;
        private Color daySkyHorizon;
        private Color dayClouds;
        private Color dayFog;
        private float nightness; // 0 = day, 1 = night
        private float warmth;    // 0 = day, 1 = dusk/dawn

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
            // The next scene (or the Editor) starts untouched.
            Shader.SetGlobalColor(DarknessId, Color.clear);
            Shader.SetGlobalFloat(SunTintId, 0f);
            Shader.SetGlobalFloat(ShadowStrengthId, 0f);
            Shader.SetGlobalFloat(CastShadowId, 0f);
        }

        private void Start()
        {
            nightness = NightTarget();
            warmth = WarmthTarget();
            Apply();
        }

        private void Update()
        {
            float step = Time.deltaTime / transitionSeconds;
            nightness = Mathf.MoveTowards(nightness, NightTarget(), step);
            warmth = Mathf.MoveTowards(warmth, WarmthTarget(), step);
            Apply();
        }

        private float NightTarget() => cycle != null && cycle.State.IsNight ? 1f : 0f;

        private float WarmthTarget() => cycle == null ? 0f
            : DayNightPalette.Warmth(cycle.State.IsNight, cycle.Elapsed, cycle.Remaining, duskSeconds, dawnSeconds);

        /// <summary>Day → dusk by warmth, then → night by nightness.</summary>
        private Color Blend(Color day, Color dusk, Color night) =>
            Color.Lerp(Color.Lerp(day, dusk, warmth), night, nightness);

        private float Blend(float day, float dusk, float night) =>
            Mathf.Lerp(Mathf.Lerp(day, dusk, warmth), night, nightness);

        private void Apply()
        {
            if (sun != null)
            {
                sun.color = Blend(daySunColor, duskSunColor, nightSunColor);
                sun.intensity = Blend(daySunIntensity, daySunIntensity * duskSunFactor, daySunIntensity * nightSunFactor);
            }
            RenderSettings.ambientIntensity = Mathf.Lerp(dayAmbientIntensity, dayAmbientIntensity * nightAmbientFactor, nightness);
            RenderSettings.fogColor = Blend(dayFog, duskFog, nightFog);

            Shader.SetGlobalColor(DarknessId, Color.Lerp(Color.clear, nightDarkness, nightness));
            Shader.SetGlobalFloat(SunTintId, spriteSunTint);
            Shader.SetGlobalFloat(ShadowStrengthId, spriteShadowStrength);
            float sunLeft = Blend(1f, duskSunFactor, nightSunFactor); // the sun's strength relative to midday
            Shader.SetGlobalFloat(CastShadowId, spriteCastShadow * sunLeft);

            if (duskVolume != null) duskVolume.weight = warmth * (1f - nightness);
            if (nightVolume != null) nightVolume.weight = nightness;

            if (sky != null)
            {
                sky.SetColor(TopColorId, Blend(daySkyTop, duskSkyTop, nightSkyTop));
                sky.SetColor(HorizonColorId, Blend(daySkyHorizon, duskSkyHorizon, nightSkyHorizon));
                if (sky.HasProperty(CloudColorId)) sky.SetColor(CloudColorId, Blend(dayClouds, duskClouds, nightClouds));
            }
        }
    }
}
