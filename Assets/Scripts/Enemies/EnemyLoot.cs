using CodenameVelvet.Gathering;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>What an enemy may leave when it dies: each drop rolled once by its chance, tossed out around it.</summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyLoot : MonoBehaviour
    {
        [SerializeField] private BonusDrop[] drops = new BonusDrop[0];
        [Tooltip("Drops land this far from the enemy (min, max).")]
        [SerializeField] private Vector2 dropRadius = new Vector2(0.8f, 1.6f);

        private void Awake() => GetComponent<EnemyHealth>().Died += _ => Drop();

        private void Drop()
        {
            foreach (BonusDrop drop in drops)
            {
                if (drop.prefab == null || Random.value >= drop.chance)
                    continue;

                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector3 at = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(dropRadius.x, dropRadius.y);
                GameObject loot = Instantiate(drop.prefab, at, Quaternion.identity);
                if (loot.TryGetComponent(out PickupMotion motion))
                    motion.LaunchFrom(transform.position);
            }
        }
    }
}
