using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodenameVelvet.Cameras;
using CodenameVelvet.Core.Debugging;
using CodenameVelvet.DayNight;
using CodenameVelvet.Multiplayer;
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
    /// <item>F2 skips to the next day/night phase; F3 toggles free flight.</item>
    /// <item>1-9 teleport every player to the scene's destinations: the spawn, each camera framing zone
    /// (e.g. the boss arena) and each SpawnZone, found automatically in every level.</item>
    /// <item>SpawnZones are drawn on screen and a label lists the keys.</item>
    /// </list>
    /// </summary>
    public class DevMode : MonoBehaviour
    {
        private const string LogCategory = "DevMode";
        private const string OutlineName = "DevMode Outline";
        private static readonly Color OutlineColor = new Color(0.2f, 1f, 0.4f, 0.9f);
        private const float OutlineWidth = 0.25f;
        private const int MaxDestinations = 9;
        private const float TeleportSpread = 2f;
        private const float TeleportLift = 0.5f;
        private const float GroundProbeHeight = 200f;

        private readonly struct Destination
        {
            public Destination(string label, Vector3 position)
            {
                Label = label;
                Position = position;
            }

            public string Label { get; }
            public Vector3 Position { get; }
        }

        public static bool IsActive { get; private set; }
        public static bool IsFlying { get; private set; }
        public static event Action<bool> Changed;
        public static event Action<bool> FlightChanged;

        private readonly List<Destination> destinations = new List<Destination>();
        private GUIStyle labelStyle;
        private Material outlineMaterial;
        private string help = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // The Editor may skip the domain reload between Play sessions.
            IsActive = false;
            IsFlying = false;
            Changed = null;
            FlightChanged = null;
            if (!Debug.isDebugBuild) return;

            var go = new GameObject(nameof(DevMode));
            DontDestroyOnLoad(go);
            go.AddComponent<DevMode>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) SetActive(!IsActive);
            if (!IsActive) return;

            if (keyboard.f2Key.wasPressedThisFrame) SkipDayNightPhase();
            if (keyboard.f3Key.wasPressedThisFrame) SetFlying(!IsFlying);
            for (int i = 0; i < destinations.Count; i++)
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame) TeleportAll(destinations[i]);
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
            if (active)
            {
                AttachToPlayers();
                RefreshDestinations();
            }
            SetZoneOutlines(active);
            Changed?.Invoke(active);
            if (!active) SetFlying(false);
        }

        private void SetFlying(bool flying)
        {
            if (IsFlying == flying) return;
            IsFlying = flying;
            VelvetLog.Info(LogCategory, this, flying ? "Flight ON" : "Flight OFF");
            FlightChanged?.Invoke(flying);
            RefreshHelp();
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
            RefreshDestinations();
        }

        private static void AttachToPlayers()
        {
            foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
                if (!player.TryGetComponent(out PlayerDevMode _))
                    player.gameObject.AddComponent<PlayerDevMode>(); // applies itself in OnEnable
        }

        private void RefreshDestinations()
        {
            destinations.Clear();

            PlayerSpawner spawner = FindAnyObjectByType<PlayerSpawner>();
            if (spawner != null) destinations.Add(new Destination("Spawn", spawner.FirstSpawnPoint.position));

            IEnumerable<BoxCollider> areas = FindObjectsByType<CameraFramingZone>(FindObjectsSortMode.None)
                .Select(zone => zone.GetComponent<BoxCollider>())
                .Concat(FindObjectsByType<SpawnZone>(FindObjectsSortMode.None).Select(zone => zone.GetComponent<BoxCollider>()))
                .Where(box => box != null);
            foreach (BoxCollider area in areas.OrderBy(area => area.GetComponent<CameraFramingZone>() == null).ThenBy(area => area.name))
                destinations.Add(new Destination(area.name, GroundBelow(area.bounds.center)));

            if (destinations.Count > MaxDestinations) destinations.RemoveRange(MaxDestinations, destinations.Count - MaxDestinations);
            RefreshHelp();
        }

        private static Vector3 GroundBelow(Vector3 point)
        {
            return Physics.Raycast(point + Vector3.up * GroundProbeHeight, Vector3.down, out RaycastHit hit,
                GroundProbeHeight * 2f, LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore)
                ? hit.point
                : point;
        }

        /// <summary>Moves every player around the destination (spread out so nobody overlaps) and snaps the cameras.</summary>
        private void TeleportAll(Destination destination)
        {
            PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.InstanceID);
            for (int i = 0; i < players.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / Mathf.Max(1, players.Length);
                Vector3 offset = players.Length > 1 ? new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * TeleportSpread : Vector3.zero;
                Vector3 position = destination.Position + offset + Vector3.up * TeleportLift;

                players[i].CancelDash();
                players[i].transform.position = position;
                if (players[i].TryGetComponent(out Rigidbody rb))
                {
                    rb.position = position;
                    rb.linearVelocity = Vector3.zero;
                }
            }

            foreach (CameraFollow follow in FindObjectsByType<CameraFollow>(FindObjectsSortMode.None))
                follow.SnapToTarget();
            VelvetLog.Info(LogCategory, this, $"Teleported to {destination.Label}");
        }

        private void RefreshHelp()
        {
            var text = new StringBuilder();
            for (int i = 0; i < destinations.Count; i++)
                text.Append(i == 0 ? "" : "   ").Append(i + 1).Append(' ').Append(destinations[i].Label);
            text.Append('\n').Append("DEV MODE (F1): ghost · invulnerable · ×2.5 speed · full inventory · F2 day/night · F3 fly ")
                .Append(IsFlying ? "ON (Space/Ctrl up/down)" : "OFF");
            help = text.ToString();
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
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.2f) },
            };
            // Bottom of the screen: the day/night indicator sits at the top.
            GUI.Label(new Rect(0f, Screen.height - 76f, Screen.width, 68f), help, labelStyle);
        }
    }
}
