using System.Collections.Generic;
using UnityEngine;

namespace CodenameVelvet.Feedback
{
    /// <summary>
    /// Emits particle bursts. One particle system per <see cref="BurstStyle"/>, created on first use in the active
    /// scene (so it goes away with it) and reused by every burst afterwards. Particles are billboards in world space:
    /// each split-screen camera sees them facing it.
    /// </summary>
    public static class Bursts
    {
        private const int MaxParticles = 600;
        private static readonly Dictionary<BurstStyle, ParticleSystem> Systems = new Dictionary<BurstStyle, ParticleSystem>();

        /// <param name="scale">Multiplies count and size (a bigger node, a heavier hit).</param>
        public static void Emit(BurstStyle style, Vector3 position, float scale = 1f)
        {
            if (style == null || style.material == null || scale <= 0f)
                return;

            ParticleSystem system = SystemFor(style);
            int count = Mathf.Max(1, Mathf.RoundToInt(Random.Range(style.count.x, style.count.y + 1) * scale));
            float sizeScale = Mathf.Sqrt(scale);

            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector2 flat = Random.insideUnitCircle.normalized;
                Vector3 direction = Vector3.Slerp(new Vector3(flat.x, 0f, flat.y), Vector3.up, style.upward * Random.Range(0.6f, 1f));
                Vector3 offset = Random.insideUnitSphere * style.spawnRadius;

                emit.position = position + offset;
                emit.velocity = direction.normalized * Random.Range(style.speed.x, style.speed.y);
                emit.startSize = Random.Range(style.size.x, style.size.y) * sizeScale;
                emit.startLifetime = Random.Range(style.lifetime.x, style.lifetime.y);
                emit.startColor = Color.Lerp(style.colorA, style.colorB, Random.value);
                emit.rotation = style.spin ? Random.Range(0f, 360f) : 0f;
                emit.angularVelocity = style.spin ? Random.Range(-540f, 540f) : 0f;
                system.Emit(emit, 1);
            }
        }

        private static ParticleSystem SystemFor(BurstStyle style)
        {
            if (Systems.TryGetValue(style, out ParticleSystem existing) && existing != null)
                return existing;

            var holder = new GameObject($"Bursts ({style.name})");
            ParticleSystem system = holder.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxParticles;
            main.gravityModifier = style.gravity;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            if (style.drag > 0f)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 1000f; // only the drag matters
                limit.dampen = 0f;
                limit.drag = style.drag * 4f;
            }

            ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
            sizeOverLife.enabled = style.shrink;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            ParticleSystem.ColorOverLifetimeModule colorOverLife = system.colorOverLifetime;
            colorOverLife.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = fade;

            ParticleSystemRenderer particleRenderer = holder.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = style.material;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = style.sortingOrder;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            system.Play();
            Systems[style] = system;
            return system;
        }
    }
}
