using System;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Picks an index with probability proportional to its weight. Randomness is
    /// injected so callers own the seed (independent enemies, deterministic tests).
    /// </summary>
    public static class WeightedRandom
    {
        /// <summary>
        /// Returns an index of <paramref name="weights"/> chosen proportionally to its weight.
        /// Negative weights count as zero. Returns -1 when the array is empty or every weight is zero.
        /// </summary>
        public static int Pick(float[] weights, System.Random random)
        {
            if (weights == null)
                throw new ArgumentNullException(nameof(weights));
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            double total = 0;
            foreach (float w in weights)
                total += Math.Max(0f, w);
            if (total <= 0)
                return -1;

            double roll = random.NextDouble() * total;
            int lastPositive = -1;
            for (int i = 0; i < weights.Length; i++)
            {
                float w = Math.Max(0f, weights[i]);
                if (w <= 0f)
                    continue;
                lastPositive = i;
                if (roll < w)
                    return i;
                roll -= w;
            }
            // Floating point leftovers land on the last index that could be picked.
            return lastPositive;
        }
    }
}
