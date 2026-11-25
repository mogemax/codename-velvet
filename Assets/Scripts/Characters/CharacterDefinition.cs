using UnityEngine;

namespace CodenameVelvet.Characters
{
    /// <summary>
    /// One selectable character. Characters only differ in looks, so this holds
    /// presentation data only — gameplay tuning stays on the shared Player prefab,
    /// of which <see cref="PlayerPrefab"/> is a variant with another visual.
    /// </summary>
    [CreateAssetMenu(menuName = "Characters/Character Definition", fileName = "New CharacterDefinition")]
    public class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Character";
        [Tooltip("Placeholder look: lobby portrait background and tint of the character's sprites.")]
        [SerializeField] private Color color = Color.white;
        [Tooltip("Optional. When empty the lobby shows a plain color swatch.")]
        [SerializeField] private Sprite portrait;
        [Tooltip("Variant of the Player prefab with this character's rig. When empty the level spawns the Player prefab itself.")]
        [SerializeField] private GameObject playerPrefab;

        public string DisplayName => displayName;
        public Color Color => color;
        public Sprite Portrait => portrait;
        public GameObject PlayerPrefab => playerPrefab;
    }
}
