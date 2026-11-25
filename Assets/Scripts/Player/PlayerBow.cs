using System.Collections.Generic;
using CodenameVelvet.Combat;
using CodenameVelvet.Core.Debugging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// The bow this player may carry (picked up from the ground, kept on death and between levels). The "Shoot"
    /// action (this object's PlayerInput, Send Messages) draws the bow for <see cref="drawSeconds"/> and, when the
    /// string is let go, fires an <see cref="Arrow"/> at the nearest enemy within <see cref="aimRange"/>, or where the
    /// player faces. Arrows are endless, one every <see cref="cooldown"/> seconds counted from the release; a draw
    /// cut short (the player dies, dashes or climbs) fires nothing and costs nothing. The bow shows in hand only
    /// while shooting, in place of the sword.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerBow : MonoBehaviour
    {
        private const string LogCategory = "Player";

        private enum State { Idle, Drawing, Released }

        [Header("Shot")]
        [SerializeField] private Arrow arrowPrefab;
        [SerializeField, Min(1)] private int damage = 1;
        [Tooltip("Seconds between shots, from one release to the next draw.")]
        [SerializeField, Min(0f)] private float cooldown = 2f;
        [Tooltip("Seconds the string is pulled back before the arrow leaves.")]
        [SerializeField, Min(0.01f)] private float drawSeconds = 0.25f;
        [Tooltip("Enemies this close (centre to centre, on the ground plane) pull the aim.")]
        [SerializeField, Min(0f)] private float aimRange = 14f;
        [Tooltip("Height above the feet the arrow flies at.")]
        [SerializeField, Min(0f)] private float shotHeight = 2f;
        [Tooltip("Layers that may hold enemies to aim at.")]
        [SerializeField] private LayerMask aimMask = ~0;

        [Header("Look")]
        [Tooltip("The bow in pieces (grip, limbs, string), shown in hand while shooting.")]
        [SerializeField] private HeldBowView heldBowPrefab;
        [Tooltip("Seconds the bow stays in hand after the release.")]
        [SerializeField, Min(0f)] private float showAfterRelease = 0.3f;
        [Tooltip("Seconds the string takes to snap back after the release.")]
        [SerializeField, Min(0.01f)] private float snapSeconds = 0.08f;
        [Tooltip("Height of the bow drawing in world units (the player is about 5).")]
        [SerializeField, Min(0.1f)] private float heldSize = 4.5f;
        [Tooltip("Where the bow is held, in world units: sideways towards the shot, and up from the feet.")]
        [SerializeField] private Vector2 heldOffset = new Vector2(1.2f, 2.2f);

        private readonly Collider[] nearby = new Collider[32];
        private readonly List<Vector3> enemyPositions = new List<Vector3>();
        private readonly List<float> enemyDistances = new List<float>();

        private PlayerMovement movement;
        private PlayerHealth health;
        private PlayerSword sword;
        private HeldBowView heldBow;
        private State state;
        private Vector2 shotAim;
        private float nextShotTime;
        private float stateStart;

        public bool HasBow { get; private set; }

        /// <summary>Between the press and the release.</summary>
        public bool IsDrawing => state == State.Drawing;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            health = GetComponent<PlayerHealth>();
            sword = GetComponent<PlayerSword>();
            CreateHeldBow();
        }

        public void Equip() => HasBow = true;

        private void OnShoot(InputValue value)
        {
            if (value.isPressed) TryShoot();
        }

        /// <summary>Starts drawing if the player can (has the bow, is standing, the bow is ready). False otherwise.</summary>
        public bool TryShoot()
        {
            if (!CanShoot())
                return false;

            shotAim = ChooseAim();
            Enter(State.Drawing);
            return true;
        }

        private bool CanShoot() =>
            HasBow && arrowPrefab != null && state != State.Drawing
            && CanKeepDrawing()
            && Time.time >= nextShotTime;

        private bool CanKeepDrawing() =>
            (health == null || health.IsAlive) && !movement.IsDashing && !movement.IsClimbing;

        private void Update()
        {
            float elapsed = Time.time - stateStart;
            switch (state)
            {
                case State.Drawing when !CanKeepDrawing():
                    Enter(State.Idle); // cut short: nothing fired, nothing spent
                    break;
                case State.Drawing when elapsed >= drawSeconds:
                    Release();
                    break;
                case State.Released when elapsed >= Mathf.Max(showAfterRelease, snapSeconds):
                    Enter(State.Idle);
                    break;
            }
        }

        private void Release()
        {
            shotAim = ChooseAim(); // the target may have moved during the draw
            nextShotTime = Time.time + cooldown;
            Arrow arrow = Instantiate(arrowPrefab, transform.position + Vector3.up * shotHeight, Quaternion.identity);
            arrow.Launch(shotAim, damage);
            Enter(State.Released);
            VelvetLog.Info(LogCategory, this, $"Shot an arrow towards {shotAim}");
        }

        private void Enter(State next)
        {
            state = next;
            stateStart = Time.time;
            if (heldBow == null)
                return;

            bool shown = next != State.Idle;
            heldBow.gameObject.SetActive(shown);
            heldBow.ShowArrow(next == State.Drawing);
            if (sword != null) sword.PutAway(shown);
        }

        /// <summary>The nearest living enemy within <see cref="aimRange"/>, otherwise where the player faces.</summary>
        private Vector2 ChooseAim()
        {
            Vector3 origin = transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, aimRange, nearby, aimMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (Arrow.IsEnemy(nearby[i], out IDamageable _, out Component owner) && !enemyPositions.Contains(owner.transform.position))
                {
                    Vector3 position = owner.transform.position;
                    enemyPositions.Add(position);
                    enemyDistances.Add(new Vector2(position.x - origin.x, position.z - origin.z).magnitude);
                }
                nearby[i] = null;
            }

            Vector2 aim = MeleeAim.ChooseAimDirection(origin, movement.FacingDirection, enemyPositions, enemyDistances, aimRange);
            enemyPositions.Clear();
            enemyDistances.Clear();
            return aim;
        }

        // ---- Bow in hand ---------------------------------------------------------------------------------------

        /// <summary>
        /// The bow, under the character's drawing (it faces the camera with it) and in front of all of it; hidden
        /// until a shot.
        /// </summary>
        private void CreateHeldBow()
        {
            Transform root = movement.SpriteRoot;
            if (heldBowPrefab == null || root == null)
                return;

            heldBow = Instantiate(heldBowPrefab, root, false);
            heldBow.name = "Bow";
            float scale = heldSize / BowHeight(heldBowPrefab);
            Vector3 rootScale = root.lossyScale;
            heldBow.transform.localScale = new Vector3(Mathf.Abs(scale / rootScale.x), Mathf.Abs(scale / rootScale.y), 1f);

            int front = int.MinValue, layer = 0;
            foreach (SpriteRenderer part in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (part.transform.IsChildOf(heldBow.transform)) continue;
                front = Mathf.Max(front, part.sortingOrder);
                layer = part.sortingLayerID;
            }
            if (front != int.MinValue) heldBow.SetSorting(layer, front + 1);
            heldBow.gameObject.SetActive(false);
        }

        private static float BowHeight(HeldBowView bow)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (SpriteRenderer part in bow.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Bounds local = part.sprite.bounds;
                Vector3 min = bow.transform.InverseTransformPoint(part.transform.TransformPoint(local.min));
                Vector3 max = bow.transform.InverseTransformPoint(part.transform.TransformPoint(local.max));
                if (first) { bounds = new Bounds(min, Vector3.zero); first = false; }
                bounds.Encapsulate(min);
                bounds.Encapsulate(max);
            }
            return Mathf.Max(bounds.size.y, 0.01f);
        }

        private void LateUpdate()
        {
            if (heldBow == null || state == State.Idle)
                return;

            float elapsed = Time.time - stateStart;
            heldBow.SetPull(state == State.Drawing ? elapsed / drawSeconds : 1f - elapsed / snapSeconds);

            // Held out on the side of the shot as seen on screen, upright, its belly towards the shot. The drawing
            // shoots left, and the root may be mirrored (the character facing the other way).
            Vector3 onScreen = Quaternion.Euler(0f, -movement.ViewYaw, 0f) * new Vector3(shotAim.x, 0f, shotAim.y);
            float side = onScreen.x >= 0f ? 1f : -1f;
            Vector3 screenRight = Quaternion.Euler(0f, movement.ViewYaw, 0f) * Vector3.right;
            Transform bow = heldBow.transform;
            bow.position = transform.position + screenRight * (side * heldOffset.x) + Vector3.up * heldOffset.y;
            bow.localRotation = Quaternion.identity;

            bool parentMirrored = bow.parent.lossyScale.x < 0f;
            bool mirror = (side > 0f) != parentMirrored;
            Vector3 scale = bow.localScale;
            scale.x = Mathf.Abs(scale.x) * (mirror ? -1f : 1f);
            bow.localScale = scale;
        }
    }
}
