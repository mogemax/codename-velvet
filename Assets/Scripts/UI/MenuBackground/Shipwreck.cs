using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace CodenameVelvet.UI
{
    /// <summary>
    /// The menu background's boat is wrecked on the way to the first level: the sea gets rough, a giant wave lifts
    /// the boat and flips it, it breaks apart (pieces fly, float or sink, foam splashes, the sea shakes), the sea calms
    /// down and the broken boat comes up and stays adrift. <see cref="Play"/> runs it once and ends when it is over
    /// (<see cref="duration"/>). Real time. Lives on the MenuBackground prefab, built by VelvetMenuBackgroundSetup.
    /// </summary>
    public class Shipwreck : MonoBehaviour
    {
        [Serializable]
        public class Piece
        {
            public RectTransform rect;
            [Tooltip("Center relative to the boat's pivot, in boat widths.")]
            public Vector2 offset;
            [Tooltip("Size, in boat widths.")]
            public Vector2 size;
            public bool floats;

            [NonSerialized] public Image image;
            [NonSerialized] public Vector2 velocity;
            [NonSerialized] public float angle, spin, wetSince;
            [NonSerialized] public PieceState state;
        }

        public enum PieceState { Flying, Floating, Sinking, Gone }

        private const float WaterY = -15f; // where pieces meet the water, below the sea's line

        [Header("Parts (set by the setup tool)")]
        [SerializeField] private RectTransform sea;
        [SerializeField] private RectTransform boat;
        [SerializeField] private SailingBoat sailing;
        [SerializeField] private RectTransform brokenBoat;
        [SerializeField] private RectTransform giantWave;
        [SerializeField] private RectTransform splash;
        [SerializeField] private ScrollingWaveLayer[] layers;
        [SerializeField] private Piece[] pieces;

        [Header("Timing (seconds from the start)")]
        [Tooltip("The wave breaks on the boat and it breaks apart.")]
        [SerializeField, Min(0.7f)] private float impactTime = 2.1f;
        [Tooltip("When it is over and the game moves on.")]
        [SerializeField, Min(1f)] private float duration = 6f;
        [Tooltip("Giant wave's speed, canvas units per second.")]
        [SerializeField] private float waveSpeed = 900f;

        [Header("Sea")]
        [SerializeField] private float shake = 22f;
        [SerializeField] private float roughSpeed = 2.4f;
        [SerializeField] private float roughBob = 2f;
        [Tooltip("How calm the sea is left (times its usual speed and bob).")]
        [SerializeField] private float calmSpeed = 0.3f;
        [SerializeField] private float calmBob = 0.5f;

        [Header("Broken boat")]
        [Tooltip("Tilt it is left adrift with, degrees.")]
        [SerializeField] private float driftTilt = -4f;

        /// <summary>The boat breaks apart. The menu music stops here; an impact sound would go here too.</summary>
        public static event Action Impacted;

        private float startTime;
        private bool running, hit;
        private Vector2 startPos, flipPos, impactPos, seaPos;
        private float startAngle, flipAngle;
        private float waveLipX;
        private Image waveImage, splashImage, brokenImage;

        private float Now => Time.unscaledTime;
        private float FlipTime => impactTime - 0.65f;

        private void Awake()
        {
            waveImage = Prepare(giantWave);
            splashImage = Prepare(splash);
            brokenImage = Prepare(brokenBoat);
            foreach (Piece piece in pieces)
                piece.image = Prepare(piece.rect);
        }

        private static Image Prepare(RectTransform rect)
        {
            if (rect == null) return null;
            rect.gameObject.SetActive(false);
            return rect.GetComponent<Image>();
        }

        /// <summary>Wrecks the boat; finishes when it is over. Once.</summary>
        public IEnumerator Play()
        {
            if (!running) Begin();
            while (Now - startTime < duration)
                yield return null;
        }

        private void Begin()
        {
            running = true;
            startTime = Now;

            // From here on this moves the boat; take it off its screen-width anchor without a jump.
            sailing.enabled = false;
            startPos = new Vector2((boat.anchorMin.x - 0.5f) * sea.rect.width, 0f) + boat.anchoredPosition;
            boat.anchorMin = boat.anchorMax = new Vector2(0.5f, 0.5f);
            boat.anchoredPosition = startPos;
            startAngle = SignedAngle(boat.localEulerAngles.z);
            seaPos = sea.anchoredPosition;

            waveLipX = startPos.x - 300f; // its lip falls on the boat right at the impact
            giantWave.anchorMin = giantWave.anchorMax = new Vector2(0.5f, 0.5f);
            giantWave.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!running) return;
            float t = Now - startTime;

            Sea(t);
            Wave(t);
            if (!hit)
            {
                if (t < FlipTime) Approach(t);
                else Flip(t);
                if (t >= impactTime) Impact();
            }
            else
            {
                Splash(t - impactTime);
                Pieces();
                Broken(t);
            }

            float strength = hit ? shake * Mathf.Exp(-(t - impactTime) * 3.2f) : t > FlipTime ? 4f : 0f;
            Vector2 jolt = strength > 0.05f
                ? new Vector2(Mathf.PerlinNoise(t * 25f, 0f) - 0.5f, Mathf.PerlinNoise(0f, t * 25f) - 0.5f) * 2f * strength
                : Vector2.zero;
            sea.anchoredPosition = seaPos + jolt;
        }

        private void Sea(float t)
        {
            float rough = Ease(0f, 1.8f, t);
            float calm = Ease(impactTime + 0.7f, impactTime + 2.5f, t);
            float speed = Mathf.Lerp(Mathf.Lerp(1f, roughSpeed, rough), calmSpeed, calm);
            float bob = Mathf.Lerp(Mathf.Lerp(1f, roughBob, rough), calmBob, calm);
            foreach (ScrollingWaveLayer layer in layers)
                layer.SetScale(speed, bob);
        }

        private void Wave(float t)
        {
            if (!giantWave.gameObject.activeSelf) return;
            float x = waveLipX + waveSpeed * (impactTime - t);
            float grow = Mathf.Lerp(0.55f, 1f, Ease(0f, impactTime - 0.2f, t));
            float collapse = Ease(impactTime + 0.1f, impactTime + 1.5f, t);
            giantWave.anchoredPosition = new Vector2(x, -10f);
            giantWave.localScale = new Vector3(1f, Mathf.Lerp(grow, 0.3f, collapse), 1f);
            float alpha = 1f - Ease(impactTime + 0.4f, impactTime + 1.3f, t);
            waveImage.color = new Color(1f, 1f, 1f, alpha);
            if (alpha <= 0.001f) giantWave.gameObject.SetActive(false);
        }

        // Rocks harder and harder as the wave comes, then the bow rises with it.
        private void Approach(float t)
        {
            float u = Mathf.Clamp01(t / FlipTime);
            float w = t * Mathf.PI * 2f / 1.1f;
            float lift = Ease(FlipTime - 0.5f, FlipTime, t);
            Vector2 pos = startPos + new Vector2(-40f * u, Mathf.Sin(w) * (12f + 18f * u) + lift * 160f);
            float angle = Mathf.Lerp(startAngle, 0f, Ease(0f, 0.4f, t)) + Mathf.Cos(w) * (3f + 9f * u) + lift * 14f;
            boat.anchoredPosition = pos;
            boat.localRotation = Quaternion.Euler(0f, 0f, angle);
            flipPos = pos;
            flipAngle = angle;
        }

        // The wave drags it back and turns it over (its height scale goes from 1 to -1).
        private void Flip(float t)
        {
            float k = Mathf.Clamp01((t - FlipTime) / (impactTime - FlipTime));
            float e = k * k;
            boat.anchoredPosition = flipPos + new Vector2(-200f * e, 140f * Mathf.Sin(k * Mathf.PI) - 60f * e);
            boat.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(flipAngle, 60f, e));
            boat.localScale = new Vector3(1f, Mathf.Cos(e * Mathf.PI), 1f);
        }

        private void Impact()
        {
            hit = true;
            impactPos = boat.anchoredPosition;
            float angle = SignedAngle(boat.localEulerAngles.z);
            float flipped = boat.localScale.y < 0f ? -1f : 1f;
            float width = boat.rect.width;
            boat.gameObject.SetActive(false);

            // Every piece starts where it was in the boat and flies off from its center.
            Quaternion turn = Quaternion.Euler(0f, 0f, angle);
            foreach (Piece piece in pieces)
            {
                RectTransform rect = piece.rect;
                rect.gameObject.SetActive(true);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = piece.size * width;
                Vector2 offset = turn * new Vector2(piece.offset.x, piece.offset.y * flipped) * width;
                rect.anchoredPosition = impactPos + offset;
                rect.localScale = new Vector3(1f, flipped, 1f);
                Vector2 away = offset.sqrMagnitude > 1f ? offset.normalized : Vector2.up;
                piece.velocity = away * Random.Range(150f, 450f) + new Vector2(-260f, Random.Range(350f, 750f));
                piece.angle = angle;
                piece.spin = Random.Range(90f, 420f) * (Random.value < 0.5f ? -1f : 1f);
                piece.state = PieceState.Flying;
                piece.image.color = Color.white;
            }

            splash.gameObject.SetActive(true);
            splash.anchorMin = splash.anchorMax = new Vector2(0.5f, 0.5f);
            splash.pivot = new Vector2(0.5f, 0.12f);
            splash.anchoredPosition = new Vector2(impactPos.x, -10f);

            Impacted?.Invoke();
        }

        private void Splash(float since)
        {
            if (!splash.gameObject.activeSelf) return;
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(since / 0.9f), 3f);
            float scale = Mathf.Lerp(0.35f, 1.25f, grow);
            splash.localScale = new Vector3(scale, scale, 1f);
            float alpha = 1f - Ease(0.45f, 1.5f, since);
            splashImage.color = new Color(1f, 1f, 1f, alpha);
            if (alpha <= 0.001f) splash.gameObject.SetActive(false);
        }

        private void Pieces()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Now;
            foreach (Piece piece in pieces)
            {
                if (piece.state == PieceState.Gone) continue;
                Vector2 pos = piece.rect.anchoredPosition;
                float alpha = 1f;
                switch (piece.state)
                {
                    case PieceState.Flying:
                        piece.velocity += Vector2.down * 1500f * dt;
                        pos += piece.velocity * dt;
                        piece.angle += piece.spin * dt;
                        if (pos.y < WaterY && piece.velocity.y < 0f)
                        {
                            piece.state = piece.floats ? PieceState.Floating : PieceState.Sinking;
                            piece.wetSince = now;
                            piece.velocity = piece.floats ? new Vector2(Random.Range(-90f, -30f), 0f) : piece.velocity * 0.15f;
                            piece.spin *= 0.1f;
                        }
                        break;
                    case PieceState.Floating: // drifts away, bobbing, and fades
                        pos.x += piece.velocity.x * dt;
                        pos.y = Mathf.Lerp(pos.y, WaterY + Mathf.Sin(now * 2.2f + pos.x * 0.01f) * 6f, dt * 6f);
                        piece.angle = Mathf.LerpAngle(piece.angle, Mathf.Sin(now * 1.7f + pos.x) * 12f, dt * 2f);
                        alpha = 1f - Ease(1.2f, 3.2f, now - piece.wetSince);
                        break;
                    case PieceState.Sinking:
                        pos += piece.velocity * dt;
                        alpha = 1f - Ease(0f, 0.6f, now - piece.wetSince);
                        break;
                }

                piece.rect.anchoredPosition = pos;
                piece.rect.localRotation = Quaternion.Euler(0f, 0f, piece.angle);
                piece.image.color = new Color(1f, 1f, 1f, alpha);
                if (alpha <= 0.001f)
                {
                    piece.state = PieceState.Gone;
                    piece.rect.gameObject.SetActive(false);
                }
            }
        }

        // Comes up near where the boat broke, rocks to a stop and stays adrift.
        private void Broken(float t)
        {
            float since = t - (impactTime + 1.2f);
            if (since < 0f) return;
            if (!brokenBoat.gameObject.activeSelf)
            {
                brokenBoat.gameObject.SetActive(true);
                brokenBoat.anchorMin = brokenBoat.anchorMax = new Vector2(0.5f, 0.5f);
            }

            float half = sea.rect.width * 0.5f;
            float x = Mathf.Clamp(impactPos.x + 40f, -half + 320f, half - 320f);
            float rise = 1f - Mathf.Pow(1f - Mathf.Clamp01(since / 1.4f), 3f);
            float drift = Now * Mathf.PI * 2f / 4.2f;
            float y = Mathf.Lerp(-220f, 0f, rise) + Mathf.Sin(drift) * 5f * rise;
            float angle = driftTilt + 14f * Mathf.Exp(-since * 1.6f) * Mathf.Cos(since * 4f) + Mathf.Cos(drift) * 1.5f;
            brokenBoat.anchoredPosition = new Vector2(x, y);
            brokenBoat.localRotation = Quaternion.Euler(0f, 0f, angle);
            brokenImage.color = new Color(1f, 1f, 1f, Ease(0f, 0.6f, since));
        }

        private static float Ease(float from, float to, float x)
        {
            float k = Mathf.Clamp01((x - from) / (to - from));
            return k * k * (3f - 2f * k);
        }

        private static float SignedAngle(float degrees)
        {
            degrees %= 360f;
            return degrees > 180f ? degrees - 360f : degrees;
        }
    }
}
