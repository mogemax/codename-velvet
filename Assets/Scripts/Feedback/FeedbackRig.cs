using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>Lookups shared by the feedback components.</summary>
    public static class FeedbackRig
    {
        public static T GetOrAdd<T>(GameObject owner) where T : Component =>
            owner.TryGetComponent(out T component) ? component : owner.AddComponent<T>();

        /// <summary>
        /// The child that holds every sprite of <paramref name="root"/> (their closest common parent below the root),
        /// or null when a sprite sits on the root itself. Squash and shake move this child, never the root: the
        /// root carries the collider and the Rigidbody.
        /// </summary>
        public static Transform VisualRoot(Transform root)
        {
            Transform common = null;
            foreach (SpriteRenderer sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
                common = common == null ? sprite.transform : CommonParent(common, sprite.transform);

            if (common == null || common == root)
                return null;
            while (common.parent != root)
                common = common.parent;
            return common;
        }

        private static Transform CommonParent(Transform a, Transform b)
        {
            for (Transform t = a; t != null; t = t.parent)
                if (b == t || b.IsChildOf(t))
                    return t;
            return null;
        }
    }
}
