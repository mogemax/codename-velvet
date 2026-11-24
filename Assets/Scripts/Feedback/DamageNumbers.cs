using System.Collections.Generic;
using CodenameVelvet.Cameras;
using TMPro;
using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// Floating damage numbers: they pop, jump up, fall and fade. World-space text that faces every split-screen
    /// camera (<see cref="ViewTilt"/>). Pooled: finished numbers are hidden and reused.
    /// </summary>
    public static class DamageNumbers
    {
        private static readonly Stack<DamageNumber> Pool = new Stack<DamageNumber>();

        public static void Show(DamageNumberStyle style, int amount, Vector3 position)
        {
            if (style == null || style.font == null)
                return;

            DamageNumber number = null;
            while (Pool.Count > 0 && number == null)
                number = Pool.Pop(); // a scene change destroys pooled ones
            if (number == null)
                number = Create();

            number.Play(style, amount, position);
        }

        internal static void Release(DamageNumber number)
        {
            number.gameObject.SetActive(false);
            Pool.Push(number);
        }

        private static DamageNumber Create()
        {
            var holder = new GameObject("DamageNumber");
            holder.AddComponent<ViewTilt>();
            TextMeshPro text = holder.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return holder.AddComponent<DamageNumber>();
        }
    }

    /// <summary>One number on screen; driven by <see cref="DamageNumbers"/>.</summary>
    public class DamageNumber : MonoBehaviour
    {
        private const float PopSeconds = 0.12f;

        private TextMeshPro text;
        private DamageNumberStyle style;
        private Vector3 velocity;
        private float age;

        private void Awake() => text = GetComponent<TextMeshPro>();

        internal void Play(DamageNumberStyle numberStyle, int amount, Vector3 position)
        {
            style = numberStyle;
            age = 0f;
            transform.position = position;
            velocity = new Vector3(Random.Range(-style.sideways, style.sideways), style.rise, 0f);

            if (text.font != style.font)
                text.font = style.font;
            text.text = amount.ToString();
            text.fontSize = style.fontSize;
            text.color = style.color;
            text.outlineColor = style.outline;
            text.outlineWidth = style.outlineWidth;
            text.sortingOrder = style.sortingOrder;
            gameObject.SetActive(true);
            Apply();
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= style.lifetime)
            {
                DamageNumbers.Release(this);
                return;
            }
            velocity.y -= style.gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            Apply();
        }

        private void Apply()
        {
            float pop = age < PopSeconds ? Mathf.Lerp(style.popScale, 1f, age / PopSeconds) : 1f;
            transform.localScale = Vector3.one * pop;
            float fadeStart = style.lifetime * 0.6f;
            Color color = style.color;
            color.a *= age < fadeStart ? 1f : 1f - (age - fadeStart) / (style.lifetime - fadeStart);
            text.alpha = color.a;
        }
    }
}
