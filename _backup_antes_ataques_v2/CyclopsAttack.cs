using System;
using UnityEngine;
using UnityEngine.Events;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// Data for one cyclops attack. The hit happens <see cref="windup"/> seconds after the
    /// attack starts (or when the clip fires the Animation Event, if enabled on the brain),
    /// damages every player inside a sphere of <see cref="impactRadius"/> placed
    /// <see cref="impactForwardOffset"/> in front of the cyclops, and then the cyclops stays
    /// committed for <see cref="recovery"/> seconds before it can act again.
    /// </summary>
    [Serializable]
    public class CyclopsAttack
    {
        [Tooltip("Solo para identificarlo en el Inspector y en los logs.")]
        public string name = "Ataque";
        [Tooltip("Trigger del Animator que inicia la animación de este ataque.")]
        public string animatorTrigger = "Attack";
        [Tooltip("Probabilidad relativa frente a los otros ataques disponibles.")]
        [Min(0f)] public float weight = 1f;

        [Header("Rango (hueco entre cuerpos, en el suelo)")]
        [Min(0f)] public float minGap = 0f;
        [Min(0f)] public float maxGap = 2f;

        [Header("Tiempos (segundos)")]
        [Tooltip("Desde que empieza el ataque hasta el golpe.")]
        [Min(0f)] public float windup = 0.6f;
        [Tooltip("Desde el golpe hasta que puede volver a moverse (p. ej. levantarse del suelo).")]
        [Min(0f)] public float recovery = 1f;
        [Tooltip("Tiempo antes de poder repetir ESTE ataque.")]
        [Min(0f)] public float cooldown = 3f;

        [Header("Golpe")]
        [Min(0)] public int damage = 1;
        [Min(0f)] public float impactRadius = 2f;
        [Tooltip("Qué tan adelante del cíclope cae el golpe (0 = a sus pies).")]
        public float impactForwardOffset = 1f;
        [Min(0f)] public float knockbackForce = 8f;
        [Min(0f)] public float knockbackStun = 0.5f;

        [Tooltip("Se llama en el momento del golpe con la posición del impacto (VFX, sacudida de cámara, sonido).")]
        public UnityEvent<Vector3> onImpact = new UnityEvent<Vector3>();

        [NonSerialized] public float readyTime;

        public bool IsReady(float now) => now >= readyTime;
        public bool CoversGap(float gap) => gap >= minGap && gap <= maxGap;
    }
}
