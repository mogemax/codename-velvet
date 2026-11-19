using System;
using System.Collections.Generic;
using CodenameVelvet.Enemies;
using UnityEngine;

namespace CodenameVelvet.Spawning
{
    /// <summary>
    /// Reglas puras para decidir si un punto sirve para aparecer algo (enemigo o recurso).
    /// Las consultas a la física y a las cámaras las hace <c>SpawnZone</c>; aquí solo la decisión.
    /// </summary>
    public static class SpawnRules
    {
        /// <summary>True si la superficie (por su normal) no es más inclinada que <paramref name="maxSlopeDegrees"/>.</summary>
        public static bool IsFlatEnough(Vector3 surfaceNormal, float maxSlopeDegrees)
        {
            if (surfaceNormal == Vector3.zero)
                return false;
            return Vector3.Angle(surfaceNormal, Vector3.up) <= maxSlopeDegrees;
        }

        /// <summary>
        /// True si <paramref name="candidate"/> queda a al menos <paramref name="minDistance"/> (en el plano XZ)
        /// de todos los puntos ocupados.
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
        /// True si un punto en coordenadas de viewport (Camera.WorldToViewportPoint) cae dentro de la vista,
        /// ampliada por <paramref name="margin"/> para no aparecer justo en el borde.
        /// </summary>
        public static bool IsInsideViewport(Vector3 viewportPoint, float margin)
        {
            return viewportPoint.z > 0f
                && viewportPoint.x >= -margin && viewportPoint.x <= 1f + margin
                && viewportPoint.y >= -margin && viewportPoint.y <= 1f + margin;
        }
    }
}
