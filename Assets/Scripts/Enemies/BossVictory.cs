using CodenameVelvet.Core;
using UnityEngine;

namespace CodenameVelvet.Enemies
{
    /// <summary>Al morir este enemigo (el jefe) la partida termina en victoria.</summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class BossVictory : MonoBehaviour
    {
        [SerializeField] private MatchFlow matchFlow;

        private EnemyHealth health;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            if (matchFlow == null)
                Debug.LogWarning($"{name}: BossVictory sin MatchFlow; matarlo no terminará la partida.", this);
        }

        private void OnEnable() => health.Died += HandleDied;
        private void OnDisable() => health.Died -= HandleDied;

        private void HandleDied(EnemyHealth _)
        {
            if (matchFlow != null) matchFlow.Victory();
        }
    }
}
