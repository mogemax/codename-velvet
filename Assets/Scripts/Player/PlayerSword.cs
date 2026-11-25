using UnityEngine;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// The sword this player carries: hidden until <see cref="Equip"/>, then shown in hand for good (dying does not
    /// drop it). While it is in hand the swing deals <see cref="Damage"/> instead of the bare-handed damage.
    /// </summary>
    public class PlayerSword : MonoBehaviour
    {
        [Tooltip("The sword drawing under the hand bone.")]
        [SerializeField] private GameObject heldSword;
        [Tooltip("Damage of a swing with the sword in hand.")]
        [SerializeField, Min(1)] private int damage = 2;

        private bool isPutAway;

        public bool HasSword { get; private set; }

        public int Damage => damage;

        /// <summary>The sword's drawing in hand (null if unset): another weapon held takes its place and look.</summary>
        public SpriteRenderer Drawing => heldSword != null ? heldSword.GetComponent<SpriteRenderer>() : null;

        private void Awake() => Show();

        public void Equip()
        {
            HasSword = true;
            Show();
        }

        /// <summary>Hides the sword for a moment (another weapon is in hand) or brings it back.</summary>
        public void PutAway(bool putAway)
        {
            isPutAway = putAway;
            Show();
        }

        private void Show()
        {
            if (heldSword != null) heldSword.SetActive(HasSword && !isPutAway);
        }
    }
}
