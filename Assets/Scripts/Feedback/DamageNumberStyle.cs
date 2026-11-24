using TMPro;
using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>How damage numbers look and move.</summary>
    [CreateAssetMenu(menuName = "Velvet/Feedback/Damage Number Style", fileName = "DamageNumbers")]
    public class DamageNumberStyle : ScriptableObject
    {
        public TMP_FontAsset font;
        public Color color = Color.white;
        public Color outline = new Color(0.08f, 0.05f, 0.05f, 1f);
        [Range(0f, 1f)] public float outlineWidth = 0.3f;
        [Tooltip("TextMeshPro font size; about 10 = one world unit tall.")]
        public float fontSize = 14f;
        [Tooltip("Initial upward speed and random sideways speed.")]
        public float rise = 5f;
        public float sideways = 1.2f;
        public float gravity = 9f;
        public float lifetime = 0.8f;
        [Tooltip("Pop: starts this much bigger and settles.")]
        public float popScale = 1.6f;
        public int sortingOrder = 300;
    }
}
