using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// How an enemy gets around, kept independent of the navigation technique so a
    /// NavMesh-based mover can replace the Rigidbody one without touching the brain.
    /// </summary>
    public interface IEnemyMover
    {
        /// <summary>
        /// True once the current destination is reached (within the mover's own
        /// arrival tolerance) or when there is no destination at all.
        /// </summary>
        bool HasReachedDestination { get; }

        /// <summary>Starts or redirects movement toward a world point.</summary>
        void MoveTo(Vector3 destination, float speed);

        /// <summary>Stops moving and clears the destination.</summary>
        void Stop();
    }
}
