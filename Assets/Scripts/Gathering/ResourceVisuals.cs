using System;
using CodenameVelvet.Inventory;
using UnityEngine;

namespace CodenameVelvet.Gathering
{
    /// <summary>
    /// How each resource looks, in one place: its drawing (pickup on the ground and inventory icon), a tint (mineral
    /// reuses the stone drawing in crystal blue) and its height on the ground.
    /// </summary>
    [CreateAssetMenu(menuName = "Gathering/Resource Visuals", fileName = "ResourceVisuals")]
    public class ResourceVisuals : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ResourceType type;
            public Sprite sprite;
            public Color tint;
            [Tooltip("Height of the pickup on the ground, in world units (the player is about 5).")]
            [Min(0.1f)] public float worldHeight;
        }

        [SerializeField] private Entry[] entries = new Entry[0];

        public bool TryGet(ResourceType type, out Entry entry)
        {
            foreach (Entry candidate in entries)
            {
                if (candidate.type == type && candidate.sprite != null)
                {
                    entry = candidate;
                    return true;
                }
            }
            entry = default;
            return false;
        }

        /// <summary>Editor setup only.</summary>
        public void SetEntries(Entry[] newEntries) => entries = newEntries;
    }
}
