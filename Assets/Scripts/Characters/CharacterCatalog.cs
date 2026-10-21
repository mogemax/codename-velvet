using UnityEngine;

namespace CodenameVelvet.Characters
{
    /// <summary>
    /// The ordered list of selectable characters. The lobby stores a character
    /// as an index into this list, and the level resolves it back through the
    /// same asset — so both scenes must reference this one catalog.
    /// </summary>
    [CreateAssetMenu(menuName = "Characters/Character Catalog", fileName = "CharacterCatalog")]
    public class CharacterCatalog : ScriptableObject
    {
        [SerializeField] private CharacterDefinition[] characters = new CharacterDefinition[0];

        public int Count => characters.Length;

        public CharacterDefinition Get(int index) => characters[index];
    }
}
