using Game.AI;
using Game.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Game.World
{
    /// <summary>
    /// Hides a target object while a <see cref="Fact"/> is true: a blocker a quest clears, or an NPC who leaves the world
    /// (e.g. on an <see cref="ElapsedFact"/>). Lives on a parent of the target, so it keeps listening after the target
    /// goes. Reads the fact through <see cref="WorldStateManager.GetFact"/> in <c>Start</c> (which covers a load: facts
    /// are restored before the region reloads), on <c>OnFactChanged</c>, and on a slow poll for the computed facts that
    /// raise no event (Quest / Skill / Stat). It only shows again a target it hid itself, so it never undoes another
    /// system's hiding. An entity target (an <see cref="EntityHealth"/> on the target itself) is never touched once dead,
    /// waits while it fights, chases a thief or is knocked out, and closes its dialogue before it goes. Hiding is not a
    /// kill: no KilledFact, no XP. Decisions: <see cref="FactRemovedObjectRules"/>.
    /// </summary>
    public class FactRemovedObject : MonoBehaviour
    {
        private const string TAG = "[FactRemovedObject]";
        private const float MIN_POLL_INTERVAL = 0.1f;

        [Tooltip("Any fact, stored or computed (World, Dialogue, Quest, Elapsed…). The target is hidden while it is true.")]
        [SerializeField] private Fact _fact;
        [Tooltip("The object to hide: a blocker, or an NPC root (Entity_base / NPC_base variant).")]
        [SerializeField] private GameObject _target;
        [SerializeField] private GameEventSO_Fact _onFactChanged;

        [Tooltip("Seconds between fact reads, for computed facts that raise no event and for an NPC waiting out a fight.")]
        [SerializeField] private float _pollInterval = 1f;

        [Tooltip("Optional. Left off in the scene, because a bake cuts an enabled carving obstacle into the NavMesh for good.")]
        [SerializeField] private NavMeshObstacle _runtimeCarve;

        private EntityHealth _health;
        private EntityBrain _brain;
        private NPCPresence _presence;
        private PersistentID _persistentID;
        private float _pollTimer;
        private bool _started;
        private bool _waitLogged;
        private bool _hiddenByMe;

        private bool IsEntity => _health != null;

        private void Awake()
        {
            if (_runtimeCarve != null) _runtimeCarve.enabled = true;
            if (_fact != null && _target != null && _onFactChanged != null)
            {
                _target.TryGetComponent(out _health);
                _target.TryGetComponent(out _brain);
                _target.TryGetComponent(out _presence);
                _target.TryGetComponent(out _persistentID);
                if (_health == null && _target.GetComponentInChildren<EntityHealth>(true) != null)
                    GameLog.Error(TAG, $"{name}: _target '{_target.name}' wraps an entity — point it at the NPC root (EntityHealth), or it is hidden like a blocker (even dead or mid-fight)");
                return;
            }
            GameLog.Error(TAG, $"{name}: needs a fact, a target and the fact event — disabled.");
            enabled = false;
        }

        private void OnEnable()
        {
            if (_onFactChanged != null) _onFactChanged.AddListener(HandleFactChanged);
        }

        private void OnDisable()
        {
            if (_onFactChanged != null) _onFactChanged.RemoveListener(HandleFactChanged);
        }

        private void Start()
        {
            _started = true;
            _pollTimer = PollInterval;
            if (WorldStateManager.Instance == null)
            {
                GameLog.Warn(TAG, $"{name}: no WorldStateManager loaded — the target stays until one is.");
                return;
            }
            Evaluate();
        }

        private float PollInterval => Mathf.Max(MIN_POLL_INTERVAL, _pollInterval);

        private void Update()
        {
            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = PollInterval;
            Evaluate();
        }

        private void HandleFactChanged(FactData data)
        {
            // Applied on the next Update, not inside the dispatch: hiding an NPC here would unsubscribe its own
            // NPCMemoryComponent before it hears this change.
            if (_started && data.key == _fact.ToString()) _pollTimer = 0f;
        }

        private void Evaluate()
        {
            WorldStateManager wsm = WorldStateManager.Instance;
            if (wsm == null || _target == null) return;

            bool factTrue = wsm.GetFact(_fact);
            bool isDead = IsEntity && (_health.IsDead || IsKilled(wsm));
            // Chasing / scolding a thief is not combat, but its scold line has no requester to close.
            bool isBusy = IsEntity && (_health.IsUnconscious || (_brain != null && (_brain.IsInCombat || _brain.IsChasingThief)));

            switch (FactRemovedObjectRules.Decide(factTrue, IsEntity, isDead, isBusy, _target.activeSelf, _hiddenByMe))
            {
                case FactRemovalAction.Hide:
                    Hide();
                    break;
                case FactRemovalAction.Show:
                    _waitLogged = false;
                    _hiddenByMe = false;
                    _target.SetActive(true);
                    if (IsEntity) GameLog.Info(TAG, $"{_target.name} is back ({_fact.name} is false)");
                    break;
                case FactRemovalAction.Wait:
                    if (_waitLogged) break;
                    _waitLogged = true;
                    GameLog.Info(TAG, $"{_target.name} leaves once out of combat and on their feet ({_fact.name})");
                    break;
            }
        }

        private void Hide()
        {
            _waitLogged = false;
            if (IsEntity)
            {
                // Closes the dialogue this NPC opened, and the trade window bought from it (NPCTradeUI listens too).
                if (_presence != null) _presence.RequestDialogueClose();
                GameLog.Info(TAG, $"{_target.name} leaves the world ({_fact.name})");
            }
            _hiddenByMe = true;
            _target.SetActive(false); // EntityBrain.OnDisable gets out of bed and frees it
        }

        private bool IsKilled(WorldStateManager wsm) =>
            _persistentID != null && _persistentID.KilledFact != null && wsm.IsKilled(_persistentID.KilledFact);
    }
}
