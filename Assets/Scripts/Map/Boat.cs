using CodenameVelvet.Core;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Feedback;
using CodenameVelvet.Inventory;
using CodenameVelvet.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Map
{
    /// <summary>
    /// A boat the players build together by bringing it materials. Its trigger is the area from which a player can
    /// use it with Interact: deliver while parts are missing (they appear one by one as it fills up), and once built,
    /// take everyone to the next level when all of them are there. Without a next level it is only repaired and does not sail.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(SquashStretch))]
    public class Boat : MonoBehaviour
    {
        private const string LogCategory = "Boat";
        private static readonly Vector2 DeliveryKick = new Vector2(0.06f, -0.1f);

        // 24 wood (two slots) + stone + rope = exactly the four inventory slots. Eight trees give that wood and,
        // at 50% each, about four ropes; three rocks give the stone.
        [SerializeField] private ResourceAmount[] cost =
        {
            new ResourceAmount(ResourceType.Wood, 24),
            new ResourceAmount(ResourceType.Stone, 12),
            new ResourceAmount(ResourceType.Rope, 4),
        };
        [Tooltip("Shown one by one, in this order, as materials arrive. The last one appears when it is finished.")]
        [SerializeField] private GameObject[] parts = new GameObject[0];
        [Tooltip("No grass grows closer than this.")]
        [SerializeField, Min(0f)] private float grassClearRadius = 8f;

        [Header("Journey")]
        [SerializeField] private MatchFlow matchFlow;
        [Tooltip("Empty = this boat does not sail from here; it only has to be built.")]
        [SerializeField] private string nextLevel = "Level_02";

        private BuildProgress progress;
        private SquashStretch squash;

        public bool IsBuilt => progress.IsComplete;

        /// <summary>Built, and has somewhere to sail to.</summary>
        public bool CanSail => IsBuilt && !string.IsNullOrEmpty(nextLevel);

        /// <summary>Pressing Interact does something: it still needs materials, or it can sail.</summary>
        public bool CanUse => !IsBuilt || (CanSail && EveryoneIsHere());

        /// <summary>What has been delivered of each material.</summary>
        public string Status { get; private set; }

        private void Awake()
        {
            progress = new BuildProgress(cost);
            squash = GetComponent<SquashStretch>();
            if (matchFlow == null && !string.IsNullOrEmpty(nextLevel))
                Debug.LogWarning($"{name}: Boat has no MatchFlow; it will not sail.", this);
            Refresh();
        }

        private void Start() => GrassField.ClearAround(transform.position, grassClearRadius);

        /// <summary>Sails if it is built; otherwise takes whatever the player carries that is still missing.</summary>
        public void Use(PlayerInventory inventory)
        {
            if (IsBuilt)
            {
                if (CanSail && matchFlow != null && EveryoneIsHere()) matchFlow.GoToLevel(nextLevel);
                return;
            }

            if (!inventory.DeliverTo(progress)) return;

            Refresh();
            squash.Kick(DeliveryKick);
            VelvetLog.Info(LogCategory, this, IsBuilt ? "Built" : Status);
        }

        // Downed players do not count as here: they come back on foot.
        private bool EveryoneIsHere()
        {
            foreach (PlayerInput player in PlayerInput.all)
                if (!player.TryGetComponent(out PlayerBoatBuilder builder) || builder.NearbyBoat != this) return false;
            return true;
        }

        private void Refresh()
        {
            Status = progress.ToString();
            int shown = progress.StepsDone(parts.Length);
            for (int i = 0; i < parts.Length; i++)
                parts[i].SetActive(i < shown);
        }
    }
}
