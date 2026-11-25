using System.Collections.Generic;
using CodenameVelvet.Combat;
using CodenameVelvet.Core.Debugging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CodenameVelvet.Player
{
    /// <summary>
    /// The bow this player may carry (picked up from the ground, kept on death and between levels). The "Shoot"
    /// action (this object's PlayerInput, Send Messages) fires an <see cref="Arrow"/> at the nearest enemy within
    /// <see cref="aimRange"/>, or where the player faces; arrows are endless, one every <see cref="cooldown"/>
    /// seconds. The bow shows in hand only while shooting, in place of the sword.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerBow : MonoBehaviour
    {
        private const string LogCategory = "Player";

        [Header("Shot")]
        [SerializeField] private Arrow arrowPrefab;
        [SerializeField, Min(1)] private int damage = 1;
        [Tooltip("Seconds between shots.")]
        [SerializeField, Min(0f)] private float cooldown = 2f;
        [Tooltip("Enemies this close (centre to centre, on the ground plane) pull the aim.")]
        [SerializeField, Min(0f)] private float aimRange = 14f;
        [Tooltip("Height above the feet the arrow flies at.")]
        [SerializeField, Min(0f)] private float shotHeight = 2f;
        [Tooltip("Layers that may hold enemies to aim at.")]
        [SerializeField] private LayerMask aimMask = ~0;

        [Header("Look")]
        [Tooltip("The bow drawing shown in hand while shooting.")]
        [SerializeField] private Sprite bowSprite;
        [SerializeField, Min(0f)] private float showSeconds = 0.35f;
        [Tooltip("Height of the bow drawing in world units (the player is about 5).")]
        [SerializeField, Min(0.1f)] private float heldSize = 3f;
        [Tooltip("Where the bow is held, in world units: sideways towards the shot, and up from the feet.")]
        [SerializeField] private Vector2 heldOffset = new Vector2(1.2f, 2.2f);

        private readonly Collider[] nearby = new Collider[32];
        private readonly List<Vector3> enemyPositions = new List<Vector3>();
        private readonly List<float> enemyDistances = new List<float>();

        private PlayerMovement movement;
        private PlayerHealth health;
        private PlayerSword sword;
        private SpriteRenderer heldBow;
        private Vector2 shotAim;
        private float nextShotTime;
        private float hideTime;

        public bool HasBow { get; private set; }

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

        /// <summary>Fires an arrow if the player can (has the bow, is standing, the bow is ready). False otherwise.</summary>
        public bool TryShoot()
        {
            if (!CanShoot())
                return false;

            nextShotTime = Time.time + cooldown;
            shotAim = ChooseAim();

            Vector3 start = transform.position + Vector3.up * shotHeight;
            Arrow arrow = Instantiate(arrowPrefab, start, Quaternion.identity);
            arrow.Launch(shotAim, damage);
            ShowBow();

            VelvetLog.Info(LogCategory, this, $"Shot an arrow towards {shotAim}");
            return true;
        }

        private bool CanShoot() =>
            HasBow && arrowPrefab != null
            && (health == null || health.IsAlive)
            && !movement.IsDashing && !movement.IsClimbing
            && Time.time >= nextShotTime;

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
        /// The bow drawing, under the character's drawing (it faces the camera with it) and in front of all of it,
        /// with the sword's material; hidden until a shot.
        /// </summary>
        private void CreateHeldBow()
        {
            Transform root = movement.SpriteRoot;
            if (bowSprite == null || root == null)
                return;

            SpriteRenderer[] parts = root.GetComponentsInChildren<SpriteRenderer>(true);
            var bow = new GameObject("Bow");
            bow.transform.SetParent(root, false);
            float scale = heldSize / bowSprite.bounds.size.y;
            Vector3 rootScale = root.lossyScale;
            bow.transform.localScale = new Vector3(Mathf.Abs(scale / rootScale.x), Mathf.Abs(scale / rootScale.y), 1f);

            heldBow = bow.AddComponent<SpriteRenderer>();
            heldBow.sprite = bowSprite;
            heldBow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (parts.Length > 0)
            {
                SpriteRenderer reference = sword != null && sword.Drawing != null ? sword.Drawing : parts[0];
                heldBow.sharedMaterial = reference.sharedMaterial;
                heldBow.sortingLayerID = reference.sortingLayerID;
                int front = int.MinValue;
                foreach (SpriteRenderer part in parts) front = Mathf.Max(front, part.sortingOrder);
                heldBow.sortingOrder = front + 1;
            }
            bow.SetActive(false);
        }

        private void ShowBow()
        {
            if (heldBow == null)
                return;

            hideTime = Time.time + showSeconds;
            heldBow.gameObject.SetActive(true);
            if (sword != null) sword.PutAway(true);
        }

        private void LateUpdate()
        {
            if (heldBow == null || !heldBow.gameObject.activeSelf)
                return;

            if (Time.time >= hideTime || (health != null && !health.IsAlive))
            {
                heldBow.gameObject.SetActive(false);
                if (sword != null) sword.PutAway(false);
                return;
            }

            // Held out on the side of the shot as seen on screen, upright, its belly towards the shot. The drawing
            // faces left, and the root may be mirrored (the character facing the other way).
            Vector3 onScreen = Quaternion.Euler(0f, -movement.ViewYaw, 0f) * new Vector3(shotAim.x, 0f, shotAim.y);
            float side = onScreen.x >= 0f ? 1f : -1f;
            Vector3 screenRight = Quaternion.Euler(0f, movement.ViewYaw, 0f) * Vector3.right;
            Transform bow = heldBow.transform;
            bow.position = transform.position + screenRight * (side * heldOffset.x) + Vector3.up * heldOffset.y;
            bow.localRotation = Quaternion.identity;
            heldBow.flipX = (side > 0f) != (bow.lossyScale.x < 0f);
        }
    }
}
