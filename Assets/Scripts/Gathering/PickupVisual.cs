using UnityEngine;

namespace CodenameVelvet.Gathering
{
    /// <summary>Shows a drawing on a pickup: its sprite, tint and height on the ground, standing on its base.</summary>
    public static class PickupVisual
    {
        public static void Apply(GameObject pickup, Sprite sprite, Color tint, float worldHeight)
        {
            SpriteRenderer renderer = pickup.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null || sprite == null)
                return;

            renderer.sprite = sprite;
            renderer.color = tint;

            Transform visual = renderer.transform;
            float parentScale = visual.parent != null ? Mathf.Max(visual.parent.lossyScale.y, 0.0001f) : 1f;
            float scale = worldHeight / Mathf.Max(sprite.bounds.size.y, 0.0001f) / parentScale;
            visual.localScale = new Vector3(scale, scale, scale);
            visual.localPosition = Vector3.zero; // the sprite's pivot is its base: it stands on the ground
        }
    }
}
