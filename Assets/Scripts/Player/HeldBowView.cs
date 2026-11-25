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
