using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    public class SceneLoader : MonoBehaviour
    {
        private const string TAG = "[Scene]";

        [SerializeField] private string _startupScene = "StartingTown";

        private void Start()
        {
            if (!SceneManager.GetSceneByName(_startupScene).isLoaded)
                StartCoroutine(LoadRegionCoroutine(_startupScene));
            else
                StartCoroutine(SpawnPlayerCoroutine()); // Scene already open in editor — skip load, just spawn
        }

        public void LoadRegion(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                GameLog.Error(TAG, "LoadRegion called with empty scene name");
                return;
            }
            StartCoroutine(LoadRegionCoroutine(sceneName));
        }

        public void UnloadRegion(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                GameLog.Error(TAG, "UnloadRegion called with empty scene name");
                return;
            }
            StartCoroutine(UnloadRegionCoroutine(sceneName));
        }

        private IEnumerator LoadRegionCoroutine(string sceneName)
        {
            GameLog.Info(TAG, $"Loading region: {sceneName}");
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            yield return op;
            GameLog.Info(TAG, $"Region loaded: {sceneName}");

            SpawnPlayerAtPoint();
        }

        private IEnumerator SpawnPlayerCoroutine()
        {
            yield return null; // Wait one frame — ensures all Start() on scene objects have run
            SpawnPlayerAtPoint();
        }

        private void SpawnPlayerAtPoint()
        {
            var spawnPoint = GameObject.Find("PlayerSpawnPoint");
            if (spawnPoint == null)
            {
                GameLog.Warn(TAG, "PlayerSpawnPoint not found in loaded scenes");
                return;
            }

            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO == null)
            {
                GameLog.Warn(TAG, "No GameObject tagged 'Player' found — cannot spawn");
                return;
            }

            playerGO.transform.position = spawnPoint.transform.position;
            GameLog.Info(TAG, $"Player spawned at {spawnPoint.transform.position}");
        }

        private IEnumerator UnloadRegionCoroutine(string sceneName)
        {
            GameLog.Info(TAG, $"Unloading region: {sceneName}");
            var op = SceneManager.UnloadSceneAsync(sceneName);
            yield return op;
            GameLog.Info(TAG, $"Region unloaded: {sceneName}");
        }
    }
}
