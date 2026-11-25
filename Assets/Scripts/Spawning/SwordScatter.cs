using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.Gathering;
using CodenameVelvet.Map;
using CodenameVelvet.Multiplayer;
using UnityEngine;

namespace CodenameVelvet.Spawning
{
    /// <summary>
    /// Leaves one sword per player on the ground when the level starts, each at a different random spot. Spots over
    /// water or with something solid on them are only used when there are not enough free ones.
    /// </summary>
    public class SwordScatter : MonoBehaviour
    {
        private const string LogCategory = "SwordScatter";

        [SerializeField] private SessionRosterAsset rosterAsset;
        [SerializeField] private SwordPickup swordPrefab;
        [Tooltip("Where a sword may appear; at least one per player.")]
        [SerializeField] private Transform[] spots = new Transform[0];
        [Tooltip("Empty = 'Ground' layer.")]
        [SerializeField] private LayerMask groundMask;
        [Tooltip("A spot with something solid (not ground) closer than this is taken last.")]
        [SerializeField, Min(0f)] private float clearRadius = 1f;

        private void Awake()
        {
            if (groundMask.value == 0) groundMask = LayerMask.GetMask("Ground");
        }

        // Waits two frames: SpawnZone places its nodes and enemies one frame after its Start.
        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            Physics.SyncTransforms(); // nodes are scaled after they are created

            Vector3[] points = spots.Where(spot => spot != null)
                .Select(spot => PlayerSpawner.OnGround(spot.position, groundMask))
                .ToArray();
            int count = Mathf.Max(1, rosterAsset.Roster.Count); // empty roster = PlayerSpawner's test player
            if (points.Length < count)
                VelvetLog.Warn(LogCategory, this, $"{points.Length} spots for {count} players");

            var random = new System.Random(Random.Range(int.MinValue, int.MaxValue));
            List<int> picked = SpawnRules.PickSpots(points.Select(IsFree).ToArray(), count, random);
            foreach (int index in picked)
                Instantiate(swordPrefab, points[index], swordPrefab.transform.rotation);
        }

        // The water surface can run under dry ground near the shore, so it is not an obstacle by itself.
        private bool IsFree(Vector3 point) =>
            !WaterEdge.IsOverWater(point)
            && !Physics.CheckSphere(point, clearRadius, ~(groundMask | LayerMask.GetMask("Water")), QueryTriggerInteraction.Ignore);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            foreach (Transform spot in spots)
                if (spot != null) Gizmos.DrawWireSphere(spot.position, clearRadius);
        }
    }
}
