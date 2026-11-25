using UnityEngine;
using UnityEngine.UI;

namespace CodenameVelvet.Audio
{
    /// <summary>Ties a settings slider (0-100) to the game's overall volume (<see cref="GameVolume"/>).</summary>
    [RequireComponent(typeof(Slider))]
    public class VolumeSlider : MonoBehaviour
    {
        private Slider slider;

        private void Awake()
        {
            slider = GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = VolumeRules.MaxPercent;
        }

        private void OnEnable()
        {
            slider.SetValueWithoutNotify(GameVolume.Percent);
            slider.onValueChanged.AddListener(GameVolume.Set);
        }

        private void OnDisable() => slider.onValueChanged.RemoveListener(GameVolume.Set);
    }
}
