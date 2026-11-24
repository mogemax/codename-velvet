using CodenameVelvet.Inventory;
using UnityEngine;

namespace CodenameVelvet.Gathering
{
    /// <summary>
    /// Tuning data for one resource node type (tree, rock, mineral vein...). Every type
    /// shares the same <see cref="ResourceNode"/> component; only this asset changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Gathering/Resource Node Definition", fileName = "New ResourceNodeDefinition")]
    public class ResourceNodeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Resource Node";
        [Tooltip("Resource dropped when the node is hit.")]
        [SerializeField] private ResourceType resource = ResourceType.Wood;
        [Tooltip("Placeholder tint applied to the node's child SpriteRenderers.")]
        [SerializeField] private Color color = Color.white;

        [Header("Harvest")]
        [Tooltip("Sword hits before the node is depleted. Minimum 1.")]
        [SerializeField] private int hitsToDeplete = 3;
        [Tooltip("Pickups dropped on every hit. Minimum 1.")]
        [SerializeField] private int piecesPerHit = 1;
        [Tooltip("Seconds a depleted node stays gone before it comes back. 0 = never respawns.")]
        [SerializeField] private float respawnSeconds;

        [Header("Sound")]
        [Tooltip("One at random on every hit.")]
        [SerializeField] private AudioClip[] hitSounds;

        public string DisplayName => displayName;
        public ResourceType Resource => resource;
        public Color Color => color;
        public int HitsToDeplete => hitsToDeplete;
        public int PiecesPerHit => piecesPerHit;
        public float RespawnSeconds => respawnSeconds;
        public AudioClip[] HitSounds => hitSounds;

        private void OnValidate()
        {
            hitsToDeplete = Mathf.Max(1, hitsToDeplete);
            piecesPerHit = Mathf.Max(1, piecesPerHit);
            respawnSeconds = Mathf.Max(0f, respawnSeconds);
        }
    }
}
