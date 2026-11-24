using System.Collections.Generic;
using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// Gives this object its own copy of every "Velvet/Sprite" material its sprites use, so per-object values (the
    /// cast-shadow ground height, the hit flash) never leak to other objects. A MaterialPropertyBlock can't be used:
    /// it turns off the GPU deformation of bone-animated sprites, which then stop animating. Copies are made on first
    /// use and destroyed with the object.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpriteMaterials : MonoBehaviour
    {
        private static readonly int MarkerId = Shader.PropertyToID("_ShadowGround"); // only "Velvet/Sprite" has it

        private readonly List<Material> copies = new List<Material>();
        private bool isReady;

        public static SpriteMaterials On(GameObject owner) => FeedbackRig.GetOrAdd<SpriteMaterials>(owner);

        public void SetVector(int id, Vector4 value)
        {
            EnsureCopies();
            foreach (Material copy in copies)
                copy.SetVector(id, value);
        }

        public void SetColor(int id, Color value)
        {
            EnsureCopies();
            foreach (Material copy in copies)
                copy.SetColor(id, value);
        }

        private void OnDestroy()
        {
            foreach (Material copy in copies)
                Destroy(copy);
        }

        private void EnsureCopies()
        {
            if (isReady)
                return;
            isReady = true;

            var byShared = new Dictionary<Material, Material>();
            foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
            {
                Material shared = sprite.sharedMaterial;
                if (shared == null || !shared.HasProperty(MarkerId))
                    continue;
                if (!byShared.TryGetValue(shared, out Material copy))
                {
                    copy = new Material(shared) { name = $"{shared.name} ({name})" };
                    byShared.Add(shared, copy);
                    copies.Add(copy);
                }
                sprite.sharedMaterial = copy;
            }
        }
    }
}
