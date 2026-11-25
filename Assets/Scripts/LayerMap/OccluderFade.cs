using CodenameVelvet.Map;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Makes this prop's sprites see-through while they draw over a player, and returns them to their own opacity when
/// the player comes out. It casts a ray from each player's camera to their body and head against the drawing itself
/// (colliders only cover the base), and only counts when the prop draws in front (<see cref="DynamicSortingOrder"/>).
/// </summary>
[DisallowMultipleComponent]
public class OccluderFade : MonoBehaviour
{
    [Tooltip("Opacity while it hides a player.")]
    [SerializeField, Range(0f, 1f)] private float hiddenOpacity = 0.35f;
    [Tooltip("Seconds to fade out, and to come back.")]
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.2f;
    [Tooltip("Part of the drawing's rectangle that counts as covering (its corners are usually empty).")]
    [SerializeField, Range(0.1f, 1f)] private float coverage = 0.85f;

    private SpriteRenderer[] sprites;
    private float[] ownAlphas;
    private float opacity = 1f;

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
        float target = HidesAnyPlayer() ? hiddenOpacity : 1f;
        if (opacity != target)
            SetOpacity(Mathf.MoveTowards(opacity, target, Time.deltaTime / fadeSeconds));
    }

    private bool HidesAnyPlayer()
    {
        foreach (PlayerInput player in PlayerInput.all)
        {
            Camera cam = player.camera;
            if (cam == null || transform.position.z >= player.transform.position.z) continue; // the player draws on top
            if (!player.TryGetComponent(out Collider body)) continue;

            Vector3 center = body.bounds.center;
            Vector3 head = new Vector3(center.x, body.bounds.max.y, center.z);
            foreach (SpriteRenderer sprite in sprites)
                if (sprite.isVisible && (Covers(sprite, cam, center) || Covers(sprite, cam, head)))
                    return true;
        }
        return false;
    }

    private bool Covers(SpriteRenderer sprite, Camera cam, Vector3 point)
    {
        Transform space = sprite.transform;
        return OcclusionRules.Covers(space.InverseTransformPoint(cam.transform.position),
            space.InverseTransformPoint(point), sprite.localBounds, coverage);
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
