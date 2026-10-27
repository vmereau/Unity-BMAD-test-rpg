using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Gothic-style daily routine: hour blocks (<c>start → end : activity @ spot</c>) evaluated statelessly from the
    /// time of day (<see cref="GameClock.Active"/>, polled). A block change sends the block's spot to the
    /// <see cref="EntityBrain"/>, the only mover: walk on a natural hour change, warp on the first evaluation (region
    /// load) and after <c>OnTimeSkipped</c> (sleep). Nothing is saved. Runs after <c>EntityBrain.Start</c>.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class NpcRoutine : MonoBehaviour
    {
        private const string TAG = "[NpcRoutine]";

        [SerializeField] private EntityBrain _brain;
        [Tooltip("Raised by GameClock.SkipTo (sleep) — the NPC warps to its new block.")]
        [SerializeField] private GameEventSO_Float _onTimeSkipped;
        [Tooltip("Seconds between clock reads.")]
        [SerializeField] private float _pollInterval = 1f;
        [Tooltip("First matching block wins. No block for the hour = home behaviour.")]
        [SerializeField] private List<NpcRoutineBlock> _blocks = new List<NpcRoutineBlock>();

        private int _currentIndex = -1;
        private bool _evaluated;
        private bool _snapNext = true;
        private float _pollTimer;
        private bool _warnedNoClock;

        /// <summary>Current block, or null (no clock / no block for the hour → home behaviour). Hook for future activity / dialogue gating.</summary>
        public NpcRoutineBlock CurrentBlock => _currentIndex >= 0 && _currentIndex < _blocks.Count ? _blocks[_currentIndex] : null;
        public NpcActivity CurrentActivity => CurrentBlock != null ? CurrentBlock.activity : NpcActivity.Stand;

        private void Awake()
        {
            if (_brain == null) _brain = GetComponent<EntityBrain>();
            if (_brain == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: no EntityBrain — routine disabled");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_onTimeSkipped != null) _onTimeSkipped.AddListener(HandleTimeSkipped);
        }

        private void OnDisable()
        {
            if (_onTimeSkipped != null) _onTimeSkipped.RemoveListener(HandleTimeSkipped);
        }

        private void Start()
        {
            _pollTimer = PollInterval;
            Evaluate();
        }

        private float PollInterval => Mathf.Max(0.1f, _pollInterval);

        private void Update()
        {
            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = PollInterval;
            Evaluate();
        }

        private void HandleTimeSkipped(float seconds)
        {
            _snapNext = true;
            Evaluate();
        }

        private void Evaluate()
        {
            GameClock clock = GameClock.Active;
            if (clock == null)
            {
                if (!_warnedNoClock) GameLog.Info(TAG, $"{gameObject.name}: no GameClock loaded — home behaviour");
                _warnedNoClock = true;
                return;
            }

            int index = NpcRoutineSchedule.FindBlock(_blocks, clock.Hour);
            bool snap = _snapNext;
            if (_evaluated && index == _currentIndex && !snap) return;
            // Never walk off mid-dialogue / trade: a walking change waits while a menu holds the cursor. Snaps (load, sleep) don't.
            if (_evaluated && !snap && !CursorManager.IsLocked) return;
            _snapNext = false;

            // A snap with an unchanged block still re-sends it: after a skip the NPC may stand mid-walk.
            _evaluated = true;
            _currentIndex = index;
            Transform spot = index >= 0 ? _blocks[index].spot : null;
            GameLog.Info(TAG, $"{gameObject.name} → block {index} at {clock.Hour:00}h ({(spot != null ? spot.name : "home")}{(snap ? ", snap" : "")})");
            _brain.SetRoutineSpot(spot, snap);
        }
    }
}
