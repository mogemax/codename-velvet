using UnityEngine;

namespace CodenameVelvet.Characters
{
    /// <summary>
    /// One selectable character. Characters only differ in looks, so this holds
    /// presentation data only — gameplay tuning stays on the shared Player prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "Characters/Character Definition", fileName = "New CharacterDefinition")]
    public class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Character";
        [Tooltip("Placeholder look: lobby portrait background and tint of the character's sprites.")]
        [SerializeField] private Color color = Color.white;
        [Tooltip("Optional. When empty the lobby shows a plain color swatch.")]
        [SerializeField] private Sprite portrait;

        public string DisplayName => displayName;
        public Color Color => color;
        public Sprite Portrait => portrait;
    }
}
