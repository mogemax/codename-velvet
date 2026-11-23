using System;
using System.Collections;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.DayNight;
using CodenameVelvet.Spawning;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CodenameVelvet.DebugTools
{
    /// <summary>
    /// Global dev mode toggled with F1. Only exists in the Editor and development builds: it creates itself
    /// on startup and never ships in a release build. While active:
    /// <list type="bullet">
    /// <item>Every player gets <see cref="PlayerDevMode"/> (speed, invulnerable, full inventory, ghost).</item>
    /// <item>SpawnZones are drawn on screen.</item>
    /// <item>F2 skips to the next day/night phase.</item>
    /// <item>A label shows that it is on.</item>
    /// </list>
    /// </summary>
    public class DevMode : MonoBehaviour
    {
        private const string LogCategory = "DevMode";
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
            IsActive = false; // the Editor may skip the domain reload between Play sessions
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
            VelvetLog.Info(LogCategory, this, active ? "ON" : "OFF");
            if (active) AttachToPlayers();
            SetZoneOutlines(active);
            Changed?.Invoke(active);
        }

        // PlayerSpawner creates the players in its Start, so wait one frame after the scene loads.
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
                    player.gameObject.AddComponent<PlayerDevMode>(); // applies itself in OnEnable
        }

        private void SetZoneOutlines(bool visible)
        {
            foreach (SpawnZone zone in FindObjectsByType<SpawnZone>(FindObjectsSortMode.None))
            {
                Transform existing = zone.transform.Find(OutlineName);
                if (existing != null) existing.gameObject.SetActive(visible);
                else if (visible && zone.TryGetComponent(out BoxCollider box)) CreateOutline(zone.transform, box);
            }
        }

        /// <summary>Box outline drawn with a single LineRenderer (16 points cover the 12 edges).</summary>
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
                P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), P(-1, -1, -1), // bottom
                P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), P(-1, 1, -1),      // top
                P(-1, 1, 1), P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), P(1, -1, -1), // verticals
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
            // Bottom of the screen: the day/night indicator sits at the top.
            GUI.Label(new Rect(0f, Screen.height - 48f, Screen.width, 40f),
                "DEV MODE (F1): ghost · invulnerable · ×2.5 speed · full inventory · F2 day/night", labelStyle);
        }
    }
}
