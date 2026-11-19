using CodenameVelvet.Core;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>Ends the match in victory when this enemy (the boss) dies.</summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class BossVictory : MonoBehaviour
    {
        [SerializeField] private MatchFlow matchFlow;

        private EnemyHealth health;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            if (matchFlow == null)
                Debug.LogWarning($"{name}: BossVictory has no MatchFlow; killing it will not end the match.", this);
        }

        private void OnEnable() => health.Died += HandleDied;
        private void OnDisable() => health.Died -= HandleDied;

        private void HandleDied(EnemyHealth _)
        {
            if (matchFlow != null) matchFlow.Victory();
        }
    }
}
