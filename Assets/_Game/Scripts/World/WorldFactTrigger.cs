using Game.AI;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Sets a <see cref="WorldFact"/> once when the player stands inside this volume. Shape = every
    /// <see cref="BoxCollider"/> on this GameObject and its children (triggers on Ignore Raycast, only center / size
    /// are read). The project has no trigger events: containment is polled every <c>_pollInterval</c> seconds with
    /// <see cref="RestrictedAreaRules.IsInsideBox"/>, like <see cref="RestrictedArea"/>. Idles once the fact is true
    /// (also after a load), so it never writes the fact twice.
    /// <para>Two optional gates (<see cref="WorldFactTriggerRules"/>) must also pass in the same poll: an hour window read
    /// from <see cref="GameClock.Active"/> (no clock = never fires), and a required NPC that is present (active), alive,
    /// conscious and placed per <see cref="NpcPlacementGate"/>. No gates = plain box trigger.</para>
    /// </summary>
    public class WorldFactTrigger : MonoBehaviour
    {
        private const string TAG = "[WorldFactTrigger]";

        [SerializeField] private WorldFact _fact;
        [Min(0.05f)] [SerializeField] private float _pollInterval = 0.25f;

        [Header("Required fact (optional)")]
        [Tooltip("When set, this fact must be true (e.g. a QuestFact 'started') for the trigger to fire. Empty = no fact gate.")]
        [SerializeField] private Fact _requiredFact;

        [Header("Hour gate (optional)")]
        [Tooltip("Only fire between Start (inclusive) and End (exclusive), wrapping midnight. Needs the GameClock (Core): without it a gated trigger never fires.")]
        [SerializeField] private bool _useHours;
        [Range(0, 23)] [SerializeField] private int _startHour;
        [Range(0, 23)] [SerializeField] private int _endHour;

        [Header("Required NPC (optional)")]
        [Tooltip("When set, this NPC must be present (active), alive and conscious, and placed as below. Empty = no NPC gate.")]
        [SerializeField] private EntityBrain _requiredNpc;
        [SerializeField] private NpcPlacementGate _npcPlacement = NpcPlacementGate.Anywhere;
        [Tooltip("InRoutineBlock: label of the NpcRoutine block the NPC must currently be in.")]
        [SerializeField] private string _routineBlockLabel;
        [Tooltip("NearSpot: the spot the NPC must stand near.")]
        [SerializeField] private Transform _spot;
        [Tooltip("NearSpot: max flat (XZ) distance from the spot, in metres.")]
        [Min(0.1f)] [SerializeField] private float _spotRadius = 2f;

        private BoxCollider[] _boxes = System.Array.Empty<BoxCollider>();
        private EntityHealth _npcHealth;
        private NpcRoutine _npcRoutine;
        private bool _hasNpcGate;
        private bool _loggedNoClock;
        private float _nextPoll;

        private void Awake()
        {
            _boxes = GetComponentsInChildren<BoxCollider>(true);
            if (_fact == null || _boxes.Length == 0)
            {
                GameLog.Error(TAG, $"{name}: needs a WorldFact and at least one BoxCollider shape — disabled.");
                enabled = false;
                return;
            }

            if (_useHours && _startHour == _endHour)
                GameLog.Warn(TAG, $"{name}: hour gate on with start == end ({_startHour}) — passes all day (needs only a clock).");

            _hasNpcGate = _requiredNpc != null;
            if (!_hasNpcGate) return;

            _npcHealth = _requiredNpc.GetComponent<EntityHealth>();
            _npcRoutine = _requiredNpc.GetComponent<NpcRoutine>();
            if (_npcRoutine == null) _npcRoutine = _requiredNpc.GetComponentInChildren<NpcRoutine>(true);
            string problem = null;
            if (_npcHealth == null)
                problem = $"{_requiredNpc.name} has no EntityHealth (dead / unconscious can't be read)";
            else if (_npcPlacement == NpcPlacementGate.NearSpot && _spot == null)
                problem = "NearSpot gate has no spot";
            else if (_npcPlacement == NpcPlacementGate.InRoutineBlock && string.IsNullOrEmpty(_routineBlockLabel))
                problem = "InRoutineBlock gate has no block label";
            else if (_npcPlacement == NpcPlacementGate.InRoutineBlock && _npcRoutine == null)
                problem = $"InRoutineBlock gate but {_requiredNpc.name} has no NpcRoutine";
            if (problem == null)
            {
                if (_npcPlacement == NpcPlacementGate.InRoutineBlock && !_npcRoutine.HasBlockLabel(_routineBlockLabel))
                    GameLog.Warn(TAG, $"{name}: no routine block on {_requiredNpc.name} is labelled '{_routineBlockLabel}' — the gate never passes.");
                return;
            }

            GameLog.Error(TAG, $"{name}: {problem} — disabled.");
            enabled = false;
        }

        private void Update()
        {
            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + _pollInterval;

            WorldStateManager state = WorldStateManager.Instance;
            var player = TargetRegistry.Player;
            if (state == null || player == null || state.GetFact(_fact)) return;
            if (!Contains(player.transform.position)) return;
            if (!GatesPass(state)) return;

            state.SetWorldEvent(_fact, true);
            GameLog.Info(TAG, $"{name}: player entered — {_fact.name} set");
        }

        private bool GatesPass(WorldStateManager state)
        {
            if (!WorldFactTriggerRules.FactGatePasses(_requiredFact != null, _requiredFact != null && state.GetFact(_requiredFact)))
                return false;

            GameClock clock = GameClock.Active;
            if (_useHours && clock == null && !_loggedNoClock)
            {
                _loggedNoClock = true;
                GameLog.Info(TAG, $"{name}: hour-gated but no GameClock loaded (region without Core) — won't fire.");
            }
            if (!WorldFactTriggerRules.HourGatePasses(_useHours, clock != null, clock != null ? clock.Hour : 0, _startHour, _endHour))
                return false;
            if (!_hasNpcGate) return true;

            // Unity null: a destroyed NPC (fact-driven removal) reads as null here.
            bool present = _requiredNpc != null && _requiredNpc.gameObject.activeInHierarchy;
            if (!present) return false;
            bool isDead = _npcHealth != null && _npcHealth.IsDead;
            bool isUnconscious = _npcHealth != null && _npcHealth.IsUnconscious;
            return WorldFactTriggerRules.NpcGatePasses(true, isDead, isUnconscious, PlacementOk());
        }

        private bool PlacementOk()
        {
            switch (_npcPlacement)
            {
                case NpcPlacementGate.InRoutineBlock:
                    // Suspended = a story sequence moves the NPC; the stale block label doesn't say where it is.
                    if (_npcRoutine == null || _npcRoutine.IsSuspended) return false;
                    NpcRoutineBlock block = _npcRoutine.CurrentBlock;
                    return WorldFactTriggerRules.IsInBlock(block?.label, _routineBlockLabel);
                case NpcPlacementGate.NearSpot:
                    return _spot != null && WorldFactTriggerRules.IsNearSpot(_requiredNpc.transform.position, _spot.position, _spotRadius);
                default:
                    return true;
            }
        }

        private bool Contains(Vector3 worldPoint)
        {
            for (int i = 0; i < _boxes.Length; i++)
            {
                BoxCollider box = _boxes[i];
                if (box == null || !box.enabled || !box.gameObject.activeInHierarchy) continue;
                if (RestrictedAreaRules.IsInsideBox(box.transform.InverseTransformPoint(worldPoint), box.center, box.size))
                    return true;
            }
            return false;
        }
    }
}
