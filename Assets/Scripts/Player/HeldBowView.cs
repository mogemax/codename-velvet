using UnityEngine;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// The bow drawing built from its pieces (VelvetBowSetup): a fixed grip, two limbs that bend on hinges at the
    /// grip's ends, a string drawn as a line from tip to tip through the nocking point, and an arrow resting on it.
    /// <see cref="SetPull"/> 0 = at rest (straight string), 1 = fully drawn (limbs bent, string pulled back). The art
    /// shoots towards -X (left); the string is pulled towards +X. Pure look: the shot itself is <see cref="PlayerBow"/>'s.
    /// </summary>
    public class HeldBowView : MonoBehaviour
    {
        [SerializeField] private Transform topHinge;
        [SerializeField] private Transform bottomHinge;
        [Tooltip("Tip of each limb, in its hinge's space (where the string ties).")]
        [SerializeField] private Vector3 topTip;
        [SerializeField] private Vector3 bottomTip;
        [SerializeField] private LineRenderer bowString;
        [SerializeField] private Transform nockedArrow;
        [Tooltip("The arrow's tail, in its own space: it sits on the string.")]
        [SerializeField] private Vector3 arrowTail;
        [Tooltip("Limbs bend this much at full draw (degrees).")]
        [SerializeField, Range(0f, 45f)] private float maxBend = 14f;
        [Tooltip("How far back the string goes at full draw, in the bow's units (it is about 15 tall).")]
        [SerializeField, Min(0f)] private float maxPull = 3.2f;

        private Renderer[] renderers;
        private int[] baseOrders;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            baseOrders = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseOrders[i] = renderers[i].sortingOrder;
            ShowArrow(false);
            SetPull(0f);
        }

        /// <summary>Draws every piece from <paramref name="order"/> up, keeping their order among themselves.</summary>
        public void SetSorting(int layerId, int order)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingLayerID = layerId;
                renderers[i].sortingOrder = order + baseOrders[i];
            }
        }

        /// <summary>
        /// A bow <paramref name="height"/> world units tall under <paramref name="root"/> (a character's drawing, so it
        /// faces the camera with it), drawn in front of the whole character, hidden.
        /// </summary>
        public static HeldBowView CreateIn(HeldBowView prefab, Transform root, float height)
        {
            HeldBowView bow = Instantiate(prefab, root, false);
            bow.name = "Bow";
            float scale = height / Height(prefab);
            Vector3 rootScale = root.lossyScale;
            bow.transform.localScale = new Vector3(Mathf.Abs(scale / rootScale.x), Mathf.Abs(scale / rootScale.y), 1f);

            int front = int.MinValue, layer = 0;
            foreach (SpriteRenderer part in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (part.transform.IsChildOf(bow.transform)) continue;
                front = Mathf.Max(front, part.sortingOrder);
                layer = part.sortingLayerID;
            }
            if (front != int.MinValue) bow.SetSorting(layer, front + 1);
            bow.gameObject.SetActive(false);
            return bow;
        }

        /// <summary>
        /// Holds the bow out on one side of its owner as seen on screen (<paramref name="side"/> 1 = right), upright,
        /// its belly towards the shot. <paramref name="offset"/>: sideways and up from <paramref name="feet"/>.
        /// </summary>
        public void HoldAt(Vector3 feet, Vector3 screenRight, float side, Vector2 offset)
        {
            transform.position = feet + screenRight * (side * offset.x) + Vector3.up * offset.y;
            transform.localRotation = Quaternion.identity;

            // The drawing shoots left; the parent may be mirrored (the character facing the other way).
            bool parentMirrored = transform.parent != null && transform.parent.lossyScale.x < 0f;
            bool mirror = (side > 0f) != parentMirrored;
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (mirror ? -1f : 1f);
            transform.localScale = scale;
        }

        private static float Height(HeldBowView bow)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (SpriteRenderer part in bow.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Bounds local = part.sprite.bounds;
                Vector3 min = bow.transform.InverseTransformPoint(part.transform.TransformPoint(local.min));
                Vector3 max = bow.transform.InverseTransformPoint(part.transform.TransformPoint(local.max));
                if (first) { bounds = new Bounds(min, Vector3.zero); first = false; }
                bounds.Encapsulate(min);
                bounds.Encapsulate(max);
            }
            return Mathf.Max(bounds.size.y, 0.01f);
        }

        public void ShowArrow(bool show)
        {
            if (nockedArrow != null) nockedArrow.gameObject.SetActive(show);
        }

        public void SetPull(float pull)
        {
            pull = Mathf.Clamp01(pull);
            float bend = maxBend * pull;
            topHinge.localRotation = Quaternion.Euler(0f, 0f, -bend);
            bottomHinge.localRotation = Quaternion.Euler(0f, 0f, bend);

            Vector3 top = transform.InverseTransformPoint(topHinge.TransformPoint(topTip));
            Vector3 bottom = transform.InverseTransformPoint(bottomHinge.TransformPoint(bottomTip));
            Vector3 nock = (top + bottom) * 0.5f + Vector3.right * (maxPull * pull);

            bowString.SetPosition(0, top);
            bowString.SetPosition(1, nock);
            bowString.SetPosition(2, bottom);
            if (nockedArrow != null)
                nockedArrow.localPosition = nock - Vector3.Scale(arrowTail, nockedArrow.localScale);
        }
    }
}
