using _Game.ScriptableObjects.Entities;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Game.AI
{
    /// <summary>
    /// Per-creature-type data: stats, movement, and animator override controller.
    /// Replaces AIConfigSO as the single data source for enemy behaviour.
    /// All tunable gameplay values live here — never hardcode in EnemyBrain or EnemyHealth.
    /// Enemy Creature System: Initial implementation.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/AI/Enemy Type", fileName = "EnemyType_")]
    public class MonsterEntity : Entity
    {
        
        [Header("Monster properties")]
        [Header("Idle")]
        [SerializeField] private float _idleWanderRadius = 5f;

        public override void ExecuteIdle(NavMeshAgent agent, Vector3 origin, ref float waitTimer)
        {
            if (agent.remainingDistance > WaypointArrivalThreshold) return;

            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                Vector3 randomOffset = Random.insideUnitSphere * _idleWanderRadius;
                randomOffset.y = 0f;
                Vector3 candidate = origin + randomOffset;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, _idleWanderRadius, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                    waitTimer = PatrolWaitTime;
                }
                // If SamplePosition fails: stay put, waitTimer stays <= 0, retry next frame
            }
            else
            {
                agent.isStopped = true;
            }
        }
    }
}
