using System.Collections.Generic;
using Game.AI;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The arrival event (event-001). Part 1 (spec-area-1-arrival-escort-beating): on a region load without
    /// <see cref="ArrivalSequenceSO.completedFact"/>, two guards escort the player from the arrival gate down the lane to the
    /// stage (free walk: they wait for a lagging player, warn then brawl a straying one; a player knocked out on the way is
    /// moved to the stage). There the player is held in place, the Warden gives a forced speech, then knocks the worker
    /// (Tam) out in a real non-lethal brawl. Part 2 (spec-area-1-arrival-hut-briefing): the Warden and Tam go back to their
    /// routines, the guards escort the player on to their hut with the same rules, and the tent mate (Hobb) opens a forced
    /// briefing that ends on his topic list. When it closes, the guards and Hobb go back to their routines and the fact is
    /// set. The participants' <see cref="NpcRoutine"/>s are suspended meanwhile; the brains stay the only movers. Saving is
    /// refused while it runs; nothing of it is saved.
    /// </summary>
    public class ArrivalSequence : MonoBehaviour
    {
        private const string TAG = "[Arrival]";
        // Technical, not gameplay tuning: frames to wait after load so routines (Start, order 100) and brains settle.
        private const int START_DELAY_FRAMES = 3;
        // Seconds between two attempts to open a forced topic (a failing request logs a warning each time).
        private const float TOPIC_RETRY_INTERVAL = 0.5f;

        private enum Phase { WaitingToStart, Escort, Dragged, Speech, Beating, Closing, Briefing, Done }

        // Which walk the escort is on: to the stage (part 1) or on to the hut (part 2).
        private enum Leg { Stage, Hut }

        private enum DevStart { None, SkipAll, SkipToHutEscort }

        [SerializeField] private ArrivalSequenceSO _config;

        [Header("Cast")]
        [SerializeField] private EntityBrain _guardLeft;
        [SerializeField] private EntityBrain _guardRight;
        [SerializeField] private EntityBrain _warden;
        [Tooltip("The worker beaten on the stage (Tam). Passive, so he never hits back.")]
        [SerializeField] private EntityBrain _victim;
        [Tooltip("The tent mate (Hobb) who briefs the player at the hut.")]
        [SerializeField] private EntityBrain _tentMate;

        [Header("Route to the stage (same point count, last = the guards' places at the stage)")]
        [SerializeField] private List<Transform> _laneLeft = new List<Transform>();
        [SerializeField] private List<Transform> _laneRight = new List<Transform>();

        [Header("Route to the hut (same point count, first = by the stage, last = either side of the hut door)")]
        [SerializeField] private List<Transform> _hutLaneLeft = new List<Transform>();
        [SerializeField] private List<Transform> _hutLaneRight = new List<Transform>();

        [Header("Spots")]
        [Tooltip("Where the player starts (a load without the fact warps them here).")]
        [SerializeField] private Transform _playerStart;
        [Tooltip("Where the player watches the beating (a dragged player is put here).")]
        [SerializeField] private Transform _watchSpot;
        [SerializeField] private Transform _wardenStageSpot;
        [SerializeField] private Transform _victimStageSpot;
        [Tooltip("Where a player knocked out on the way to the hut is put.")]
        [SerializeField] private Transform _hutWatchSpot;
        [Tooltip("Where the tent mate waits for the escort (snapped there when the arrival starts).")]
        [SerializeField] private Transform _tentMateSpot;

        [Header("Dev")]
        [Tooltip("Editor / development builds only: SkipAll = fact set, everyone on their routines; SkipToHutEscort = start at part 2.")]
        [SerializeField] private DevStart _devStart;
        [Tooltip("Where a dev skip puts the player (outside the hut's south door: the bed fills the hut).")]
        [SerializeField] private Transform _devPlayerSpot;

        private Phase _phase = Phase.WaitingToStart;
        private Leg _leg;
        private int _framesWaited;
        private int _laneIndex;
        private float _phaseTime;
        private float _lastWaitBarkTime = float.NegativeInfinity;
        private bool _strayWarned;
        private float _strayWarnTime;
        private int _barkIndex = -1;
        private bool _valid;
        private bool _hutValid;
        private EntityBrain _chaser; // guard walking after a straying player (Intercepting), until it reaches them
        private bool _topicOpened;    // the Warden's speech / Hobb's briefing is open (cursor unlocked by the request)
        private float _topicWaitTime; // time the topic could have opened (player calm, no menu) without opening
        private float _nextTopicAttempt;

        private EntityHealth _guardLeftHealth, _guardRightHealth, _wardenHealth, _victimHealth, _tentMateHealth;
        private NPCPresence _wardenPresence, _tentMatePresence;
        private FactionMember _victimMember;
        private readonly List<NpcRoutine> _routines = new List<NpcRoutine>();
        private readonly List<NpcRoutine> _stageRoutines = new List<NpcRoutine>(); // Warden + victim, released after the beating
        private FactionMember _player;
        private PlayerStateManager _playerState;

        /// <summary>True between the start of the escort and the end of the hut briefing.</summary>
        public bool IsRunning => _phase != Phase.WaitingToStart && _phase != Phase.Done;

        private List<Transform> LaneLeft => _leg == Leg.Stage ? _laneLeft : _hutLaneLeft;
        private List<Transform> LaneRight => _leg == Leg.Stage ? _laneRight : _hutLaneRight;
        private Transform DragSpot => _leg == Leg.Stage ? _watchSpot : _hutWatchSpot;

        private void Awake()
        {
            _valid = _config != null && _config.completedFact != null && _guardLeft != null && _guardRight != null &&
                     _warden != null && _victim != null && _watchSpot != null && ValidLanes(_laneLeft, _laneRight);
            if (!_valid)
            {
                GameLog.Error(TAG, $"{name}: needs a config with completedFact, both guards, the Warden, the victim, a watch spot " +
                                   "and two non-empty lanes of the same length — arrival disabled");
                return;
            }
            _hutValid = _tentMate != null && _hutWatchSpot != null && _tentMateSpot != null &&
                        ValidLanes(_hutLaneLeft, _hutLaneRight);
            if (!_hutValid)
                GameLog.Warn(TAG, $"{name}: part 2 needs the tent mate, his spot, a hut watch spot and two hut lanes of the " +
                                  "same length — the arrival ends at the stage");

            _guardLeftHealth = _guardLeft.GetComponent<EntityHealth>();
            _guardRightHealth = _guardRight.GetComponent<EntityHealth>();
            _wardenHealth = _warden.GetComponent<EntityHealth>();
            _victimHealth = _victim.GetComponent<EntityHealth>();
            _wardenPresence = _warden.GetComponent<NPCPresence>();
            _victimMember = _victim.GetComponent<FactionMember>();
            foreach (EntityBrain brain in new[] { _warden, _victim })
                if (brain.TryGetComponent(out NpcRoutine routine)) _stageRoutines.Add(routine);
            _routines.AddRange(_stageRoutines);
            foreach (EntityBrain brain in new[] { _guardLeft, _guardRight, _hutValid ? _tentMate : null })
                if (brain != null && brain.TryGetComponent(out NpcRoutine routine)) _routines.Add(routine);
            if (_hutValid)
            {
                _tentMateHealth = _tentMate.GetComponent<EntityHealth>();
                _tentMatePresence = _tentMate.GetComponent<NPCPresence>();
                if (_config.hutBriefing == null) GameLog.Warn(TAG, $"{name}: no hut briefing — the arrival ends without it");
            }
            if (_config.wardenSpeech == null) GameLog.Warn(TAG, $"{name}: no Warden speech — the beating starts without it");
        }

        private static bool ValidLanes(List<Transform> left, List<Transform> right) =>
            left.Count > 0 && left.Count == right.Count && !left.Contains(null) && !right.Contains(null);

        private void OnDisable()
        {
            // Region unloaded / reloaded mid-sequence (F9): hand everything back; the next load decides again.
            if (IsRunning)
            {
                // Release first (unblocks saving, frees the player) so nothing below can skip it.
                Release(snap: false);
                // Core owns the dialogue and outlives the region: close the speech / briefing the sequence opened. The
                // speaker may already be destroyed during the unload — still raise (matched by reference).
                if (_topicOpened && !CursorManager.IsLocked)
                {
                    NPCPresence speaker = _phase == Phase.Speech ? _wardenPresence
                                        : _phase == Phase.Briefing ? _tentMatePresence : null;
                    if (speaker is not null) speaker.RequestDialogueClose();
                }
            }
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
                case Phase.Briefing: TickBriefing(); break;
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
            if (DevSkipAllowed && _devStart == DevStart.SkipAll)
            {
                DevSkip();
                return;
            }
            if (DevSkipAllowed && _devStart == DevStart.SkipToHutEscort)
            {
                if (_hutValid)
                {
                    DevSkipToHutEscort();
                    return;
                }
                GameLog.Warn(TAG, "dev start SkipToHutEscort: part 2 not set up — playing the whole arrival");
            }
            Begin();
        }

        // Save block, every routine suspended, run state reset, the tent mate waiting at the hut.
        private void TakeOwnership()
        {
            SaveSystem.BlockSaving(this);
            foreach (NpcRoutine routine in _routines) routine.Suspend();
            ResetEscortState();
            if (_hutValid) _tentMate.SetRoutineTarget(_tentMateSpot, null, true);
        }

        private void ResetEscortState()
        {
            _strayWarned = false;
            _strayWarnTime = 0f;
            _lastWaitBarkTime = float.NegativeInfinity;
            _chaser = null;
        }

        private void Begin()
        {
            TakeOwnership();
            _leg = Leg.Stage;
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

        // --- Escort (both legs) ---

        private void TickEscort()
        {
            if (_player.Damageable.IsUnconscious)
            {
                Drag();
                return;
            }

            TickStray();
            if (AnyoneFightingPlayer()) return; // no advancing, and never the stage hold, mid-brawl
            float playerDistance = FlatDistance(_player.Transform.position, GroupCenter());
            bool isLast = _laneIndex >= LaneLeft.Count - 1;
            switch (ArrivalSequenceRules.EvaluateEscort(GuardsArrived(), playerDistance, _config.followDistance, isLast))
            {
                case EscortStep.WaitForPlayer:
                    TryWaitBark();
                    break;
                case EscortStep.Advance:
                    _laneIndex++;
                    SendGuards(snap: false);
                    break;
                case EscortStep.ReachedEnd:
                    if (_leg == Leg.Stage) BeginSpeech();
                    else BeginBriefing();
                    break;
            }
        }

        // Straying is measured from the lane, not the guards (a chasing guard would drag their centre along): the nearer of
        // the current and previous lane points, so an advance doesn't put a following player "far" at once.
        private void TickStray()
        {
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
        }

        // The escort has arrived but the player stopped short of the tent mate: he calls them over (else a guard's wait line).
        private void CallPlayerOver()
        {
            if (Time.time - _lastWaitBarkTime < _config.waitBarkCooldown) return;
            bool said = _config.tentMateCallBarks != null && !IsDown(_tentMateHealth)
                ? SayLine(_tentMate, _config.tentMateCallBarks)
                : SayLine(NearestConsciousGuard(), _config.waitBarks);
            if (said) _lastWaitBarkTime = Time.time;
        }

        private void TryWaitBark()
        {
            if (Time.time - _lastWaitBarkTime >= _config.waitBarkCooldown && SayLine(NearestConsciousGuard(), _config.waitBarks))
                _lastWaitBarkTime = Time.time;
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

        // Knocked out on the way: the body is carried to the leg's end, the escort waits there for the player to get up.
        private void Drag()
        {
            EndChase();
            TeleportPlayer(DragSpot);
            _laneIndex = LaneLeft.Count - 1;
            SendGuards(snap: true); // a guard still fighting / robbing keeps its flow and walks there after
            EnterPhase(Phase.Dragged);
            GameLog.Info(TAG, $"player knocked out during the escort — dragged to the {(_leg == Leg.Stage ? "stage" : "hut")}");
        }

        private void TickDragged()
        {
            if (_player.Damageable.IsUnconscious) return;
            if (_leg == Leg.Stage) BeginSpeech();
            else BeginBriefing();
        }

        // --- Stage ---

        private void BeginSpeech()
        {
            EndChase();
            EnterPhase(Phase.Speech);
            ResetTopic();
            UpdateHold();
        }

        private void ResetTopic()
        {
            _topicOpened = false;
            _topicWaitTime = 0f;
            _nextTopicAttempt = 0f;
        }

        // Held only while calm: never freeze a player someone is hitting (no block / dodge) or one lying knocked out.
        private void UpdateHold()
        {
            if (_playerState != null) _playerState.SetScriptedHold(IsPlayerCalm());
        }

        private bool IsPlayerCalm() =>
            !_player.Damageable.IsUnconscious && !_player.Damageable.IsDead && !AnyoneFightingPlayer();

        // The Warden leaves the cast after the beating: on the hut leg his own fight with the player no longer holds the escort.
        private bool AnyoneFightingPlayer() =>
            _guardLeft.IsFighting(_player) || _guardRight.IsFighting(_player) ||
            (_leg == Leg.Stage && _warden.IsFighting(_player)) || (_hutValid && _tentMate.IsFighting(_player));

        private void TickSpeech()
        {
            UpdateHold();
            if (_topicOpened)
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
            _topicWaitTime += Time.deltaTime;
            if (!_warden.IsInCombat && Time.time >= _nextTopicAttempt)
            {
                _nextTopicAttempt = Time.time + TOPIC_RETRY_INTERVAL;
                if (_wardenPresence.RequestForcedTopic(_config.wardenSpeech) && !CursorManager.IsLocked)
                {
                    _topicOpened = true;
                    return;
                }
            }
            if (_topicWaitTime > _config.speechOpenTimeout)
            {
                GameLog.Warn(TAG, "the Warden's speech could not open — starting the beating without it");
                StartBeating();
            }
        }

        private void StartBeating()
        {
            EnterPhase(Phase.Beating);
            // Fists: a beating, not a sword fight (the Warden keeps his sword sheathed).
            if (_victimMember == null || !_warden.StartBrawl(_victimMember, useFists: true))
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
            // A player knocked out at the stage gets up there; the hut escort leaves from the stage, not a dragged body.
            if (_hutValid && _player.Damageable.IsUnconscious) return;
            if (_hutValid)
            {
                StartHutEscort(snapStageCast: false);
                return;
            }
            Complete(snap: false);
            GameLog.Info(TAG, "arrival done");
        }

        // --- Hut ---

        // The stage is over: the Warden and Tam go back to their routines (Tam gets up, then walks to the campfire), the
        // player is released and the guards lead on to the hut.
        private void StartHutEscort(bool snapStageCast)
        {
            if (_victimMember != null) _warden.EndBrawl(_victimMember);
            if (_playerState != null) _playerState.SetScriptedHold(false);
            foreach (NpcRoutine routine in _stageRoutines) routine.Resume(snapStageCast);

            ResetEscortState();
            _leg = Leg.Hut;
            _laneIndex = 0;
            SendGuards(snap: false);
            SayLine(NearestConsciousGuard(), _config.moveBarks);
            EnterPhase(Phase.Escort);
            GameLog.Info(TAG, "escort to the hut");
        }

        private void BeginBriefing()
        {
            EndChase();
            EnterPhase(Phase.Briefing);
            ResetTopic();
        }

        private void TickBriefing()
        {
            // Knocked out (a knockout also closes the dialogue): the briefing starts over once the player is up.
            if (_player.Damageable.IsUnconscious)
            {
                Drag();
                return;
            }
            if (_topicOpened)
            {
                if (!CursorManager.IsLocked) return; // briefing (then his topic list) still open
                Complete(snap: false);
                GameLog.Info(TAG, "arrival done — briefed at the hut");
                return;
            }
            TickStray();
            if (AnyoneFightingPlayer()) return;

            bool inRange = FlatDistance(_player.Transform.position, _tentMate.transform.position) <= _config.briefingDistance;
            bool couldOpen = inRange && IsPlayerCalm() && CursorManager.IsLocked && !_tentMate.IsInCombat;
            if (couldOpen) _topicWaitTime += Time.deltaTime; // the timeout only counts while it could open
            bool speakerUnavailable = _config.hutBriefing == null || _tentMatePresence == null || IsDown(_tentMateHealth);
            switch (ArrivalSequenceRules.EvaluateBriefing(speakerUnavailable, inRange,
                        couldOpen && Time.time >= _nextTopicAttempt, _topicWaitTime,
                        _config.briefingOpenTimeout))
            {
                case BriefingStep.Skip:
                    GameLog.Warn(TAG, "the tent mate's briefing could not open — the arrival ends without it");
                    Complete(snap: false);
                    break;
                case BriefingStep.Open:
                    // The request opens synchronously: an unlocked cursor right after means it opened.
                    _nextTopicAttempt = Time.time + TOPIC_RETRY_INTERVAL;
                    if (_tentMatePresence.RequestForcedTopic(_config.hutBriefing, thenTopics: true) && !CursorManager.IsLocked)
                        _topicOpened = true;
                    break;
                case BriefingStep.Wait:
                    if (!inRange) CallPlayerOver();
                    break;
            }
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
            foreach (NpcRoutine routine in _routines) routine.Resume(snap); // no-op on the already resumed stage cast
            SaveSystem.UnblockSaving(this);
        }

        // Editor and development builds only (project rule: runtime debug tools gate on Debug.isDebugBuild).
        private static bool DevSkipAllowed => Application.isEditor || Debug.isDebugBuild;

        [ContextMenu("Dev: Skip Arrival")]
        private void DevSkip()
        {
            if (!DevSkipAllowed || !Application.isPlaying || !_valid) return;
            if (!CursorManager.IsLocked)
            {
                GameLog.Warn(TAG, "dev skip refused: close the open dialogue / menu first");
                return;
            }
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

        // Starts part 2 at once: Warden and Tam on their routines, guards at the stage, player at the watch spot.
        [ContextMenu("Dev: Skip to Hut Escort")]
        private void DevSkipToHutEscort()
        {
            if (!DevSkipAllowed || !Application.isPlaying || !_valid) return;
            WorldStateManager wsm = WorldStateManager.Instance;
            if (!_hutValid || _phase == Phase.Done || (wsm != null && wsm.GetFact(_config.completedFact)))
            {
                GameLog.Warn(TAG, "skip to hut escort: part 2 not set up, or the arrival is already done");
                return;
            }
            if (!CursorManager.IsLocked)
            {
                GameLog.Warn(TAG, "skip to hut escort refused: close the open dialogue / menu first");
                return;
            }
            _player = TargetRegistry.Player;
            if (_player == null) return;
            _playerState = _player.GetComponent<PlayerStateManager>();

            if (IsRunning)
            {
                EndChase();
                ResetEscortState();
                _tentMate.SetRoutineTarget(_tentMateSpot, null, true);
            }
            else TakeOwnership();
            _leg = Leg.Stage;
            _laneIndex = _laneLeft.Count - 1;
            SendGuards(snap: true);
            TeleportPlayer(_watchSpot);
            foreach (NpcRoutine routine in _stageRoutines) routine.Suspend(); // so Resume re-sends their block
            StartHutEscort(snapStageCast: true);
            GameLog.Info(TAG, "skipped to the hut escort (dev)");
        }

        // --- Helpers ---

        private void EnterPhase(Phase phase)
        {
            _phase = phase;
            _phaseTime = 0f;
        }

        private void SendGuards(bool snap)
        {
            _guardLeft.SetRoutineTarget(LaneLeft[_laneIndex], null, snap);
            _guardRight.SetRoutineTarget(LaneRight[_laneIndex], null, snap);
        }

        // A guard counts as arrived when at its lane point or out of the escort (down, dead).
        private bool GuardsArrived() =>
            GuardArrived(_guardLeft, _guardLeftHealth, LaneLeft[_laneIndex]) &&
            GuardArrived(_guardRight, _guardRightHealth, LaneRight[_laneIndex]);

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

        private Vector3 LaneCenter(int index) => (LaneLeft[index].position + LaneRight[index].position) * 0.5f;

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
            Gizmos.color = new Color(0.9f, 0.7f, 0.2f, 0.9f);
            DrawLane(_hutLaneLeft);
            DrawLane(_hutLaneRight);
            if (_hutWatchSpot != null) Gizmos.DrawWireSphere(_hutWatchSpot.position, 0.6f);
            if (_tentMateSpot != null) Gizmos.DrawWireCube(_tentMateSpot.position + Vector3.up, new Vector3(0.5f, 2f, 0.5f));
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
