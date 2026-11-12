using System.Collections.Generic;
using Game.AI;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The arrival event, part 1 (event-001, spec-area-1-arrival-escort-beating). On a region load without
    /// <see cref="ArrivalSequenceSO.completedFact"/>, two guards escort the player from the arrival gate down the lane to the
    /// stage (free walk: they wait for a lagging player, warn then brawl a straying one; a player knocked out on the way is
    /// moved to the stage). There the player is held in place, the Warden gives a forced speech, then knocks the worker
    /// (Tam) out in a real non-lethal brawl. Afterwards everyone goes back to their routine (guards to their gate posts,
    /// Tam to the campfire once he gets up) and the fact is set. The participants' <see cref="NpcRoutine"/>s are suspended
    /// meanwhile; the brains stay the only movers. Saving is refused while it runs; nothing of it is saved.
    /// </summary>
    public class ArrivalSequence : MonoBehaviour
    {
        private const string TAG = "[Arrival]";
        // Technical, not gameplay tuning: frames to wait after load so routines (Start, order 100) and brains settle.
        private const int START_DELAY_FRAMES = 3;
        // Seconds between two attempts to open the Warden's speech (a failing request logs a warning each time).
        private const float SPEECH_RETRY_INTERVAL = 0.5f;

        private enum Phase { WaitingToStart, Escort, Dragged, Speech, Beating, Closing, Done }

        [SerializeField] private ArrivalSequenceSO _config;

        [Header("Cast")]
        [SerializeField] private EntityBrain _guardLeft;
        [SerializeField] private EntityBrain _guardRight;
        [SerializeField] private EntityBrain _warden;
        [Tooltip("The worker beaten on the stage (Tam). Passive, so he never hits back.")]
        [SerializeField] private EntityBrain _victim;

        [Header("Route (same point count, last = the guards' places at the stage)")]
        [SerializeField] private List<Transform> _laneLeft = new List<Transform>();
        [SerializeField] private List<Transform> _laneRight = new List<Transform>();

        [Header("Spots")]
        [Tooltip("Where the player starts (a load without the fact warps them here).")]
        [SerializeField] private Transform _playerStart;
        [Tooltip("Where the player watches the beating (a dragged player is put here).")]
        [SerializeField] private Transform _watchSpot;
        [SerializeField] private Transform _wardenStageSpot;
        [SerializeField] private Transform _victimStageSpot;

        [Header("Dev")]
        [Tooltip("Editor / development builds only: skip the arrival at start (fact set, everyone on their routines).")]
        [SerializeField] private bool _devSkip;
        [Tooltip("Where a dev skip puts the player (by the tent).")]
        [SerializeField] private Transform _devPlayerSpot;

        private Phase _phase = Phase.WaitingToStart;
        private int _framesWaited;
        private int _laneIndex;
        private float _phaseTime;
        private float _lastWaitBarkTime = float.NegativeInfinity;
        private bool _strayWarned;
        private float _strayWarnTime;
        private int _barkIndex = -1;
        private bool _valid;
        private EntityBrain _chaser; // guard walking after a straying player (Intercepting), until it reaches them
        private bool _speechOpened;
        private float _speechWaitTime;    // time the speech could have opened (player calm, no menu) without opening
        private float _nextSpeechAttempt;

        private EntityHealth _guardLeftHealth, _guardRightHealth, _wardenHealth, _victimHealth;
        private NPCPresence _wardenPresence;
        private FactionMember _victimMember;
        private readonly List<NpcRoutine> _routines = new List<NpcRoutine>();
        private FactionMember _player;
        private PlayerStateManager _playerState;

        /// <summary>True between the start of the escort and the end of the beating.</summary>
        public bool IsRunning => _phase != Phase.WaitingToStart && _phase != Phase.Done;

        private void Awake()
        {
            _valid = _config != null && _config.completedFact != null && _guardLeft != null && _guardRight != null &&
                     _warden != null && _victim != null && _watchSpot != null && _laneLeft.Count > 0 &&
                     _laneLeft.Count == _laneRight.Count && !_laneLeft.Contains(null) && !_laneRight.Contains(null);
            if (!_valid)
            {
                GameLog.Error(TAG, $"{name}: needs a config with completedFact, both guards, the Warden, the victim, a watch spot " +
                                   "and two non-empty lanes of the same length — arrival disabled");
                return;
            }
            _guardLeftHealth = _guardLeft.GetComponent<EntityHealth>();
            _guardRightHealth = _guardRight.GetComponent<EntityHealth>();
            _wardenHealth = _warden.GetComponent<EntityHealth>();
            _victimHealth = _victim.GetComponent<EntityHealth>();
            _wardenPresence = _warden.GetComponent<NPCPresence>();
            _victimMember = _victim.GetComponent<FactionMember>();
            foreach (EntityBrain brain in new[] { _guardLeft, _guardRight, _warden, _victim })
                if (brain.TryGetComponent(out NpcRoutine routine)) _routines.Add(routine);
            if (_config.wardenSpeech == null) GameLog.Warn(TAG, $"{name}: no Warden speech — the beating starts without it");
        }

        private void OnDisable()
        {
            // Region unloaded / reloaded mid-sequence (F9): hand everything back; the next load decides again.
            if (IsRunning) Release(snap: false);
            _phase = Phase.WaitingToStart;
            _framesWaited = 0;
        }

        private void Update()
        {
            if (!_valid) return;
            switch (_phase)
            {
                case Phase.WaitingToStart: TryStart(); return;
                case Phase.Escort:   TickEscort();   break;
                case Phase.Dragged:  TickDragged();  break;
                case Phase.Speech:   TickSpeech();   break;
                case Phase.Beating:  TickBeating();  break;
                case Phase.Closing:  TickClosing();  break;
            }
            _phaseTime += Time.deltaTime;
        }

        // --- Start ---

        private void TryStart()
        {
            // Let routines (Start, execution order 100) and the brains settle first, and never during a load.
            if (++_framesWaited < START_DELAY_FRAMES) return;
            WorldStateManager wsm = WorldStateManager.Instance;
            _player = TargetRegistry.Player;
            if (wsm == null || _player == null) return;
            if (SaveSystem.Instance != null && SaveSystem.Instance.IsLoading) return;
            _playerState = _player.GetComponent<PlayerStateManager>();

            if (wsm.GetFact(_config.completedFact))
            {
                _phase = Phase.Done;
                return;
            }
            if (DevSkipAllowed && _devSkip)
            {
                DevSkip();
                return;
            }
            Begin();
        }

        private void Begin()
        {
            SaveSystem.BlockSaving(this);
            foreach (NpcRoutine routine in _routines) routine.Suspend();
            _strayWarned = false;
            _strayWarnTime = 0f;
            _lastWaitBarkTime = float.NegativeInfinity;
            _chaser = null;

            _laneIndex = 0;
            SendGuards(snap: true);
            if (_wardenStageSpot != null) _warden.SetRoutineTarget(_wardenStageSpot, null, true);
            if (_victimStageSpot != null) _victim.SetRoutineTarget(_victimStageSpot, null, true);
            if (_playerStart != null && FlatDistance(_player.Transform.position, _playerStart.position) > _config.playerStartTolerance)
                TeleportPlayer(_playerStart);

            SayLine(_guardLeft, _config.moveBarks);
            _laneIndex = Mathf.Min(1, _laneLeft.Count - 1);
            SendGuards(snap: false);
            EnterPhase(Phase.Escort);
            GameLog.Info(TAG, "arrival started — escort to the stage");
        }

        // --- Escort ---

        private void TickEscort()
        {
            if (_player.Damageable.IsUnconscious)
            {
                Drag();
                return;
            }

            float playerDistance = FlatDistance(_player.Transform.position, GroupCenter());
            // Straying is measured from the lane, not the guards (a chasing guard would drag their centre along): the nearer
            // of the current and previous lane points, so an advance doesn't put a following player "far" at once.
            float strayDistance = Mathf.Min(FlatDistance(_player.Transform.position, LaneCenter(_laneIndex)),
                FlatDistance(_player.Transform.position, LaneCenter(Mathf.Max(0, _laneIndex - 1))));
            TickChaser(strayDistance);
            bool brawling = _chaser != null || _guardLeft.IsFighting(_player) || _guardRight.IsFighting(_player);
            switch (ArrivalSequenceRules.EvaluateStray(strayDistance, _config.strayDistance, _strayWarned,
                        Time.time - _strayWarnTime, _config.strayGraceSeconds, brawling))
            {
                case StrayAction.Warn:
                    _strayWarned = true;
                    _strayWarnTime = Time.time;
                    SayLine(NearestConsciousGuard(), _config.strayBarks);
                    break;
                case StrayAction.Brawl:
                    // Walk up first: a brawl started from beyond the guard's disengage range would drop at once.
                    EntityBrain guard = NearestConsciousGuard();
                    if (guard != null && guard.BeginIntercept(_player, _config.chaseReachDistance, _config.chaseTimeout))
                    {
                        _strayWarned = false;
                        _chaser = guard;
                        GameLog.Info(TAG, $"{guard.name} goes after the straying player");
                    }
                    else _strayWarnTime = Time.time; // guard busy (watching, robbing, staggered): retry after the grace, no new bark
                    break;
                case StrayAction.Clear:
                    _strayWarned = false;
                    break;
            }

            if (AnyoneFightingPlayer()) return; // no advancing, and never the stage hold, mid-brawl
            bool isLast = _laneIndex >= _laneLeft.Count - 1;
            switch (ArrivalSequenceRules.EvaluateEscort(GuardsArrived(), playerDistance, _config.followDistance, isLast))
            {
                case EscortStep.WaitForPlayer:
                    if (Time.time - _lastWaitBarkTime >= _config.waitBarkCooldown && SayLine(NearestConsciousGuard(), _config.waitBarks))
                        _lastWaitBarkTime = Time.time;
                    break;
                case EscortStep.Advance:
                    _laneIndex++;
                    SendGuards(snap: false);
                    break;
                case EscortStep.ReachedStage:
                    BeginSpeech();
                    break;
            }
        }

        // A guard walking after the straying player: reached → brawl; player back in line → back to the escort; aborted
        // (timeout, hit, unreachable) → the stray rules start over (warning, then another chase).
        private void TickChaser(float playerDistance)
        {
            if (_chaser == null) return;
            switch (_chaser.Intercept)
            {
                case EntityBrain.InterceptStatus.Approaching:
                    if (playerDistance > _config.strayDistance) return;
                    _chaser.EndIntercept();
                    GameLog.Info(TAG, $"{_chaser.name}: the player is back in line — chase called off");
                    break;
                case EntityBrain.InterceptStatus.Reached:
                    if (_chaser.StartBrawl(_player)) GameLog.Info(TAG, $"{_chaser.name} brawls the straying player");
                    else _chaser.EndIntercept();
                    break;
                default:
                    _chaser.ClearIntercept();
                    break;
            }
            _chaser = null;
        }

        private void EndChase()
        {
            if (_chaser == null) return;
            if (_chaser.Intercept == EntityBrain.InterceptStatus.Approaching || _chaser.Intercept == EntityBrain.InterceptStatus.Reached)
                _chaser.EndIntercept();
            else _chaser.ClearIntercept();
            _chaser = null;
        }

        // Knocked out on the way: the body is carried to the stage, the escort waits there for the player to get up.
        private void Drag()
        {
            EndChase();
            TeleportPlayer(_watchSpot);
            _laneIndex = _laneLeft.Count - 1;
            SendGuards(snap: true); // a guard still fighting / robbing keeps its flow and walks there after
            EnterPhase(Phase.Dragged);
            GameLog.Info(TAG, "player knocked out during the escort — dragged to the stage");
        }

        private void TickDragged()
        {
            if (_player.Damageable.IsUnconscious) return;
            BeginSpeech();
        }

        // --- Stage ---

        private void BeginSpeech()
        {
            EndChase();
            EnterPhase(Phase.Speech);
            _speechOpened = false;
            _speechWaitTime = 0f;
            _nextSpeechAttempt = 0f;
            UpdateHold();
        }

        // Held only while calm: never freeze a player someone is hitting (no block / dodge) or one lying knocked out.
        private void UpdateHold()
        {
            if (_playerState != null) _playerState.SetScriptedHold(IsPlayerCalm());
        }

        private bool IsPlayerCalm() =>
            !_player.Damageable.IsUnconscious && !_player.Damageable.IsDead && !AnyoneFightingPlayer();

        private bool AnyoneFightingPlayer() =>
            _guardLeft.IsFighting(_player) || _guardRight.IsFighting(_player) || _warden.IsFighting(_player);

        private void TickSpeech()
        {
            UpdateHold();
            if (_speechOpened)
            {
                if (!CursorManager.IsLocked) return; // speech still open
                StartBeating();
                return;
            }
            if (_config.wardenSpeech == null || WardenDown() || _wardenPresence == null)
            {
                StartBeating();
                return;
            }
            // Wait (without counting) while the player isn't calm or a menu holds the cursor: an open inventory must not
            // skip the speech. The request opens synchronously: an unlocked cursor right after means it opened.
            if (!IsPlayerCalm() || !CursorManager.IsLocked) return;
            _speechWaitTime += Time.deltaTime;
            if (!_warden.IsInCombat && Time.time >= _nextSpeechAttempt)
            {
                _nextSpeechAttempt = Time.time + SPEECH_RETRY_INTERVAL;
                if (_wardenPresence.RequestForcedTopic(_config.wardenSpeech) && !CursorManager.IsLocked)
                {
                    _speechOpened = true;
                    return;
                }
            }
            if (_speechWaitTime > _config.speechOpenTimeout)
            {
                GameLog.Warn(TAG, "the Warden's speech could not open — starting the beating without it");
                StartBeating();
            }
        }

        private void StartBeating()
        {
            EnterPhase(Phase.Beating);
            if (_victimMember == null || !_warden.StartBrawl(_victimMember))
            {
                // Victim already down, Warden down / unable: nothing to watch, close at once (no closing line).
                GameLog.Warn(TAG, $"{_warden.name} could not start the beating (victim down or Warden unable to fight)");
                EnterPhase(Phase.Closing);
                return;
            }
            GameLog.Info(TAG, $"{_warden.name} beats {_victim.name}");
        }

        private void TickBeating()
        {
            UpdateHold();
            bool victimDown = _victimHealth == null || _victimHealth.IsUnconscious || _victimHealth.IsDead;
            BeatingOutcome outcome = ArrivalSequenceRules.EvaluateBeating(victimDown, WardenDown(), _phaseTime,
                _config.beatingTimeout);
            if (outcome == BeatingOutcome.Ongoing) return;
            if (outcome == BeatingOutcome.VictimDown) SayLine(_warden, _config.wardenClosingBarks);
            else GameLog.Warn(TAG, "beating interrupted (Warden down or timeout)");
            EnterPhase(Phase.Closing);
        }

        private void TickClosing()
        {
            UpdateHold();
            if (_phaseTime < _config.closingDelay) return;
            Complete(snap: false);
            GameLog.Info(TAG, "arrival done");
        }

        // --- End ---

        private void Complete(bool snap)
        {
            WorldStateManager wsm = WorldStateManager.Instance;
            if (wsm != null) wsm.SetWorldEvent(_config.completedFact, true);
            Release(snap);
            _phase = Phase.Done;
        }

        private void Release(bool snap)
        {
            EndChase();
            // A timed-out / skipped / interrupted beating: the Warden stops hitting the worker and goes back to his routine.
            if (_victimMember != null) _warden.EndBrawl(_victimMember);
            if (_playerState != null) _playerState.SetScriptedHold(false);
            foreach (NpcRoutine routine in _routines) routine.Resume(snap);
            SaveSystem.UnblockSaving(this);
        }

        // Editor and development builds only (project rule: runtime debug tools gate on Debug.isDebugBuild).
        private static bool DevSkipAllowed => Application.isEditor || Debug.isDebugBuild;

        [ContextMenu("Dev: Skip Arrival")]
        private void DevSkip()
        {
            if (!DevSkipAllowed || !Application.isPlaying || !_valid) return;
            _player = TargetRegistry.Player;
            if (_player != null)
            {
                _playerState = _player.GetComponent<PlayerStateManager>();
                if (_devPlayerSpot != null) TeleportPlayer(_devPlayerSpot);
            }
            // Resume only resumes suspended routines: suspend first so a skip at start re-sends every block (snap).
            foreach (NpcRoutine routine in _routines) routine.Suspend();
            Complete(snap: true);
            GameLog.Info(TAG, "arrival skipped (dev)");
        }

        // --- Helpers ---

        private void EnterPhase(Phase phase)
        {
            _phase = phase;
            _phaseTime = 0f;
        }

        private void SendGuards(bool snap)
        {
            _guardLeft.SetRoutineTarget(_laneLeft[_laneIndex], null, snap);
            _guardRight.SetRoutineTarget(_laneRight[_laneIndex], null, snap);
        }

        // A guard counts as arrived when at its lane point or out of the escort (down, dead).
        private bool GuardsArrived() =>
            GuardArrived(_guardLeft, _guardLeftHealth, _laneLeft[_laneIndex]) &&
            GuardArrived(_guardRight, _guardRightHealth, _laneRight[_laneIndex]);

        private bool GuardArrived(EntityBrain guard, EntityHealth health, Transform point) =>
            IsDown(health) || FlatDistance(guard.transform.position, point.position) <= _config.arriveDistance;

        // Midpoint of the conscious guards; both down → the current lane points.
        private Vector3 GroupCenter()
        {
            bool left = !IsDown(_guardLeftHealth), right = !IsDown(_guardRightHealth);
            if (left && right) return (_guardLeft.transform.position + _guardRight.transform.position) * 0.5f;
            if (left) return _guardLeft.transform.position;
            if (right) return _guardRight.transform.position;
            return LaneCenter(_laneIndex);
        }

        private Vector3 LaneCenter(int index) => (_laneLeft[index].position + _laneRight[index].position) * 0.5f;

        private EntityBrain NearestOfGuards() =>
            FlatDistance(_guardLeft.transform.position, _player.Transform.position) <=
            FlatDistance(_guardRight.transform.position, _player.Transform.position) ? _guardLeft : _guardRight;

        private EntityBrain NearestConsciousGuard()
        {
            bool left = !IsDown(_guardLeftHealth), right = !IsDown(_guardRightHealth);
            if (left && right) return NearestOfGuards();
            return left ? _guardLeft : right ? _guardRight : null;
        }

        private bool WardenDown() => IsDown(_wardenHealth);

        private static bool IsDown(EntityHealth health) => health == null || health.IsDead || health.IsUnconscious;

        private bool SayLine(EntityBrain speaker, Dialogue.BarkSetSO barks)
        {
            if (speaker == null || barks == null) return false;
            string line = barks.GetRandomLine(ref _barkIndex);
            return speaker.Say(line, _config.bubbleDuration);
        }

        // CharacterController overrides transform writes while enabled — disable around the move.
        private void TeleportPlayer(Transform spot)
        {
            if (_player == null || spot == null) return;
            CharacterController controller = _player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            _player.Transform.SetPositionAndRotation(spot.position, Quaternion.Euler(0f, spot.eulerAngles.y, 0f));
            if (wasEnabled) controller.enabled = true;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.8f, 0.3f, 0.2f, 0.9f);
            DrawLane(_laneLeft);
            DrawLane(_laneRight);
            if (_watchSpot != null) Gizmos.DrawWireSphere(_watchSpot.position, 0.6f);
        }

        private static void DrawLane(List<Transform> lane)
        {
            for (int i = 0; i < lane.Count; i++)
            {
                if (lane[i] == null) continue;
                Gizmos.DrawWireSphere(lane[i].position, 0.3f);
                if (i > 0 && lane[i - 1] != null) Gizmos.DrawLine(lane[i - 1].position, lane[i].position);
            }
        }
#endif
    }
}
