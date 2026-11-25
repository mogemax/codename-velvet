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
    /// Leaves the weapons on the ground when the level starts: one sword per player and a fixed number of bows,
    /// each at a different random spot. Spots over water or with something solid on them are only used when there
    /// are not enough free ones.
    /// </summary>
    public class WeaponScatter : MonoBehaviour
    {
        private const string LogCategory = "WeaponScatter";

        [SerializeField] private SessionRosterAsset rosterAsset;
        [SerializeField] private SwordPickup swordPrefab;
        [SerializeField] private BowPickup bowPrefab;
        [Tooltip("Bows on the ground, however many players there are.")]
        [SerializeField, Min(0)] private int bowCount = 2;
        [Tooltip("Where a weapon may appear; at least one per player plus one per bow.")]
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
            int swords = Mathf.Max(1, rosterAsset.Roster.Count); // empty roster = PlayerSpawner's test player
            int bows = bowPrefab != null ? bowCount : 0;
            if (points.Length < swords + bows)
                VelvetLog.Warn(LogCategory, this, $"{points.Length} spots for {swords} swords and {bows} bows");

            var random = new System.Random(Random.Range(int.MinValue, int.MaxValue));
            (List<int> swordSpots, List<int> bowSpots) = SpawnRules.PickWeaponSpots(points.Select(IsFree).ToArray(), swords, bows, random);
            Place(swordPrefab, swordSpots, points);
            Place(bowPrefab, bowSpots, points);
        }

        private static void Place(WeaponPickup prefab, List<int> spots, Vector3[] points)
        {
            foreach (int index in spots)
                Instantiate(prefab, points[index], prefab.transform.rotation);
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
