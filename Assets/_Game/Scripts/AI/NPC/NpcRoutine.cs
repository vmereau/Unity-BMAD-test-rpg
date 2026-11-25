using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Gothic-style daily routine: hour blocks (<c>start → end : Stand @ spot | Patrol waypoints | Sleep in bed</c>) evaluated
    /// statelessly from the time of day (<see cref="GameClock.Active"/>, polled). A block change sends the block's spot or
    /// patrol route to the <see cref="EntityBrain"/>, the only mover: walk on a natural hour change, warp on the first
    /// evaluation (region load) and after <c>OnTimeSkipped</c> (sleep). Nothing is saved. Runs after <c>EntityBrain.Start</c>.
    /// <c>_torchAtNight</c> (spec-npc-night-torch): at every poll, asks <see cref="NpcArmament.SetTorchWanted"/> for a torch
    /// while it is night and the NPC is not asleep (<see cref="NpcTorchRules.WantsTorch"/>).
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

        [Header("Night torch")]
        [Tooltip("Holds a torch in the offhand from sunset to sunrise while awake (granted if none is carried); the daytime " +
                 "offhand item (shield) returns by day and for fights. Needs NpcArmament with a night torch item.")]
        [SerializeField] private bool _torchAtNight;
        [Tooltip("Sunrise / sunset hours deciding the night. Null = 6 / 22.")]
        [SerializeField] private DayNightConfigSO _dayNight;

        private int _currentIndex = -1;
        private bool _evaluated;
        private bool _snapNext = true;
        private float _pollTimer;
        private bool _warnedNoClock;
        private readonly HashSet<int> _warnedBlocks = new HashSet<int>();
        private NpcArmament _armament;

        /// <summary>Current block, or null (no block for the hour → stands at its spawn spot). Hook for future activity / dialogue gating.</summary>
        public NpcRoutineBlock CurrentBlock => _currentIndex >= 0 && _currentIndex < _blocks.Count ? _blocks[_currentIndex] : null;
        public NpcActivity CurrentActivity => CurrentBlock != null ? CurrentBlock.activity : NpcActivity.Stand;

        private void Awake()
        {
            if (_brain == null) _brain = GetComponent<EntityBrain>();
            _armament = GetComponent<NpcArmament>();
            if (_torchAtNight && _armament == null)
                GameLog.Warn(TAG, $"{gameObject.name}: torchAtNight set but no NpcArmament — no night torch");
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
            UpdateTorch();
        }

        private float PollInterval => Mathf.Max(0.1f, _pollInterval);

        /// <summary>True when some block's <see cref="NpcRoutineBlock.label"/> equals <paramref name="label"/> (ordinal). Used to validate gates that match by label.</summary>
        public bool HasBlockLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return false;
            for (int i = 0; i < _blocks.Count; i++)
                if (_blocks[i] != null && string.Equals(_blocks[i].label, label, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>True while a story sequence owns this NPC (<see cref="Suspend"/>): no block is sent.</summary>
        public bool IsSuspended { get; private set; }

        /// <summary>Stops sending blocks to the brain until <see cref="Resume"/> (a scripted sequence moves the NPC).</summary>
        public void Suspend() => IsSuspended = true;

        /// <summary>Hands the NPC back: the current block is re-sent at once (warped there when <paramref name="snap"/>).</summary>
        public void Resume(bool snap)
        {
            if (!IsSuspended) return;
            IsSuspended = false;
            _evaluated = false; // force a re-send even if the block didn't change
            _warnedNoClock = false; // no clock: the all-day block is applied once more
            _snapNext |= snap;
            _pollTimer = PollInterval;
            if (isActiveAndEnabled) Evaluate();
        }

        private void Update()
        {
            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = PollInterval;
            Evaluate();
            UpdateTorch();
        }

        private void HandleTimeSkipped(float seconds)
        {
            _snapNext = true;
            Evaluate();
            UpdateTorch();
        }

        // Night torch: not tied to the blocks (a story suspend keeps it too); no clock = day.
        private void UpdateTorch()
        {
            if (!_torchAtNight || _armament == null) return;
            GameClock clock = GameClock.Active;
            float sunrise = _dayNight != null ? _dayNight.sunriseHour : NpcTorchRules.DEFAULT_SUNRISE;
            float sunset = _dayNight != null ? _dayNight.sunsetHour : NpcTorchRules.DEFAULT_SUNSET;
            bool wants = clock != null
                         && NpcTorchRules.WantsTorch(true, DayNightMath.HourOfDay(clock.Now), sunrise, sunset, _brain.IsAsleep);
            _armament.SetTorchWanted(wants);
        }

        private void Evaluate()
        {
            if (IsSuspended) return;
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

        // Block → brain target: a Patrol block with waypoints sends its route, a Sleep block its bed (spot = the bed's approach),
        // anything else a stand spot (null = spawn spot).
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
                if (route == null && _warnedBlocks.Add(index))
                    GameLog.Warn(TAG, $"{gameObject.name}: Patrol block {index} has no waypoints — standing instead");
            }
            Game.World.Bed bed = null;
            if (block.activity == NpcActivity.Sleep)
            {
                if (block.bed != null && block.bed.IsNpcBed) bed = block.bed;
                else if (_warnedBlocks.Add(index))
                    GameLog.Warn(TAG, $"{gameObject.name}: Sleep block {index} has no bed with an NPC approach + sleep anchor — standing instead");
            }
            _brain.SetRoutineTarget(bed != null ? bed.NpcApproach : block.spot, route, snap, bed);
        }
    }
}
