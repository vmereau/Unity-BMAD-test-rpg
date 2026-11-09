using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Gothic-style daily routine: hour blocks (<c>start → end : Stand @ spot | Patrol waypoints</c>) evaluated
    /// statelessly from the time of day (<see cref="GameClock.Active"/>, polled). A block change sends the block's spot or
    /// patrol route to the <see cref="EntityBrain"/>, the only mover: walk on a natural hour change, warp on the first
    /// evaluation (region load) and after <c>OnTimeSkipped</c> (sleep). Nothing is saved. Runs after <c>EntityBrain.Start</c>.
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
        [Tooltip("First matching block wins. No block for the hour = stand at the spawn spot. Without a clock, the first all-day block (start == end) applies.")]
        [SerializeField] private List<NpcRoutineBlock> _blocks = new List<NpcRoutineBlock>();

        private int _currentIndex = -1;
        private bool _evaluated;
        private bool _snapNext = true;
        private float _pollTimer;
        private bool _warnedNoClock;
        private readonly HashSet<int> _warnedEmptyRoute = new HashSet<int>();

        /// <summary>Current block, or null (no block for the hour → stands at its spawn spot). Hook for future activity / dialogue gating.</summary>
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
                // Region played without Core: an all-day block (e.g. a guard's patrol) still applies, once.
                if (_warnedNoClock) return;
                _warnedNoClock = true;
                int allDay = NpcRoutineSchedule.FindAllDayBlock(_blocks);
                GameLog.Info(TAG, $"{gameObject.name}: no GameClock loaded — {(allDay >= 0 ? $"all-day block {allDay}" : "no routine")}");
                if (allDay < 0) return;
                // _snapNext stays set: a clock appearing later (Core loaded after the region) still snaps its first block.
                _evaluated = true;
                _currentIndex = allDay;
                SendBlock(allDay, true);
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
            GameLog.Info(TAG, $"{gameObject.name} → block {index} at {clock.Hour:00}h{(snap ? " (snap)" : "")}");
            SendBlock(index, snap);
        }

        // Block → brain target: a Patrol block with waypoints sends its route, anything else a stand spot (null = spawn spot).
        private void SendBlock(int index, bool snap)
        {
            NpcRoutineBlock block = index >= 0 ? _blocks[index] : null;
            if (block == null) { _brain.SetRoutineTarget(null, null, snap); return; }

            List<Transform> route = null;
            if (block.activity == NpcActivity.Patrol)
            {
                if (block.waypoints != null)
                    foreach (Transform waypoint in block.waypoints)
                        if (waypoint != null) (route ??= new List<Transform>()).Add(waypoint);
                if (route == null && _warnedEmptyRoute.Add(index))
                    GameLog.Warn(TAG, $"{gameObject.name}: Patrol block {index} has no waypoints — standing instead");
            }
            _brain.SetRoutineTarget(block.spot, route, snap);
        }
    }
}
