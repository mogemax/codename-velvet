using UnityEngine;

namespace CodenameVelvet.Combat
{
    /// <summary>
    /// Something that can be pushed and briefly stunned by a hit.
    /// </summary>
    public interface IKnockbackable
    {
        /// <summary>
        /// Pushes the target along <paramref name="direction"/> (planar XZ, need not be
        /// normalized — implementers normalize it and ignore Y) with an initial speed of
        /// <paramref name="force"/> units/second, and stops it from acting for
        /// <paramref name="stunSeconds"/>.
        /// </summary>
        void ApplyKnockback(Vector3 direction, float force, float stunSeconds);
    }
}
