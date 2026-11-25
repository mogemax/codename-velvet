using System.Collections.Generic;
using UnityEngine;

namespace CodenameVelvet.Map
{
    /// <summary>
    /// Scatters grass tufts over the ground of its area when the level starts: in patches (noise), on walkable
    /// ground only (never on water, cliffs or under other geometry), mixing the drawings with random size, flip and
    /// shade. The seed makes the layout the same every time. Placed at runtime so the scene file stays small (no
    /// merge conflicts from thousands of objects). Each tuft is a plain sprite: its material faces the camera and
    /// sways by itself ("Velvet/Sprite" with billboard and wind), so tufts cost no script. Each tuft gets the sorting
    /// order of its depth (<see cref="DynamicSortingOrder"/>), like the characters, so it draws in front of or behind
    /// them correctly. Things that appear later (rocks, trees) clear the grass under them with <see cref="ClearAround"/>.
    /// Runs before other scripts' Start, so the grass is there before the spawners place anything.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GrassField : MonoBehaviour
    {
        private static readonly List<GrassField> Active = new List<GrassField>();

        private const float ProbeHeight = 500f;

        [Header("Look")]
        [SerializeField] private Sprite[] sprites = new Sprite[0];
        [Tooltip("\"Velvet/Sprite\" with Billboard and Wind on.")]
        [SerializeField] private Material material;
        [Tooltip("Tuft height in world units (min, max); the player is about 5.")]
        [SerializeField] private Vector2 heightRange = new Vector2(1.3f, 2.3f);
        [Tooltip("Darkest random shade; each tuft gets a shade between this and white.")]
        [SerializeField] private Color darkestShade = new Color(0.78f, 0.86f, 0.72f);

        [Header("Placement")]
        [Tooltip("Area on the ground plane (X, Z), centered on this object.")]
        [SerializeField] private Vector2 area = new Vector2(200f, 200f);
        [Tooltip("Distance between candidate spots, in world units: the same in every level, so a big map is not " +
                 "denser or sparser than a small one (the player is about 5 tall).")]
        [SerializeField, Min(0.5f)] private float spacing = 10f;
        [Tooltip("Safety cap; a map too big for it spreads the tufts further apart instead of filling one side.")]
        [SerializeField, Min(1)] private int maxTufts = 3000;
        [Tooltip("Size of the noise: lower = bigger patches.")]
        [SerializeField, Min(0.001f)] private float patchScale = 0.05f;
        [Tooltip("How much of the ground has grass (0 = none, 1 = all).")]
        [SerializeField, Range(0f, 1f)] private float coverage = 0.45f;
        [SerializeField, Range(0f, 89f)] private float maxSlope = 30f;
        [SerializeField] private LayerMask groundMask = 1 << 8;
        [Tooltip("Whatever the probe may hit first: if it is not ground (water, a rock), no tuft there.")]
        [SerializeField] private LayerMask probeMask = ~((1 << 6) | (1 << 7));
        [SerializeField] private int seed = 1234;

        private readonly List<SpriteRenderer> tufts = new List<SpriteRenderer>();

        public int TuftCount => tufts.Count;

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        private void Start() => Scatter();

        /// <summary>Hides the grass within <paramref name="radius"/> of <paramref name="center"/> (on the ground plane), in every field.</summary>
        public static void ClearAround(Vector3 center, float radius)
        {
            float radiusSquared = radius * radius;
            foreach (GrassField field in Active)
            {
                foreach (SpriteRenderer tuft in field.tufts)
                {
                    Vector3 offset = tuft.transform.position - center;
                    if (tuft.enabled && offset.x * offset.x + offset.z * offset.z < radiusSquared)
                        tuft.enabled = false;
                }
            }
        }

        private void Scatter()
        {
            if (sprites.Length == 0 || material == null)
                return;

            var random = new System.Random(seed);
            // A map too big for the cap spreads it thinner instead of filling one side and stopping (≈ coverage of the cells get a tuft).
            float step = Mathf.Max(spacing, Mathf.Sqrt(area.x * area.y * coverage / maxTufts));
            float noiseOffset = (float)random.NextDouble() * 1000f;
            float minUp = Mathf.Cos(maxSlope * Mathf.Deg2Rad);
            Vector3 center = transform.position;

            for (float x = -area.x * 0.5f; x < area.x * 0.5f && TuftCount < maxTufts; x += step)
            for (float z = -area.y * 0.5f; z < area.y * 0.5f && TuftCount < maxTufts; z += step)
            {
                float px = center.x + x + ((float)random.NextDouble() - 0.5f) * step;
                float pz = center.z + z + ((float)random.NextDouble() - 0.5f) * step;
                float patch = Mathf.PerlinNoise(px * patchScale + noiseOffset, pz * patchScale + noiseOffset);
                if (patch > coverage)
                    continue;

                var ray = new Ray(new Vector3(px, center.y + ProbeHeight, pz), Vector3.down);
                if (!Physics.Raycast(ray, out RaycastHit hit, ProbeHeight * 2f, probeMask, QueryTriggerInteraction.Ignore))
                    continue;
                if ((groundMask.value & (1 << hit.collider.gameObject.layer)) == 0 || hit.normal.y < minUp)
                    continue;

                Plant(hit.point, random);
            }
        }

        private void Plant(Vector3 point, System.Random random)
        {
            Sprite sprite = sprites[random.Next(sprites.Length)];
            float height = Mathf.Lerp(heightRange.x, heightRange.y, (float)random.NextDouble());

            var tuft = new GameObject("Grass");
            tuft.transform.SetParent(transform, false);
            tuft.transform.position = point;
            tuft.transform.localScale = Vector3.one * (height / Mathf.Max(sprite.bounds.size.y, 0.0001f));

            SpriteRenderer renderer = tuft.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.flipX = random.Next(2) == 0;
            renderer.color = Color.Lerp(darkestShade, Color.white, (float)random.NextDouble());
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.sortingOrder = DynamicSortingOrder.OrderForZ(point.z); // static: set once
            tufts.Add(renderer);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 0.3f);
            Gizmos.DrawWireCube(transform.position, new Vector3(area.x, 1f, area.y));
        }
    }
}
