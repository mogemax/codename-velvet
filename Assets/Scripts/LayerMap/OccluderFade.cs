using System.Collections.Generic;
using CodenameVelvet.Map;
using CodenameVelvet.Player;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Makes this prop's sprites see-through while they hide most of a player, and returns them to their own opacity when
/// the player comes out. It casts rays from each player's camera to points all over their body against the drawing's
/// outline (the sprite's physics shape; colliders only cover the base), and only counts when the prop draws in front
/// (<see cref="DynamicSortingOrder"/>). Each prop decides alone.
/// </summary>
[DisallowMultipleComponent]
public class OccluderFade : MonoBehaviour
{
    // Shared by every prop with the same sprite.
    private static readonly Dictionary<Sprite, Vector2[][]> Outlines = new Dictionary<Sprite, Vector2[][]>();
    private static readonly List<Vector2> Path = new List<Vector2>();

    [Tooltip("Opacity while it hides a player.")]
    [SerializeField, Range(0f, 1f)] private float hiddenOpacity = 0.35f;
    [Tooltip("Seconds to fade out, and to come back.")]
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.2f;
    [Tooltip("Part of the player that must be hidden before it fades. Just over half, so two props that hide half a player each never lose them.")]
    [SerializeField, Range(0.3f, 1f)] private float hiddenPart = 0.55f;

    private SpriteRenderer[] sprites;
    private float[] ownAlphas;
    private float opacity = 1f;
    private bool hides;

    private void Start()
    {
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        ownAlphas = System.Array.ConvertAll(sprites, sprite => sprite.color.a);
    }

    private void OnDisable()
    {
        if (sprites != null) SetOpacity(1f);
    }

    private void LateUpdate()
    {
        // shortcut: one opacity for every split-screen view; fade per camera if players find it distracting.
        hides = HidesAnyPlayer();
        float target = hides ? hiddenOpacity : 1f;
        if (opacity != target)
            SetOpacity(Mathf.MoveTowards(opacity, target, Time.deltaTime / fadeSeconds));
    }

    private bool HidesAnyPlayer()
    {
        foreach (PlayerInput player in PlayerInput.all)
        {
            Camera cam = player.camera;
            if (cam == null || transform.position.z >= player.transform.position.z) continue; // the player draws on top
            if (player.TryGetComponent(out PlayerHealth health) && !health.IsAlive) continue; // not drawn

            if (OcclusionRules.IsCovered(HiddenPoints(player.transform.position, cam.transform), hides, hiddenPart))
                return true;
        }
        return false;
    }

    private int HiddenPoints(Vector3 feet, Transform cam)
    {
        int hidden = 0; // one bit per body point
        int count = 0;
        foreach (SpriteRenderer sprite in sprites)
        {
            if (!sprite.isVisible || sprite.sprite == null) continue;

            Bounds size = sprite.sprite.bounds;
            Rect bounds = new Rect(size.min, size.size);
            Vector2[][] outline = OutlineOf(sprite.sprite);

            Transform space = sprite.transform;
            Vector3 localCamera = space.InverseTransformPoint(cam.position);
            Vector3 localFeet = space.InverseTransformPoint(feet);
            Vector3 localRight = space.InverseTransformVector(cam.right);
            Vector3 localUp = space.InverseTransformVector(cam.up);

            for (int i = 0; i < OcclusionRules.BodyPoints; i++)
            {
                if ((hidden & (1 << i)) != 0) continue;
                Vector3 point = OcclusionRules.BodyPoint(i, localFeet, localRight, localUp);
                if (!OcclusionRules.Covers(localCamera, point, bounds, outline, sprite.flipX, sprite.flipY)) continue;
                hidden |= 1 << i;
                count++;
            }
        }
        return count;
    }

    /// <summary>The drawing's silhouette, or its whole rectangle if the sprite was imported without a physics shape.</summary>
    private static Vector2[][] OutlineOf(Sprite sprite)
    {
        if (Outlines.TryGetValue(sprite, out Vector2[][] outline)) return outline;

        outline = new Vector2[sprite.GetPhysicsShapeCount()][];
        for (int i = 0; i < outline.Length; i++)
        {
            sprite.GetPhysicsShape(i, Path);
            outline[i] = Path.ToArray();
        }
        if (outline.Length == 0)
        {
            Bounds b = sprite.bounds;
            outline = new[] { new Vector2[] { b.min, new Vector2(b.max.x, b.min.y), b.max, new Vector2(b.min.x, b.max.y) } };
        }
        Outlines.Add(sprite, outline);
        return outline;
    }

    private void SetOpacity(float value)
    {
        opacity = value;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue; // the prop is being destroyed
            Color color = sprites[i].color;
            color.a = ownAlphas[i] * value;
            sprites[i].color = color;
        }
    }
}
