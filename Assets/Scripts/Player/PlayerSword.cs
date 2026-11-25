using UnityEngine;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// The sword this player carries: hidden until <see cref="Equip"/>, then shown in hand for good (dying does not
    /// drop it). Only the look: combat does not depend on it.
    /// </summary>
    public class PlayerSword : MonoBehaviour
    {
        [Tooltip("The sword drawing under the hand bone.")]
        [SerializeField] private GameObject heldSword;

        public bool HasSword { get; private set; }

        private void Awake() => Show();

        public void Equip()
        {
            HasSword = true;
            Show();
        }

        private void Show()
        {
            if (heldSword != null) heldSword.SetActive(HasSword);
        }
    }
}
