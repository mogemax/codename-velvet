using System.Collections.Generic;
using CodenameVelvet.Combat;
using UnityEngine;

namespace CodenameVelvet.DebugTools
{
    /// <summary>
    /// Debug-only trigger volume: damages any living <see cref="IDamageable"/>
    /// standing inside it, once per <c>interval</c> seconds per target.
    /// Requires a Collider set to Is Trigger on this GameObject.
    /// </summary>
    public class DebugDamageTrigger : MonoBehaviour
    {
        [SerializeField] private int damage = 1;
        [SerializeField] private float interval = 1f;

        private readonly Dictionary<IDamageable, float> nextHitTime = new Dictionary<IDamageable, float>();

        private void OnTriggerStay(Collider other)
        {
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive)
                return;

            if (nextHitTime.TryGetValue(target, out float readyAt) && Time.time < readyAt)
                return;

            nextHitTime[target] = Time.time + interval;
            target.ApplyDamage(damage);
        }

        private void OnTriggerExit(Collider other)
        {
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target != null)
                nextHitTime.Remove(target);
        }
    }
}
