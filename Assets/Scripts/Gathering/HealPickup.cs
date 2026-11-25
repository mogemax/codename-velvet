using CodenameVelvet.Player;
using UnityEngine;

namespace CodenameVelvet.Gathering
{
    /// <summary>
    /// Food on the ground (an apple): a hurt, living player who touches it eats it at once and heals. It never goes
    /// to the inventory, and nobody at full health takes it (it stays for someone who needs it).
    /// </summary>
    public class HealPickup : MonoBehaviour, IPickup
    {
        [SerializeField, Min(1)] private int heal = 1;
        [Tooltip("Seconds after spawning before anyone can take it, so the drop is seen.")]
        [SerializeField, Min(0f)] private float pickupDelaySeconds = 0.4f;

        [Header("Look")]
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color tint = Color.white;
        [SerializeField, Min(0.1f)] private float worldHeight = 1.6f;

        private float collectableAt;
        private bool isEaten;

        private void Awake()
        {
            collectableAt = Time.time + pickupDelaySeconds;
            PickupVisual.Apply(gameObject, sprite, tint, worldHeight);
        }

        public bool CanBeTakenBy(PlayerHealth player) => player.IsAlive && player.CurrentHp < player.MaxHp;

        private void OnTriggerStay(Collider other)
        {
            if (isEaten || Time.time < collectableAt)
                return;

            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player == null || !CanBeTakenBy(player) || player.Heal(heal) <= 0)
                return;

            isEaten = true;
            Destroy(gameObject);
        }
    }
}
