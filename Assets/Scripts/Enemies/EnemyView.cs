using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>
    /// How an enemy looks while it acts: walks or stands (the character's Animator, the same controller as the
    /// players'), faces its target or where it goes (mirroring the drawing, as seen on screen), swings on every hit
    /// (<see cref="EnemyBrain.Attacked"/>), and holds a sword only if its type fights with one.
    /// </summary>
    [RequireComponent(typeof(EnemyBrain))]
    public class EnemyView : MonoBehaviour
    {
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");
        private static readonly int Attack = Animator.StringToHash("Attack");

        [SerializeField] private Animator animator;
        [Tooltip("The drawing that is mirrored to face left or right (it faces the camera).")]
        [SerializeField] private Transform drawing;
        [Tooltip("True if the drawing looks to the right before mirroring.")]
        [SerializeField] private bool artFacesRight = true;
        [Tooltip("The sword in hand, shown for sword fighters only.")]
        [SerializeField] private GameObject sword;
        [Tooltip("Slower than this (units/second) counts as standing.")]
        [SerializeField, Min(0f)] private float walkingSpeed = 0.3f;

        private EnemyBrain brain;
        private Rigidbody body;

        private void Awake()
        {
            brain = GetComponent<EnemyBrain>();
            body = GetComponent<Rigidbody>();
            brain.Attacked += () => animator.SetTrigger(Attack);
        }

        // After a spawner's Initialize: the type is known.
        private void Start()
        {
            if (sword != null) sword.SetActive(brain.Definition != null && brain.Definition.Weapon == EnemyWeapon.Sword);
        }

        private void Update()
        {
            Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
            velocity.y = 0f;
            animator.SetBool(IsMoving, velocity.magnitude > walkingSpeed);

            Vector3 facing = brain.Target != null ? brain.Target.position - transform.position : velocity;
            float side = Vector3.Dot(facing, drawing.right); // right or left as seen on screen
            if (Mathf.Abs(side) < 0.01f) return;

            Vector3 scale = drawing.localScale;
            scale.x = Mathf.Abs(scale.x) * ((side > 0f) == artFacesRight ? 1f : -1f);
            drawing.localScale = scale;
        }
    }
}
