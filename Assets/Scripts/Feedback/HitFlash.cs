using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// Tints every sprite of this object toward a color for a moment ("_FlashColor" in "Velvet/Sprite": rgb = color,
    /// a = amount). Holds for the first half, then fades. Sleeps (disabled) when idle.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        private static readonly int FlashId = Shader.PropertyToID("_FlashColor");

        private SpriteMaterials materials;
        private Color color;
        private float duration;
        private float timeLeft;

        private void Awake()
        {
            materials = SpriteMaterials.On(gameObject);
            enabled = false;
        }

        public void Flash(Color flashColor, float seconds)
        {
            if (seconds <= 0f || flashColor.a <= 0f)
                return;
            color = flashColor;
            duration = timeLeft = seconds;
            enabled = true;
            Apply(1f);
        }

        private void Update()
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                Apply(0f);
                enabled = false;
                return;
            }
            Apply(Mathf.Min(1f, 2f * timeLeft / duration));
        }

        private void Apply(float amount)
        {
            Color value = color;
            value.a *= amount;
            materials.SetColor(FlashId, value);
        }
    }
}
