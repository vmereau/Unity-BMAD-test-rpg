using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// Loads region scenes additively next to Core.unity. Raises <see cref="RegionUnloading"/> before a region
    /// unloads and <see cref="RegionLoaded"/> one frame after one loads (so every Start() — incl. PersistentID
    /// hiding killed entities — has run). The save system listens to both (same Core system → C# events).
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        private const string TAG = "[Scene]";

        [SerializeField] private string _startupScene = "StartingTown";

        private int _busyCount;

        /// <summary>Region scene currently loaded next to Core (null before the first load completes).</summary>
        public string CurrentRegion { get; private set; }

        /// <summary>True while any load / unload coroutine runs.</summary>
        public bool IsBusy => _busyCount > 0;

        public string StartupScene => _startupScene;

        /// <summary>Raised before a region scene starts unloading (its objects are still alive).</summary>
        public event Action<string> RegionUnloading;

        /// <summary>(sceneName, isStartup) — raised one frame after a region finished loading.</summary>
        public event Action<string, bool> RegionLoaded;

        private void Start()
        {
            if (!SceneManager.GetSceneByName(_startupScene).isLoaded)
                StartCoroutine(LoadRegionCoroutine(_startupScene, isStartup: true, spawnPlayer: true, onDone: null));
            else
                StartCoroutine(StartupAlreadyLoadedCoroutine()); // Scene already open in editor — skip load
        }

        /// <summary>True when the scene is in the build settings and can be loaded.</summary>
        public static bool CanLoadRegion(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

        public void LoadRegion(string sceneName)
        {
            if (!CanLoadRegion(sceneName))
            {
                GameLog.Error(TAG, $"LoadRegion: '{sceneName}' is empty or not in the build settings");
                return;
            }
            StartCoroutine(LoadRegionCoroutine(sceneName, isStartup: false, spawnPlayer: true, onDone: null));
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

        /// <summary>
        /// Save/load: unloads every scene except Core, loads <paramref name="sceneName"/> additively, raises
        /// <see cref="RegionLoaded"/>(sceneName, false) and then invokes <paramref name="onDone"/>(true). Does not move
        /// the player — the caller places it. If the load fails mid-way, <paramref name="onDone"/>(false) is invoked
        /// so the caller can recover. Returns false (and changes nothing) when the scene can't be loaded.
        /// </summary>
        public bool ReloadRegion(string sceneName, Action<bool> onDone)
        {
            if (!CanLoadRegion(sceneName))
            {
                GameLog.Error(TAG, $"ReloadRegion: '{sceneName}' is empty or not in the build settings");
                return false;
            }
            StartCoroutine(ReloadRegionCoroutine(sceneName, onDone));
            return true;
        }

        // All waits are `yield return op` / `yield return null` — they keep working at Time.timeScale = 0.
        private IEnumerator ReloadRegionCoroutine(string sceneName, Action<bool> onDone)
        {
            _busyCount++;
            try
            {
                var toUnload = new List<string>();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded && scene != gameObject.scene) toUnload.Add(scene.name);
                }
                foreach (var name in toUnload)
                    yield return UnloadRegionCoroutine(name);

                yield return LoadRegionCoroutine(sceneName, isStartup: false, spawnPlayer: false, onDone);
            }
            finally
            {
                _busyCount--;
            }
        }

        private IEnumerator LoadRegionCoroutine(string sceneName, bool isStartup, bool spawnPlayer, Action<bool> onDone)
        {
            _busyCount++;
            try
            {
                GameLog.Info(TAG, $"Loading region: {sceneName}");
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op == null)
                {
                    GameLog.Error(TAG, $"LoadSceneAsync returned null for '{sceneName}'");
                    onDone?.Invoke(false);
                    yield break;
                }
                yield return op;
                yield return null; // one frame — every Start() in the new scene has run
                GameLog.Info(TAG, $"Region loaded: {sceneName}");

                if (spawnPlayer) SpawnPlayerAtPoint();
                CurrentRegion = sceneName;
                RegionLoaded?.Invoke(sceneName, isStartup);
                onDone?.Invoke(true);
            }
            finally
            {
                _busyCount--;
            }
        }

        private IEnumerator StartupAlreadyLoadedCoroutine()
        {
            _busyCount++;
            try
            {
                yield return null; // Wait one frame — ensures all Start() on scene objects have run
                SpawnPlayerAtPoint();
                CurrentRegion = _startupScene;
                RegionLoaded?.Invoke(_startupScene, true);
            }
            finally
            {
                _busyCount--;
            }
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
            _busyCount++;
            try
            {
                RegionUnloading?.Invoke(sceneName);
                GameLog.Info(TAG, $"Unloading region: {sceneName}");
                var op = SceneManager.UnloadSceneAsync(sceneName);
                if (op == null)
                {
                    GameLog.Error(TAG, $"UnloadSceneAsync returned null for '{sceneName}'");
                    yield break;
                }
                yield return op;
                if (CurrentRegion == sceneName) CurrentRegion = null;
                GameLog.Info(TAG, $"Region unloaded: {sceneName}");
            }
            finally
            {
                _busyCount--;
            }
        }
    }
}
