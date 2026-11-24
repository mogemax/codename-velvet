using System;
using System.Collections.Generic;

namespace CodenameVelvet.Feedback
{
    /// <summary>Motor speeds of a gamepad (0-1): low frequency = heavy rumble, high frequency = buzz.</summary>
    public readonly struct RumbleLevel : IEquatable<RumbleLevel>
    {
        public float Low { get; }
        public float High { get; }

        public RumbleLevel(float low, float high)
        {
            Low = low;
            High = high;
        }

        public bool IsSilent => Low <= 0f && High <= 0f;
        public bool Equals(RumbleLevel other) => Low.Equals(other.Low) && High.Equals(other.High);
        public override bool Equals(object obj) => obj is RumbleLevel other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Low, High);
    }

    /// <summary>
    /// Combines overlapping rumble pulses of one gamepad: each motor runs at the strongest pulse alive, and every
    /// pulse fades out linearly over its life. A hit landing during a heavy stomp never weakens the stomp.
    /// </summary>
    public sealed class RumbleMix
    {
        private readonly struct Pulse
        {
            public readonly float Low, High, Start, End;
            public Pulse(float low, float high, float start, float end) { Low = low; High = high; Start = start; End = end; }
        }

        private readonly List<Pulse> pulses = new List<Pulse>();

        public void Add(float low, float high, float seconds, float now)
        {
            if (seconds <= 0f || (low <= 0f && high <= 0f))
                return;
            pulses.Add(new Pulse(Clamp01(low), Clamp01(high), now, now + seconds));
        }

        public RumbleLevel Sample(float now)
        {
            float low = 0f, high = 0f;
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                Pulse pulse = pulses[i];
                if (now >= pulse.End)
                {
                    pulses.RemoveAt(i);
                    continue;
                }

                float left = (pulse.End - now) / (pulse.End - pulse.Start);
                low = Math.Max(low, pulse.Low * left);
                high = Math.Max(high, pulse.High * left);
            }
            return new RumbleLevel(low, high);
        }

        public void Clear() => pulses.Clear();

        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    }
}
