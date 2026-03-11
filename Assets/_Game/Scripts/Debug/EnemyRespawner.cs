using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.DevTools
{
    /// <summary>
    /// Test scaffolding — respawns enemies after a delay for combat testing.
    /// Attach to any always-active GameObject (e.g. ProgressionSystem).
    /// This component will be superseded by Story 4-5 (no-enemy-respawn design).
    /// </summary>
    public class EnemyRespawner : MonoBehaviour
    {
        private const string TAG = "[EnemyRespawner]";

        [SerializeField] private GameObject[] _enemies;
        [Tooltip("Seconds to wait before respawning a dead enemy.")]
        [SerializeField] private float _respawnDelay = 5f;

        private WaitForSeconds _wait;
        private readonly HashSet<GameObject> _pendingRespawn = new HashSet<GameObject>();

        private void Awake()
        {
            _wait = new WaitForSeconds(_respawnDelay);
        }

        private void OnDisable()
        {
            _pendingRespawn.Clear(); // StopAllCoroutines() is called automatically by Unity
        }

        private void Update()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy != null && !enemy.activeInHierarchy && !_pendingRespawn.Contains(enemy))
                {
                    _pendingRespawn.Add(enemy);
                    StartCoroutine(RespawnAfterDelay(enemy));
                }
            }
        }

        private IEnumerator RespawnAfterDelay(GameObject enemy)
        {
            yield return _wait;
            enemy.SetActive(true);
            _pendingRespawn.Remove(enemy);
            GameLog.Info(TAG, $"{enemy.name} respawned after {_respawnDelay}s");
        }
    }
}
