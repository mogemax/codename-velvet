using System;
using UnityEngine;

namespace CodenameVelvet.Multiplayer
{
    /// <summary>
    /// Pure viewport calculator for local split-screen. Returns one normalized
    /// <see cref="Camera.rect"/> per player, indexed by slot. Three players leave
    /// the remaining grid space empty (letterboxed) instead of stretching a view.
    /// </summary>
    public static class SplitScreenLayout
    {
        public const int MinPlayers = 1;
        public const int MaxPlayers = 4;

        public static Rect[] ComputeRects(int playerCount)
        {
            switch (playerCount)
            {
                case 1:
                    return new[] { new Rect(0f, 0f, 1f, 1f) };

                case 2:
                    // Side by side.
                    return new[]
                    {
                        new Rect(0f, 0f, 0.5f, 1f),
                        new Rect(0.5f, 0f, 0.5f, 1f),
                    };

                case 3:
                    // Two on top, one centered below.
                    return new[]
                    {
                        new Rect(0f, 0.5f, 0.5f, 0.5f),
                        new Rect(0.5f, 0.5f, 0.5f, 0.5f),
                        new Rect(0.25f, 0f, 0.5f, 0.5f),
                    };

                case 4:
                    // Full 2x2 grid, P1 top-left in reading order.
                    return new[]
                    {
                        new Rect(0f, 0.5f, 0.5f, 0.5f),
                        new Rect(0.5f, 0.5f, 0.5f, 0.5f),
                        new Rect(0f, 0f, 0.5f, 0.5f),
                        new Rect(0.5f, 0f, 0.5f, 0.5f),
                    };

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(playerCount), playerCount,
                        $"Split-screen supports {MinPlayers} to {MaxPlayers} players.");
            }
        }
    }
}
