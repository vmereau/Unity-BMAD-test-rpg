using System;
using System.Collections.Generic;
using Game.Inventory;
using Game.Player;
using Game.Quest;
using Game.UI;
using Game.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// Save / load orchestrator singleton (on the SaveSystem GameObject in Core.unity).
    /// Keeps the object state of every visited region in memory (captured when a region unloads and on save),
    /// writes / reads slot files through <see cref="SaveFileStore"/>, and loads by reloading the saved region:
    /// facts → region reload → objects (RegionLoaded) → player. Handles F5 / F9, autosaves (region transition,
    /// quest completion) and the in-memory start-of-session snapshot used by Restart.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        private const string TAG = "[Save]";
        private const string NEW_GAME_SLOT = "newgame";

        public static SaveSystem Instance { get; private set; }

        [SerializeField] private SceneLoader _sceneLoader;
        [SerializeField] private ItemCatalogSO _catalog;

        // TODO(save-load prototype exception): direct cross-system refs, prescribed by the save/load spec —
        // the orchestrator needs synchronous capture / restore and gate queries that events can't provide.
        [SerializeField] private PlayerSaveAdapter _player;
        [SerializeField] private PlayerStateManager _playerState;
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private ContainerUI _containerUI;
        [SerializeField] private QuestEventsManager _questEvents;
        [SerializeField] private ConfirmDialogUI _confirmDialog; // F9 is ignored while a Game Menu confirm is open

        [Header("Event Channels")]
        [SerializeField] private GameEventSO_Quest _onQuestCompleted;
        [SerializeField] private GameEventSO_String _onSaveNotification;
        [SerializeField] private GameEventSO_Void _onLoadStarted;
        [SerializeField] private GameEventSO_Void _onLoadFinished;

        // In-memory session state — survives region transitions, replaced on load.
        private Dictionary<string, ObjectSaveData> _objectStates = new Dictionary<string, ObjectSaveData>();
        private HashSet<string> _consumed = new HashSet<string>();
        private List<DroppedItemSaveData> _dropped = new List<DroppedItemSaveData>();
        private SaveData _newGameSnapshot;
        private bool _autosavePending;

        private InputSystem_Actions _input;

        public ItemCatalogSO Catalog => _catalog;
        public bool IsLoading { get; private set; }
        public float PlaytimeSeconds { get; private set; }
        public bool HasNewGameSnapshot => _newGameSnapshot != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                GameLog.Warn(TAG, "Duplicate SaveSystem detected — destroying new instance");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_catalog == null) GameLog.Error(TAG, "ItemCatalog not assigned — saved items cannot be restored");
            if (_sceneLoader == null) GameLog.Error(TAG, "SceneLoader not assigned — saving and loading are disabled");
            if (_player == null) GameLog.Error(TAG, "PlayerSaveAdapter not assigned — saving and loading are disabled");
            if (_onSaveNotification == null) GameLog.Warn(TAG, "OnSaveNotification not assigned — save feedback will only be logged");

            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            if (Instance != this || _input == null) return; // duplicate being destroyed
            if (_sceneLoader != null)
            {
                _sceneLoader.RegionUnloading += HandleRegionUnloading;
                _sceneLoader.RegionLoaded += HandleRegionLoaded;
            }
            _onQuestCompleted?.AddListener(HandleQuestCompleted);

            // Own action instance: only these two actions are enabled here, and menus / dialogue disabling their
            // own Player map can't silence them (F9 must work on the death screen).
            _input.Player.QuickSave.performed += HandleQuickSave;
            _input.Player.QuickLoad.performed += HandleQuickLoad;
            _input.Player.QuickSave.Enable();
            _input.Player.QuickLoad.Enable();
        }

        private void OnDisable()
        {
            if (_input == null) return; // Awake may disable before OnEnable runs
            if (_sceneLoader != null)
            {
                _sceneLoader.RegionUnloading -= HandleRegionUnloading;
                _sceneLoader.RegionLoaded -= HandleRegionLoaded;
            }
            _onQuestCompleted?.RemoveListener(HandleQuestCompleted);

            _input.Player.QuickSave.performed -= HandleQuickSave;
            _input.Player.QuickLoad.performed -= HandleQuickLoad;
            _input.Player.QuickSave.Disable();
            _input.Player.QuickLoad.Disable();
        }

        private void OnDestroy()
        {
            _input?.Dispose();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (IsLoading) return;
            if (Time.timeScale > 0f) PlaytimeSeconds += Time.unscaledDeltaTime;

            // Quests usually complete inside dialogue — the autosave runs as soon as saving is allowed again.
            if (_autosavePending && CanSave(out _))
            {
                _autosavePending = false;
                Save(GameConstants.SAVE_SLOT_AUTO);
            }
        }

        // ── Save ──────────────────────────────────────────────────────────────

        /// <summary>False (with a player-facing reason) while dead, in dialogue / trade, looting, or loading.</summary>
        public bool CanSave(out string reason)
        {
            reason = null;
            if (_sceneLoader == null || _player == null) reason = "Saving is unavailable";
            else if (IsLoading || _sceneLoader.IsBusy) reason = "Can't save while loading";
            else if (string.IsNullOrEmpty(_sceneLoader.CurrentRegion)) reason = "Can't save right now";
            else if ((_playerHealth != null && _playerHealth.IsDead) || (_playerState != null && _playerState.IsDead))
                reason = "Can't save while dead";
            else if (_playerState != null && _playerState.IsInDialogue) reason = "Can't save during a conversation";
            else if (_containerUI != null && _containerUI.IsOpen) reason = "Can't save while looting";
            return reason == null;
        }

        /// <summary>Writes the current game to a slot. Returns true on success; raises a notification either way.</summary>
        public bool Save(string slotId)
        {
            if (!CanSave(out var reason))
            {
                Notify(reason);
                return false;
            }

            var data = BuildSaveData(slotId);
            if (!SaveFileStore.TryWrite(data, out var error))
            {
                Notify(error);
                return false;
            }

            Notify(slotId == GameConstants.SAVE_SLOT_QUICK ? "Quicksave"
                 : slotId == GameConstants.SAVE_SLOT_AUTO ? "Autosave"
                 : "Game saved");
            return true;
        }

        /// <summary>Captures the live region, then snapshots facts, player and every region's object state.</summary>
        private SaveData BuildSaveData(string slotId)
        {
            string region = _sceneLoader.CurrentRegion;
            CaptureRegion(region);

            var player = _player.Capture();
            return new SaveData
            {
                version = GameConstants.SAVE_FORMAT_VERSION,
                meta = new SaveMeta
                {
                    slotId = slotId,
                    savedAtUtc = DateTime.UtcNow.ToString("o"),
                    region = region,
                    playerLevel = player.level,
                    playtimeSeconds = PlaytimeSeconds
                },
                region = region,
                worldFacts = WorldStateManager.Instance != null
                    ? WorldStateManager.Instance.CaptureFacts()
                    : new Dictionary<string, bool>(),
                player = player,
                objects = new Dictionary<string, ObjectSaveData>(_objectStates),
                consumedObjects = new List<string>(_consumed),
                droppedItems = new List<DroppedItemSaveData>(_dropped)
            };
        }

        /// <summary>Records that a scene-authored saveable object was removed from the world (e.g. picked up).</summary>
        public void MarkConsumed(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                GameLog.Warn(TAG, "MarkConsumed called with an empty save key — ignored");
                return;
            }
            _consumed.Add(key);
        }

        // ── Region capture / apply ────────────────────────────────────────────

        private void CaptureRegion(string region)
        {
            if (string.IsNullOrEmpty(region)) return;

            foreach (var saveable in FindObjectsByType<SaveableObject>(FindObjectsInactive.Include))
            {
                if (saveable.gameObject.scene.name != region) continue;
                string key = saveable.SaveKey;
                if (string.IsNullOrEmpty(key))
                {
                    GameLog.Warn(TAG, $"'{saveable.name}' in {region} has no save key — not saved. Run Tools/Save/Validate Save IDs.");
                    continue;
                }
                _objectStates[key] = saveable.Capture();
            }

            // Runtime drops = pickups without a SaveableObject; replace this region's list wholesale.
            _dropped.RemoveAll(d => d.region == region);
            foreach (var pickup in FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude))
            {
                if (pickup.gameObject.scene.name != region) continue;
                if (pickup.TryGetComponent<SaveableObject>(out _)) continue;
                if (pickup.Item == null || string.IsNullOrEmpty(pickup.Item.itemId)) continue;
                _dropped.Add(new DroppedItemSaveData
                {
                    region = region,
                    itemId = pickup.Item.itemId,
                    position = SVector3.From(pickup.transform.position),
                    rotation = SQuaternion.From(pickup.transform.rotation)
                });
            }
        }

        // Runs one frame after the region loaded, so PersistentID.Start() has already hidden killed entities.
        private void ApplyRegion(string region)
        {
            foreach (var saveable in FindObjectsByType<SaveableObject>(FindObjectsInactive.Include))
            {
                if (saveable.gameObject.scene.name != region) continue;
                string key = saveable.SaveKey;
                if (string.IsNullOrEmpty(key)) continue;

                if (_consumed.Contains(key)) Destroy(saveable.gameObject);
                else if (_objectStates.TryGetValue(key, out var state)) saveable.Restore(state);
            }

            var scene = SceneManager.GetSceneByName(region);
            foreach (var drop in _dropped)
            {
                if (drop.region != region) continue;
                var item = _catalog != null ? _catalog.FindById(drop.itemId) : null;
                if (item == null || item.worldItemPrefab == null)
                {
                    GameLog.Warn(TAG, $"Dropped item '{drop.itemId}' can't be restored (unknown item or no world prefab) — skipped");
                    continue;
                }
                var go = Instantiate(item.worldItemPrefab, drop.position.To(), drop.rotation.To());
                if (go.TryGetComponent<ItemPickup>(out var pickup)) pickup.Configure(item);
                if (scene.IsValid()) SceneManager.MoveGameObjectToScene(go, scene);
            }
        }

        private void HandleRegionUnloading(string region)
        {
            if (!IsLoading) CaptureRegion(region); // region transitions keep their state in memory
        }

        private void HandleRegionLoaded(string region, bool isStartup)
        {
            ApplyRegion(region);
            if (IsLoading || _player == null) return;

            if (isStartup)
                _newGameSnapshot = BuildSaveData(NEW_GAME_SLOT); // in memory only, never written
            else
                _autosavePending = true;
        }

        private void HandleQuestCompleted(QuestSO _)
        {
            if (!IsLoading) _autosavePending = true;
        }

        // ── Load ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads a slot. On a missing / corrupted / incompatible file, notifies and leaves the world untouched.
        /// </summary>
        public void Load(string slotId)
        {
            if (IsLoading || _sceneLoader == null || _sceneLoader.IsBusy || _player == null)
            {
                GameLog.Warn(TAG, $"Load('{slotId}') ignored — a load or region change is in progress, or SaveSystem is unwired");
                return;
            }

            if (!CanLoad(out var reason))
            {
                Notify(reason);
                return;
            }

            if (!SaveFileStore.TryRead(slotId, out var data, out var error))
            {
                Notify(slotId == GameConstants.SAVE_SLOT_QUICK && error == SaveFileStore.ERROR_NO_SAVE
                    ? "No quicksave"
                    : error);
                return;
            }
            BeginLoad(data);
        }

        /// <summary>Death screen with no save: restores the start-of-session state without writing a file.</summary>
        public void RestartNewGame()
        {
            if (_newGameSnapshot == null)
            {
                GameLog.Error(TAG, "RestartNewGame: no start-of-session snapshot");
                return;
            }
            if (IsLoading || _sceneLoader == null || _sceneLoader.IsBusy) return;
            if (!CanLoad(out var reason))
            {
                Notify(reason);
                return;
            }
            BeginLoad(_newGameSnapshot);
        }

        // Loading is allowed while dead (death screen) but not under dialogue / trade / loot windows: those hold
        // references to NPCs and containers the region reload destroys, and nothing closes them on load.
        private bool CanLoad(out string reason)
        {
            reason = null;
            if (_playerState != null && _playerState.IsInDialogue) reason = "Can't load during a conversation";
            else if (_containerUI != null && _containerUI.IsOpen) reason = "Can't load while looting";
            return reason == null;
        }

        private void BeginLoad(SaveData data)
        {
            if (!SceneLoader.CanLoadRegion(data.region))
            {
                Notify($"Save region '{data.region}' can't be loaded");
                return; // checked before anything changes
            }

            IsLoading = true;
            _autosavePending = false;
            _onLoadStarted?.Raise(true);

            WorldStateManager.Instance?.RestoreFacts(data.worldFacts);
            // Copies: later captures must never mutate the loaded data (the new-game snapshot is reused).
            _objectStates = new Dictionary<string, ObjectSaveData>(data.objects ?? new Dictionary<string, ObjectSaveData>());
            _consumed = new HashSet<string>(data.consumedObjects ?? new List<string>());
            _dropped = new List<DroppedItemSaveData>(data.droppedItems ?? new List<DroppedItemSaveData>());
            PlaytimeSeconds = data.meta?.playtimeSeconds ?? 0f;

            // Objects are applied by HandleRegionLoaded before FinishLoad runs.
            _sceneLoader.ReloadRegion(data.region, success =>
            {
                if (success) FinishLoad(data);
                else FailLoad(data.region);
            });
        }

        // The region scene failed to load after the old one was unloaded — release the load state so the game
        // isn't stuck behind the loading overlay; the player keeps its current state.
        private void FailLoad(string region)
        {
            Time.timeScale = 1f;
            CursorManager.Lock(); // every menu was closed by the load start
            IsLoading = false;
            _onLoadFinished?.Raise(true);
            Notify($"Loading failed: region '{region}' could not be loaded");
        }

        private void FinishLoad(SaveData data)
        {
            _player.Restore(data.player, _catalog);
            _questEvents?.ReseedState();
            Time.timeScale = 1f; // every pausing screen is closed by the load
            CursorManager.Lock();
            IsLoading = false;
            _onLoadFinished?.Raise(true);
            Notify(data.meta != null && data.meta.slotId == NEW_GAME_SLOT ? "Restarted" : "Game loaded");
        }

        // ── Input / feedback ──────────────────────────────────────────────────

        private void HandleQuickSave(InputAction.CallbackContext _) => Save(GameConstants.SAVE_SLOT_QUICK);

        private void HandleQuickLoad(InputAction.CallbackContext _)
        {
            if (_confirmDialog != null && _confirmDialog.IsOpen) return;
            Load(GameConstants.SAVE_SLOT_QUICK);
        }

        private void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            GameLog.Info(TAG, message);
            _onSaveNotification?.Raise(message);
        }
    }
}
