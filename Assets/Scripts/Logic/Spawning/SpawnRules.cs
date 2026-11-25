using System;
using System.Collections.Generic;
using System.Linq;
using CodenameVelvet.Enemies;
using UnityEngine;

namespace CodenameVelvet.Spawning
{
    /// <summary>Something already placed: where it stands and the radius of its footprint on the ground.</summary>
    public readonly struct SpawnBody
    {
        public SpawnBody(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }

        public Vector3 Position { get; }
        public float Radius { get; }
    }

    /// <summary>
    /// Pure rules that decide whether a point can host a spawn (enemy or resource). <c>SpawnZone</c> runs
    /// the physics and camera queries; this class only makes the decision.
    /// </summary>
    public static class SpawnRules
    {
        /// <summary>True when the surface (given its normal) is no steeper than <paramref name="maxSlopeDegrees"/>.</summary>
        public static bool IsFlatEnough(Vector3 surfaceNormal, float maxSlopeDegrees)
        {
            if (surfaceNormal == Vector3.zero)
                return false;
            return Vector3.Angle(surfaceNormal, Vector3.up) <= maxSlopeDegrees;
        }

        /// <summary>
        /// True when <paramref name="candidate"/> is at least <paramref name="minDistance"/> (on the XZ plane)
        /// from every occupied point.
        /// </summary>
        public static bool IsClear(Vector3 candidate, IReadOnlyList<Vector3> occupied, float minDistance)
        {
            if (occupied == null)
                throw new ArgumentNullException(nameof(occupied));

            foreach (Vector3 point in occupied)
            {
                if (EnemyAggroRules.PlanarDistance(candidate, point) < minDistance)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// How many things an area holds at <paramref name="perThousandUnits"/> per 1000 square units, rounded,
        /// never negative and never above <paramref name="max"/>.
        /// </summary>
        public static int CountForArea(float area, float perThousandUnits, int max)
        {
            float count = Math.Max(0f, area) * Math.Max(0f, perThousandUnits) / 1000f;
            return Math.Min(Math.Max(0, max), (int)Math.Round(count, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// True when a body of <paramref name="radius"/> at <paramref name="candidate"/> keeps at least
        /// <paramref name="gap"/> (on the XZ plane, edge to edge) from every other body.
        /// </summary>
        public static bool IsClearOfBodies(Vector3 candidate, float radius, IReadOnlyList<SpawnBody> bodies, float gap)
        {
            if (bodies == null)
                throw new ArgumentNullException(nameof(bodies));

            foreach (SpawnBody body in bodies)
            {
                if (EnemyAggroRules.PlanarDistance(candidate, body.Position) < radius + body.Radius + gap)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Chance (0-1) of placing something where a smooth noise reads <paramref name="noise"/>: nothing below
        /// <paramref name="emptyBelow"/>, always above <paramref name="fullAbove"/>, a ramp between. This makes
        /// dense patches and empty clearings instead of an even spread.
        /// </summary>
        public static float PatchWeight(float noise, float emptyBelow, float fullAbove)
        {
            if (fullAbove <= emptyBelow)
                return noise >= emptyBelow ? 1f : 0f;
            return Mathf.Clamp01((noise - emptyBelow) / (fullAbove - emptyBelow));
        }

        /// <summary>
        /// How many bodies a cluster gets: a value in <paramref name="range"/> (inclusive) picked by
        /// <paramref name="t01"/>, never more than <paramref name="remaining"/> and never less than 1.
        /// </summary>
        public static int ClusterSize(Vector2Int range, float t01, int remaining)
        {
            int min = Math.Max(1, Math.Min(range.x, range.y));
            int max = Math.Max(min, Math.Max(range.x, range.y));
            int size = min + (int)Math.Floor(Mathf.Clamp01(t01) * (max - min + 1 - 1e-4f));
            return Math.Max(1, Math.Min(size, remaining));
        }

        /// <summary>
        /// Scale of a cluster member, picked by <paramref name="t01"/> from <paramref name="range"/>: the center one
        /// from the upper half, the rest from the lower half, so a cluster has a big piece with smaller ones around.
        /// </summary>
        public static float ClusterMemberScale(bool isCenter, Vector2 range, float t01)
        {
            float min = Math.Min(range.x, range.y), max = Math.Max(range.x, range.y);
            float mid = (min + max) * 0.5f;
            return isCenter ? Mathf.Lerp(mid, max, Mathf.Clamp01(t01)) : Mathf.Lerp(min, mid, Mathf.Clamp01(t01));
        }

        /// <summary>
        /// Indices of up to <paramref name="count"/> different spots at random: free ones first, blocked ones only
        /// when the free ones run out.
        /// </summary>
        public static List<int> PickSpots(IReadOnlyList<bool> isFree, int count, System.Random random)
        {
            if (isFree == null)
                throw new ArgumentNullException(nameof(isFree));

            return Enumerable.Range(0, isFree.Count)
                .OrderByDescending(index => isFree[index])
                .ThenBy(_ => random.Next())
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Spots for the weapons left on the ground: <paramref name="swords"/> and <paramref name="bows"/>, never two
        /// on the same spot (free spots first, as <see cref="PickSpots"/>). With too few spots the swords come first.
        /// </summary>
        public static (List<int> swords, List<int> bows) PickWeaponSpots(IReadOnlyList<bool> isFree, int swords, int bows, System.Random random)
        {
            List<int> picked = PickSpots(isFree, Math.Max(0, swords) + Math.Max(0, bows), random);
            int swordCount = Math.Min(Math.Max(0, swords), picked.Count);
            return (picked.GetRange(0, swordCount), picked.GetRange(swordCount, picked.Count - swordCount));
        }

        /// <summary>
        /// True when a viewport-space point (Camera.WorldToViewportPoint) is on screen, with the view grown
        /// by <paramref name="margin"/> so spawns never pop in right at the edge.
        /// </summary>
        public static bool IsInsideViewport(Vector3 viewportPoint, float margin)
        {
            return viewportPoint.z > 0f
                && viewportPoint.x >= -margin && viewportPoint.x <= 1f + margin
                && viewportPoint.y >= -margin && viewportPoint.y <= 1f + margin;
        }
    }
}
