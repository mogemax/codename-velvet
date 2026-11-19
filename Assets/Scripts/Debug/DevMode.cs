using System;
using System.Collections;
using CodenameVelvet.DayNight;
using CodenameVelvet.Spawning;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CodenameVelvet.DebugTools
{
    /// <summary>
    /// Dev mode global (F1). Solo existe en el Editor y en Development Builds: se crea solo al iniciar
    /// y nunca llega a un build final. Mientras está activo:
    /// <list type="bullet">
    /// <item>Cada jugador recibe <see cref="PlayerDevMode"/> (velocidad, invulnerable, recursos, fantasma).</item>
    /// <item>Las SpawnZones se dibujan en pantalla.</item>
    /// <item>F2 salta a la siguiente fase del ciclo día/noche.</item>
    /// <item>Un letrero avisa que está prendido.</item>
    /// </list>
    /// </summary>
    public class DevMode : MonoBehaviour
    {
        private const string OutlineName = "DevMode Outline";
        private static readonly Color OutlineColor = new Color(0.2f, 1f, 0.4f, 0.9f);
        private const float OutlineWidth = 0.25f;

        public static bool IsActive { get; private set; }
        public static event Action<bool> Changed;

        private GUIStyle labelStyle;
        private Material outlineMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            IsActive = false; // por si el Editor no recarga el dominio entre Plays
            Changed = null;
            if (!Debug.isDebugBuild) return;

            var go = new GameObject(nameof(DevMode));
            DontDestroyOnLoad(go);
            go.AddComponent<DevMode>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.f1Key.wasPressedThisFrame)
                SetActive(!IsActive);
            if (IsActive && Keyboard.current.f2Key.wasPressedThisFrame)
                SkipDayNightPhase();
        }

        private static void SkipDayNightPhase()
        {
            DayNightCycle cycle = FindAnyObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SkipPhase();
        }

        private void SetActive(bool active)
        {
            IsActive = active;
            Debug.Log($"[DevMode] {(active ? "ON" : "OFF")}");
            if (active) AttachToPlayers();
            SetZoneOutlines(active);
            Changed?.Invoke(active);
        }

        // Los jugadores los crea el PlayerSpawner en su Start: se espera un frame tras cargar la escena.
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsActive) StartCoroutine(ApplyNextFrame());
        }

        private IEnumerator ApplyNextFrame()
        {
            yield return null;
            AttachToPlayers();
            SetZoneOutlines(true);
        }

        private static void AttachToPlayers()
        {
            foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
                if (!player.TryGetComponent(out PlayerDevMode _))
                    player.gameObject.AddComponent<PlayerDevMode>(); // se aplica solo en su OnEnable
        }

        // ─────────────────────── SpawnZones visibles ───────────────────────

        private void SetZoneOutlines(bool visible)
        {
            foreach (SpawnZone zone in FindObjectsByType<SpawnZone>(FindObjectsSortMode.None))
            {
                Transform existing = zone.transform.Find(OutlineName);
                if (existing != null) existing.gameObject.SetActive(visible);
                else if (visible && zone.TryGetComponent(out BoxCollider box)) CreateOutline(zone.transform, box);
            }
        }

        /// <summary>Contorno de la caja con un solo LineRenderer (16 puntos recorren las 12 aristas).</summary>
        private void CreateOutline(Transform zone, BoxCollider box)
        {
            if (outlineMaterial == null) outlineMaterial = new Material(Shader.Find("Sprites/Default"));

            var go = new GameObject(OutlineName);
            go.transform.SetParent(zone, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = outlineMaterial;
            line.startColor = line.endColor = OutlineColor;
            line.widthMultiplier = OutlineWidth;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 c = box.center, e = box.size * 0.5f;
            Vector3 P(float x, float y, float z) => c + new Vector3(x * e.x, y * e.y, z * e.z);
            Vector3[] points =
            {
                P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), P(-1, -1, -1), // base
                P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), P(-1, 1, -1),      // tapa
                P(-1, 1, 1), P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), P(1, -1, -1), // verticales
            };
            line.positionCount = points.Length;
            line.SetPositions(points);
        }

        private void OnGUI()
        {
            if (!IsActive) return;
            labelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.2f) },
            };
            // Abajo: arriba va el indicador de día/noche.
            GUI.Label(new Rect(0f, Screen.height - 48f, Screen.width, 40f),
                "DEV MODE (F1): fantasma · invulnerable · ×2.5 velocidad · recursos llenos · F2 día/noche", labelStyle);
        }
    }
}
