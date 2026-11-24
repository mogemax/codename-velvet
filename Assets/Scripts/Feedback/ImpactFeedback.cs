using CodenameVelvet.Enemies;
using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// The cyclops' slams on the ground: a ring of dust and rumble for every player close enough to feel it
    /// (stronger the closer). The camera shake is the attack's own.
    /// </summary>
    [RequireComponent(typeof(CyclopsBrain))]
    public class ImpactFeedback : MonoBehaviour
    {
        [SerializeField] private BurstStyle dust;
        [Tooltip("Players within the impact radius × this feel it.")]
        [SerializeField, Min(1f)] private float rumbleReach = 3f;
        [SerializeField, Range(0f, 1f)] private float rumble = 0.8f;
        [SerializeField, Min(0f)] private float rumbleSeconds = 0.35f;
        [SerializeField] private LayerMask playerMask = 1 << 6;

        private readonly Collider[] nearby = new Collider[16];
        private CyclopsBrain brain;

        private void Awake() => brain = GetComponent<CyclopsBrain>();
        private void OnEnable() => brain.Impacted += OnImpact;
        private void OnDisable() => brain.Impacted -= OnImpact;

        private void OnImpact(Vector3 center, CyclopsAttack attack)
        {
            Bursts.Emit(dust, center, Mathf.Max(1f, attack.impactRadius));

            float reach = attack.impactRadius * rumbleReach;
            int count = Physics.OverlapSphereNonAlloc(center, reach, nearby, playerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                PlayerRumble player = nearby[i].GetComponentInParent<PlayerRumble>();
                nearby[i] = null;
                if (player == null)
                    continue;
                Vector3 offset = player.transform.position - center;
                offset.y = 0f;
                float closeness = 1f - Mathf.Clamp01(offset.magnitude / reach);
                player.Pulse(rumble * closeness, rumble * 0.5f * closeness, rumbleSeconds);
            }
        }
    }
}
