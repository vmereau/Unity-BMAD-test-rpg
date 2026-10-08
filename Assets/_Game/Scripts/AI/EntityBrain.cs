using _Game.ScriptableObjects.Entities;
using Game.Animations;
using Game.Combat;
using Game.Core;
using Game.Dialogue;
using Game.Factions;
using Game.NPC;
using Game.Stealth;
using Game.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Game.AI
{
    /// <summary>
    /// Generic entity state machine: Idle / Patrolling → (Suspicious) → Warning → Engaging ⇄ Attacking →
    /// (Searching) → Idle / Patrolling; Dead from any state.
    /// Stealth targets (the player) are acquired through the optional <see cref="EntityPerception"/> awareness
    /// meter (vision cone + line of sight); other hostiles through the TargetRegistry radius check. Without an
    /// active perception, every hostile is acquired by radius and Suspicious / Searching never happen.
    /// Taking damage while not engaged makes the entity engage the closest hostile. Implements
    /// <see cref="ISneakAttackTarget"/> (unaware = Idle / Patrolling / Suspicious and not fully aware).
    /// A non-hostile stealth target perceived in witness mode (neutral NPC with a <see cref="WitnessProfileSO"/>
    /// spotting the sneaking player) leads to Idle / Patrolling → (Suspicious) → Watching (stop, face the player, one
    /// warning speech bubble per cooldown) → back to Idle / Patrolling. Watching is neither combat nor unaware; a
    /// hostile spotted (radius scan) or a hostile hit while watching still triggers the normal combat reaction.
    /// Theft: a non-hostile witness that sees the player steal (<c>OnTheftCommitted</c>, cone + LOS) shouts an alert
    /// and goes Pursuing (non-combat chase); the first witness to catch the thief claims the incident
    /// (<see cref="TheftPursuitRegistry"/>) and goes Confronting (forced scold dialogue), the others give up. Escape
    /// (no LOS / too far / too long) → back to Idle / Patrolling.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EntityBrain : MonoBehaviour, ICombatStateProvider, ISneakAttackTarget
    {
        private const string TAG = "[AI]";

        private enum EntityState { Idle, Patrolling, Suspicious, Warning, Engaging, Attacking, Searching, Watching, Pursuing, Confronting, Dead }

        private const string DEFAULT_SCOLD_LINE = "Hey! What do you think you're doing?";

        private static readonly string[] STATE_NAMES = System.Enum.GetNames(typeof(EntityState));

        public bool IsInCombat { get; private set; }

        /// <summary>Current state name for the stealth debug views (no per-frame allocation).</summary>
        public string DebugStateName => STATE_NAMES[(int)_state];

        // Single writer for combat state. Keeps the readable flag and the animator in lockstep,
        // replacing the previously-scattered _animationDriver?.SetInCombat(...) calls. Idempotent.
        private void SetCombatState(bool inCombat)
        {
            if (IsInCombat == inCombat) return;
            IsInCombat = inCombat;
            _animationDriver?.SetInCombat(inCombat);
        }

        [SerializeField] private PersistentID _persistentID;
        [SerializeField] private Transform[] _waypoints;
        [FormerlySerializedAs("_animationBridge")]
        [FormerlySerializedAs("_entityAnimator")]
        [SerializeField] private AIAnimationDriver _animationDriver;
        [Tooltip("Resolves attack hits from animation-driven hit windows.")]
        [SerializeField] private EntityMeleeAttacker _meleeAttacker;

        [Header("Behavior")]
        [Tooltip("Skip the warning telegraph and engage the instant a target is detected.")]
        [SerializeField] private bool _engageImmediately = false;

        [Tooltip("This entity's faction membership — drives detection & engagement decisions.")]
        [SerializeField] private FactionMember _selfFactionMember;
        [Tooltip("Seconds between target-acquisition registry scans while Idle/Patrolling. Bounds per-frame scan cost.")]
        [SerializeField] private float _targetScanInterval = 0.25f;

        [Header("Perception")]
        [Tooltip("Optional. Vision-cone awareness of stealth targets (the player). Null → radius detection only.")]
        [SerializeField] private EntityPerception _perception;
        [Tooltip("Suspicious / searching tuning. Falls back to the perception's config when null.")]
        [SerializeField] private StealthConfigSO _stealthConfig;

        [Header("Witness")]
        [Tooltip("Raised when a non-hostile witness warns the player (speech bubble).")]
        [SerializeField] private GameEventSO_SpeechBubbleRequest _onSpeechBubbleRequested;
        [Tooltip("Point the speech bubble anchors to (above the name tag).")]
        [SerializeField] private Transform _speechAnchor;
        [Tooltip("Height above the root used when _speechAnchor is unassigned.")]
        [SerializeField] private float _speechAnchorFallbackHeight = 2.8f;

        [Header("Theft")]
        [Tooltip("Listened to — every NPC that sees a theft chases the thief.")]
        [SerializeField] private GameEventSO_TheftCommitted _onTheftCommitted;
        [Tooltip("Raised to confront a caught thief (forced dialogue).")]
        [SerializeField] private GameEventSO_NPCDialogueRequest _onDialogueRequested;

        private NavMeshAgent _agent;
        private EntityHealth _entityHealth;
        private FactionMember _currentTarget;
        private float _targetScanTimer;

        private EntityState _state = EntityState.Idle;
        private int _currentWaypoint;
        private float _waitTimer;
        private float _attackCooldownTimer;
        private float _warningTimer;

        private float _searchTimer;
        private bool _searchArrived;
        private float _previousHealth;
        private bool _healthSubscribed;
        private FactionMember _pendingAttacker;
        private int _pendingAttackerFrame = -1;
        private float _lastDamagedTime = float.NegativeInfinity;

        private Vector3 _idleOrigin;
        private EntityState _disengageState = EntityState.Patrolling;

        private float _watchLostTimer;
        private float _lastWarnTime = float.NegativeInfinity;
        private int _lastBarkIndex = -1;
        private bool _warnedMissingBark;

        /// <summary>Warnings given to the player by this witness (runtime only, not saved) — hook for escalation.</summary>
        public int WarnedCount { get; private set; }

        private int _pursuitTheftId;
        private TheftKind _pursuitKind;
        private float _pursuitElapsed;
        private float _pursuitNoSightTimer;
        private float _pursuitLosTimer;
        private bool _pursuitHasLos;
        private Vector3 _pursuitLastSeen;
        private float _confrontTimer;
        private bool _confrontOpened;
        private int _lastAlertBarkIndex = -1;
        private int _lastScoldBarkIndex = -1;
        private bool _warnedMissingScold;

        /// <summary>Thefts this NPC saw and chased (runtime only, not saved) — hook for future consequences.</summary>
        public int TheftsWitnessed { get; private set; }

        private bool PerceptionActive => _perception != null && _perception.IsActive;
        private StealthConfigSO StealthConfig => _stealthConfig != null ? _stealthConfig : _perception.Config;

        // How long a hit keeps the entity alert for sneak-attack purposes. Without a config: forever.
        private float DamageAlertDuration
        {
            get
            {
                if (_stealthConfig != null) return _stealthConfig.damageAlertDuration;
                if (_perception != null && _perception.Config != null) return _perception.Config.damageAlertDuration;
                return float.PositiveInfinity;
            }
        }

        // Interface refs need the UnityEngine.Object cast so a destroyed health component reads as null.
        private static bool IsLive(FactionMember m) =>
            m != null && m.Damageable != null && (Object)m.Damageable != null && !m.Damageable.IsDead;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
            {
                GameLog.Error(TAG, "NavMeshAgent not found — EntityBrain disabled");
                enabled = false;
                return;
            }

            if (_persistentID.Entity == null)
            {
                GameLog.Error(TAG, $"Entity SO not assigned on {gameObject.name} — EntityBrain disabled");
                enabled = false;
                return;
            }

            _entityHealth = GetComponent<EntityHealth>();
            if (_entityHealth == null)
            {
                GameLog.Error(TAG, "EntityHealth not found on same GameObject — EntityBrain disabled");
                enabled = false;
                return;
            }

            if (_selfFactionMember == null) _selfFactionMember = GetComponent<FactionMember>();
            if (_selfFactionMember == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: FactionMember not found on same GameObject — EntityBrain disabled");
                enabled = false;
                return;
            }

            // Hard switch: damage only flows through hit windows. Missing attacker → attacks still
            // animate but deal no damage (no hitscan fallback). The brain keeps running.
            if (_meleeAttacker == null) _meleeAttacker = GetComponent<EntityMeleeAttacker>();
            // Passive entities (DetectionRange <= 0) never attack — stay silent for them.
            if (_meleeAttacker == null && _persistentID.Entity.DetectionRange > 0f)
                GameLog.Error(TAG, $"{gameObject.name}: no EntityMeleeAttacker — attacks will deal no damage");

            if (_perception == null) _perception = GetComponent<EntityPerception>();

            if (_speechAnchor == null) _speechAnchor = transform.Find("SpeechAnchor");
            if (_speechAnchor == null && WitnessProfileSO.IsEnabled(_persistentID.Entity.WitnessProfile))
                GameLog.Warn(TAG, $"{gameObject.name}: no SpeechAnchor child — witness bubbles use the {_speechAnchorFallbackHeight} m fallback height");
        }

        private void OnEnable()
        {
            if (_onTheftCommitted != null) _onTheftCommitted.AddListener(HandleTheftCommitted);
            if (_entityHealth == null) return; // Awake failed before resolving it
            _previousHealth = _entityHealth.CurrentHealth;
            _entityHealth.HealthChanged += HandleHealthChanged;
            _healthSubscribed = true;
        }

        private void OnDisable()
        {
            if (_onTheftCommitted != null) _onTheftCommitted.RemoveListener(HandleTheftCommitted);
            if (!_healthSubscribed) return; // Guard: Awake may disable before OnEnable runs
            _entityHealth.HealthChanged -= HandleHealthChanged;
            _healthSubscribed = false;
        }

        private void Start()
        {
            // FactionMember.Awake has run by now (all Awakes precede any Start). If it failed to resolve a
            // faction it disabled itself but GetComponent still found it, leaving the brain silently passive —
            // surface that here instead of running forever with no possible target.
            if (_selfFactionMember.Faction == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: FactionMember resolved no faction — brain can never acquire a target. Disabling.");
                enabled = false;
                return;
            }

            // Runtime guard: OnValidate only clamps in-editor. An un-migrated SO with
            // WarningRange >= DetectionRange has an empty warning band, so the entity will
            // always instant-engage and the telegraph silently never fires. Warn once.
            if (!_engageImmediately &&
                _persistentID.Entity.DetectionRange > 0f &&
                _persistentID.Entity.WarningRange >= _persistentID.Entity.DetectionRange)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: WarningRange ({_persistentID.Entity.WarningRange}) >= DetectionRange ({_persistentID.Entity.DetectionRange}) — warning band empty; entity will instant-engage. Check the Entity SO.");
            }

            // ComboHitsMax above what the driver can animate is silently clamped by EntityMeleeAttacker — surface it.
            int maxComboSteps = _animationDriver != null ? _animationDriver.MaxComboSteps : 1;
            if (_persistentID.Entity.ComboHitsMax > maxComboSteps)
                GameLog.Warn(TAG, $"{gameObject.name}: ComboHitsMax ({_persistentID.Entity.ComboHitsMax}) exceeds the animation driver's MaxComboSteps ({maxComboSteps}) — combos clamped. Check the Entity SO.");

            if (_waypoints == null || _waypoints.Length == 0)
            {
                GameLog.Info(TAG, $"{gameObject.name}: No waypoints assigned — entering Idle wander");
                TransitionToIdle(transform.position);
                return;
            }
            // Initialize to last index so the first AdvanceToNextWaypoint() lands at index 0.
            _currentWaypoint = _waypoints.Length - 1;
            AdvanceToNextWaypoint();
            _state = EntityState.Patrolling;
        }

        private void Update()
        {
            if (_entityHealth.IsDead && _state != EntityState.Dead)
            {
                TransitionToDead();
                return;
            }

            if (_state != EntityState.Dead && PerceptionActive)
                _perception.Tick(Time.deltaTime, IsAlertedOnStealthTarget());

            switch (_state)
            {
                case EntityState.Idle:       HandleIdle();       break;
                case EntityState.Patrolling: HandlePatrol();     break;
                case EntityState.Suspicious: HandleSuspicious(); break;
                case EntityState.Warning:    HandleWarning();    break;
                case EntityState.Engaging:   HandleEngage();     break;
                case EntityState.Attacking:  HandleAttack();     break;
                case EntityState.Searching:  HandleSearching();  break;
                case EntityState.Watching:   HandleWatching();   break;
                case EntityState.Pursuing:   HandlePursuing();   break;
                case EntityState.Confronting: HandleConfronting(); break;
                case EntityState.Dead:       HandleDead();       break;
            }

            HandleCooldowns();
            if (_animationDriver != null) _animationDriver.DriveLocomotion(_agent);
        }

        private void HandleCooldowns()
        {
            if (_attackCooldownTimer > 0f)
                _attackCooldownTimer = Mathf.Max(0f, _attackCooldownTimer - Time.deltaTime);
        }

        // --- Shared detection helpers ---

        // Throttled wrapper for the Idle/Patrol acquisition path so a crowd of idle entities does not
        // each run a full registry scan every frame. Bounded to one scan per _targetScanInterval.
        private bool TryAcquireTargetThrottled()
        {
            _targetScanTimer -= Time.deltaTime;
            if (_targetScanTimer > 0f) return false;
            _targetScanTimer = _targetScanInterval;
            return TryAcquireTarget();
        }

        // Queries the registry for the closest hostile within detection range and caches it.
        // With an active perception, stealth targets are skipped — they are acquired through awareness instead.
        private bool TryAcquireTarget()
        {
            if (_selfFactionMember.Faction == null) return false;
            _currentTarget = TargetRegistry.FindClosestHostile(
                _selfFactionMember.Faction,
                transform.position,
                _persistentID.Entity.DetectionRange,
                skipStealthTargets: PerceptionActive);
            return _currentTarget != null;
        }

        // Idle / Patrolling detection: full awareness → react; partial awareness → Suspicious; else radius scan.
        private bool TryDetectFromNonCombat()
        {
            if (PerceptionActive)
            {
                if (TryTakePerceptionTarget()) { RespondToDetectedTarget(); return true; }
                if (_perception.IsSuspicious) { TransitionToSuspicious(); return true; }
            }
            if (TryAcquireTargetThrottled()) { RespondToDetectedTarget(); return true; }
            return false;
        }

        // Fully aware of a live stealth target → it becomes the current target.
        private bool TryTakePerceptionTarget()
        {
            if (!PerceptionActive || !_perception.IsFullyAware) return false;
            FactionMember target = _perception.Target;
            if (!IsLive(target)) return false;
            _currentTarget = target;
            return true;
        }

        private bool IsAlertedState() =>
            _state == EntityState.Warning || _state == EntityState.Engaging || _state == EntityState.Attacking;

        // True while alerted on (or watching) a stealth target: perception tracks it 360° and holds awareness at 1.
        private bool IsAlertedOnStealthTarget() =>
            (IsAlertedState() || _state == EntityState.Watching) &&
            _currentTarget != null && _currentTarget.StealthTarget != null;

        private bool IsTrackingPerceptionTarget() =>
            PerceptionActive && _currentTarget != null && _currentTarget == _perception.Target;

        // Alerted on the perception target but it has been out of sight for LoseSightTime.
        private bool HasLostSightOfTarget() =>
            IsTrackingPerceptionTarget() && _perception.TimeSinceSeen >= _persistentID.Entity.LoseSightTime;

        private bool HasValidTarget() =>
            _currentTarget != null && _currentTarget.Damageable != null && !_currentTarget.Damageable.IsDead;

        // --- State handlers ---

        private void HandleIdle()
        {
            if (TryDetectFromNonCombat()) return;

            if (_agent.pathPending) return;

            _persistentID.Entity.ExecuteIdle(_agent, _idleOrigin, ref _waitTimer);
        }

        private void HandlePatrol()
        {
            if (TryDetectFromNonCombat()) return;

            if (_agent.pathPending) return;

            if (_agent.remainingDistance <= _persistentID.Entity.WaypointArrivalThreshold)
            {
                _waitTimer -= Time.deltaTime;
                if (_waitTimer <= 0f)
                    AdvanceToNextWaypoint();
                else
                    _agent.isStopped = true;
            }
        }

        // Decide how to react to first contact: watch (non-hostile witness), instant engage,
        // cross-inner-ring engage, or warn.
        private void RespondToDetectedTarget()
        {
            if (IsWitnessTarget()) { TransitionToWatching(); return; }
            if (_engageImmediately) { TransitionToEngaging(); return; }
            float dist = Vector3.Distance(transform.position, _currentTarget.Transform.position);
            if (dist <= _persistentID.Entity.WarningRange) TransitionToEngaging();
            else TransitionToWarning();
        }

        // Partially aware: stand still and turn toward where the target was last seen. No combat state.
        private void HandleSuspicious()
        {
            if (!PerceptionActive) { ResumeNonCombat(); return; }
            if (TryTakePerceptionTarget()) { RespondToDetectedTarget(); return; }
            if (TryAcquireTargetThrottled()) { RespondToDetectedTarget(); return; } // non-stealth hostile
            if (_perception.Awareness <= 0f)
            {
                GameLog.Info(TAG, $"{gameObject.name} no longer suspicious — resuming");
                ResumeNonCombat();
                return;
            }
            if (_perception.HasLastSeenPosition)
                FacePoint(_perception.LastSeenPosition, StealthConfig.suspiciousTurnSpeed);
        }

        private void HandleWarning()
        {
            if (!HasValidTarget()) { CancelWarning(); return; }
            if (HasLostSightOfTarget()) { TransitionToSearching(); return; }
            float dist = Vector3.Distance(transform.position, _currentTarget.Transform.position);
            if (dist > _persistentID.Entity.DetectionRange) { CancelWarning(); return; }      // target escaped
            if (dist <= _persistentID.Entity.WarningRange) { TransitionToEngaging(); return; } // crossed inner ring
            FaceTarget();
            _warningTimer -= Time.deltaTime;
            if (_warningTimer <= 0f) TransitionToEngaging();
        }

        // Y-only rotation to face the target. Manual because the agent is stopped while warning,
        // so NavMeshAgent auto-rotation does not apply.
        private void FaceTarget() =>
            FacePoint(_currentTarget.Transform.position, _persistentID.Entity.WarningTurnSpeed);

        private void FacePoint(Vector3 point, float degPerSecond)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degPerSecond * Time.deltaTime);
        }

        private void HandleEngage()
        {
            if (!HasValidTarget())
            {
                GameLog.Warn(TAG, "Target lost — disengaging");
                DisengageFromCombat();
                return;
            }

            if (HasLostSightOfTarget()) { TransitionToSearching(); return; }

            float distToTarget = Vector3.Distance(transform.position, _currentTarget.Transform.position);

            if (distToTarget > _persistentID.Entity.DisengageRange)
            {
                DisengageFromCombat();
                return;
            }

            if (distToTarget <= _persistentID.Entity.AttackRange)
            {
                TransitionToAttacking();
                return;
            }

            // Out of sight (before the lose-sight timeout): chase where the target was last seen.
            bool chaseLastSeen = IsTrackingPerceptionTarget() && !_perception.CanSeeTarget && _perception.HasLastSeenPosition;
            _agent.SetDestination(chaseLastSeen ? _perception.LastSeenPosition : _currentTarget.Transform.position);
        }

        private void HandleAttack()
        {
            if (!HasValidTarget())
            {
                DisengageFromCombat();
                return;
            }

            if (HasLostSightOfTarget()) { TransitionToSearching(); return; }

            float distToTarget = Vector3.Distance(transform.position, _currentTarget.Transform.position);

            if (distToTarget > _persistentID.Entity.DisengageRange)
            {
                DisengageFromCombat();
                return;
            }

            if (distToTarget > _persistentID.Entity.AttackRange)
            {
                TransitionToEngaging();
                return;
            }

            // Agent is stopped while attacking, so NavMeshAgent auto-rotation does not apply — hits are
            // spatial now, so keep the attack pointed at the target.
            FaceTarget();

            if (_meleeAttacker != null && _meleeAttacker.IsInAttackState) return; // never cut an attack/combo mid-swing
            if (_attackCooldownTimer > 0f) return;

            ExecuteAttack();
        }

        // Alerted but lost the target: walk to the last seen position, look around, then stand down.
        private void HandleSearching()
        {
            if (!PerceptionActive) { EndSearch(); return; }
            if (TryTakePerceptionTarget()) { TransitionToEngaging(); return; } // re-sighted: no warning telegraph
            if (TryAcquireTargetThrottled()) { TransitionToEngaging(); return; } // non-stealth hostile

            if (!_searchArrived)
            {
                if (_agent.pathPending) return;
                bool arrived = _agent.pathStatus == NavMeshPathStatus.PathInvalid ||
                               _agent.remainingDistance <= _persistentID.Entity.WaypointArrivalThreshold;
                if (!arrived) return;
                _searchArrived = true;
                _agent.isStopped = true;
                GameLog.Info(TAG, $"{gameObject.name} reached the last seen position — searching");
            }

            transform.Rotate(0f, StealthConfig.searchTurnSpeed * Time.deltaTime, 0f);
            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0f) EndSearch();
        }

        private void EndSearch()
        {
            SetCombatState(false);
            GameLog.Info(TAG, $"{gameObject.name} gave up the search");
            ResumeNonCombat();
        }

        // The current target is the non-hostile stealth target the perception is witnessing.
        private bool IsWitnessTarget() =>
            PerceptionActive && _perception.IsWitnessing && _currentTarget != null && _currentTarget == _perception.Target;

        // Witness: stand still facing the player until they leave WitnessWatchRange or stay out of sight too long.
        private void HandleWatching()
        {
            if (!PerceptionActive || !IsLive(_currentTarget) || !_perception.IsWitnessing) { EndWatching(); return; }

            // A hostile (non-stealth, radius scan) showing up takes over: normal warning / engage reaction.
            FactionMember watched = _currentTarget;
            if (TryAcquireTargetThrottled())
            {
                GameLog.Info(TAG, $"{gameObject.name} stops watching — hostile {_currentTarget.Transform.name} spotted");
                _perception.ResetPerception();
                RespondToDetectedTarget();
                return;
            }
            _currentTarget = watched; // TryAcquireTarget clears it when nothing is found

            Vector3 toTarget = _currentTarget.Transform.position - transform.position;
            toTarget.y = 0f;
            float watchRange = _perception.WitnessWatchRange;
            if (toTarget.sqrMagnitude > watchRange * watchRange) { EndWatching(); return; }

            if (_perception.CanSeeTarget)
            {
                _watchLostTimer = 0f;
            }
            else
            {
                _watchLostTimer += Time.deltaTime;
                if (_watchLostTimer >= StealthConfig.witnessLoseSightTime) { EndWatching(); return; }
            }
            FacePoint(_currentTarget.Transform.position, StealthConfig.witnessTurnSpeed);
        }

        private void EndWatching()
        {
            _currentTarget = null;
            if (_perception != null) _perception.ResetPerception();
            GameLog.Info(TAG, $"{gameObject.name} stops watching — resuming");
            ResumeNonCombat();
        }

        // One warning bubble per witnessWarnCooldown; detections inside the cooldown watch silently.
        private void TryWarn()
        {
            if (Time.time - _lastWarnTime < StealthConfig.witnessWarnCooldown) return;
            _lastWarnTime = Time.time;

            WitnessProfileSO profile = _persistentID.Entity.WitnessProfile;
            BarkSetSO barks = profile != null ? profile.Barks : null;
            string line = barks != null ? barks.GetRandomLine(ref _lastBarkIndex) : null;
            if (string.IsNullOrEmpty(line) || _onSpeechBubbleRequested == null)
            {
                if (!_warnedMissingBark)
                {
                    _warnedMissingBark = true;
                    GameLog.Warn(TAG, $"{gameObject.name}: witness warning has no bark line or no speech bubble channel — watching silently");
                }
                return;
            }

            WarnedCount++; // only warnings the player actually saw count toward escalation
            RaiseSpeechBubble(line, StealthConfig.witnessBubbleDuration, StealthConfig.witnessBubblePriority);
            GameLog.Info(TAG, $"{gameObject.name} warns the player (#{WarnedCount})");
        }

        private void RaiseSpeechBubble(string line, float duration, int priority)
        {
            _onSpeechBubbleRequested.Raise(new SpeechBubbleRequest
            {
                speaker = _speechAnchor != null ? _speechAnchor : transform,
                text = line,
                duration = duration,
                priority = priority,
                anchorHeight = _speechAnchor != null ? 0f : _speechAnchorFallbackHeight,
            });
        }

        // --- Theft ---

        // Every NPC with a witness profile that sees the theft right now chases the thief (non-combat).
        private void HandleTheftCommitted(TheftCommittedData data)
        {
            if (_state != EntityState.Idle && _state != EntityState.Patrolling && _state != EntityState.Suspicious &&
                _state != EntityState.Watching && _state != EntityState.Pursuing) return;
            if (!PerceptionActive || !_perception.CanWitness) return;

            FactionMember thief = data.thief != null ? data.thief.GetComponentInParent<FactionMember>() : null;
            if (!IsLive(thief)) return;
            // Hostiles don't police theft (their own hostile behaviour already applies).
            if (_selfFactionMember.Faction != null && thief.Faction != null &&
                _selfFactionMember.Faction.IsHostileTo(thief.Faction)) return;
            if (!_perception.CanSeeTheft(thief)) return;

            if (_state == EntityState.Pursuing)
            {
                // Already chasing: follow the newest incident and restart the give-up timers.
                TheftsWitnessed++;
                StartPursuitTimers(thief, data);
                GameLog.Info(TAG, $"{gameObject.name} saw theft #{data.theftId} — still chasing");
                return;
            }
            TransitionToPursuing(thief, data);
        }

        private void StartPursuitTimers(FactionMember thief, TheftCommittedData data)
        {
            _currentTarget = thief;
            _pursuitTheftId = data.theftId;
            _pursuitKind = data.kind;
            _pursuitElapsed = 0f;
            _pursuitNoSightTimer = 0f;
            _pursuitLosTimer = 0f;
            _pursuitHasLos = true;
            _pursuitLastSeen = thief.Transform.position;
        }

        private void TransitionToPursuing(FactionMember thief, TheftCommittedData data)
        {
            // Suspicious / Watching already captured the state to resume.
            if (_state == EntityState.Idle || _state == EntityState.Patrolling)
                _disengageState = _state;
            _state = EntityState.Pursuing;
            _perception.ResetPerception();
            StartPursuitTimers(thief, data);
            _agent.isStopped = false;
            _agent.speed = _persistentID.Entity.EngageSpeed;
            _agent.stoppingDistance = StealthConfig.theftCatchDistance * 0.8f;
            _agent.SetDestination(thief.Transform.position);
            TheftsWitnessed++;
            // No SetCombatState: chasing a thief is not combat (dialogue stays possible).

            WitnessProfileSO profile = _persistentID.Entity.WitnessProfile;
            BarkSetSO barks = profile != null ? profile.TheftAlertBarks : null;
            string line = barks != null ? barks.GetRandomLine(ref _lastAlertBarkIndex) : null;
            if (!string.IsNullOrEmpty(line) && _onSpeechBubbleRequested != null)
                RaiseSpeechBubble(line, StealthConfig.theftBubbleDuration, StealthConfig.theftAlertBubblePriority);

            GameLog.Info(TAG, $"{gameObject.name} saw theft #{data.theftId} — chasing (#{TheftsWitnessed})");
        }

        private void HandlePursuing()
        {
            if (!IsLive(_currentTarget)) { EndPursuit("thief lost"); return; }
            if (TheftPursuitRegistry.IsClaimed(_pursuitTheftId)) { EndPursuit("another witness caught the thief"); return; }

            // A hostile (non-stealth, radius scan) showing up takes over: normal warning / engage reaction.
            FactionMember thief = _currentTarget;
            if (TryAcquireTargetThrottled())
            {
                GameLog.Info(TAG, $"{gameObject.name} stops chasing the thief — hostile {_currentTarget.Transform.name} spotted");
                _perception.ResetPerception();
                RespondToDetectedTarget();
                return;
            }
            _currentTarget = thief; // TryAcquireTarget clears it when nothing is found

            StealthConfigSO config = StealthConfig;
            float dt = Time.deltaTime;
            Vector3 targetPos = _currentTarget.Transform.position;

            _pursuitLosTimer -= dt;
            if (_pursuitLosTimer <= 0f)
            {
                _pursuitLosTimer = config.losCheckInterval;
                _pursuitHasLos = _perception.HasLineOfSightTo(_perception.GetVisibilityPoint(_currentTarget));
            }
            if (_pursuitHasLos)
            {
                _pursuitNoSightTimer = 0f;
                _pursuitLastSeen = targetPos;
            }
            else
            {
                _pursuitNoSightTimer += dt;
            }
            _pursuitElapsed += dt;

            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (TheftDetection.ShouldGiveUp(_pursuitNoSightTimer, config.theftChaseLoseSightTime, distance,
                    config.theftChaseMaxDistance, _pursuitElapsed, config.theftChaseMaxDuration))
            {
                EndPursuit("thief escaped");
                return;
            }

            if (TheftDetection.CanCatch(distance, config.theftCatchDistance, CursorManager.IsLocked) &&
                TheftPursuitRegistry.TryClaim(_pursuitTheftId))
            {
                TransitionToConfronting();
                return;
            }

            _agent.SetDestination(_pursuitHasLos ? targetPos : _pursuitLastSeen);
        }

        private void TransitionToConfronting()
        {
            _state = EntityState.Confronting;
            _agent.isStopped = true;
            _confrontTimer = 0f;
            _confrontOpened = false;

            WitnessProfileSO profile = _persistentID.Entity.WitnessProfile;
            BarkSetSO set = null;
            if (profile != null)
                set = _pursuitKind == TheftKind.Item && profile.ItemTheftScoldBarks != null
                    ? profile.ItemTheftScoldBarks
                    : profile.TheftScoldBarks;
            string line = set != null ? set.GetRandomLine(ref _lastScoldBarkIndex) : null;
            if (string.IsNullOrEmpty(line))
            {
                if (!_warnedMissingScold)
                {
                    _warnedMissingScold = true;
                    GameLog.Warn(TAG, $"{gameObject.name}: no theft scold line on the witness profile — using the default line");
                }
                line = DEFAULT_SCOLD_LINE;
            }

            if (_onDialogueRequested == null)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: _onDialogueRequested not assigned — cannot confront the thief");
                EndPursuit("no dialogue channel");
                return;
            }

            GameLog.Info(TAG, $"{gameObject.name} caught the thief (theft #{_pursuitTheftId}) — confronting");
            _onDialogueRequested.Raise(new NPCDialogueRequestData
            {
                npcName = _persistentID.Entity.entityName,
                forcedLine = line,
            });
        }

        // Stands facing the thief while the scold dialogue is open; resumes once it closes (cursor locked again),
        // or after theftConfrontOpenTimeout if it never opened (e.g. another dialogue was already open).
        private void HandleConfronting()
        {
            if (!IsLive(_currentTarget)) { EndPursuit("thief lost"); return; }
            FacePoint(_currentTarget.Transform.position, StealthConfig.witnessTurnSpeed);
            _confrontTimer += Time.deltaTime;
            if (!CursorManager.IsLocked)
                _confrontOpened = true;
            else if (_confrontOpened || _confrontTimer >= StealthConfig.theftConfrontOpenTimeout)
                EndPursuit("scolded the thief");
        }

        private void EndPursuit(string reason)
        {
            _currentTarget = null;
            if (_perception != null) _perception.ResetPerception();
            GameLog.Info(TAG, $"{gameObject.name} ends the theft chase — {reason}");
            ResumeNonCombat();
        }

        private void HandleDead()
        {
            // No-op: death animation and ragdoll handled by the AIAnimationDriver.
        }

        // --- Damage reaction ---

        // A hit while not engaged (e.g. from outside the cone) makes the entity engage its attacker.
        // EntityHealth is on this GameObject (same system), so a direct C# event is fine.
        private void HandleHealthChanged(float current, float max)
        {
            bool decreased = current < _previousHealth;
            _previousHealth = current;
            if (!decreased || current <= 0f || _entityHealth.IsDead) return;

            _lastDamagedTime = Time.time; // ends sneak-attack eligibility, even for passive / neutral entities
            FactionMember attacker = _pendingAttackerFrame == Time.frameCount ? _pendingAttacker : null;
            _pendingAttacker = null;

            if (_persistentID.Entity.DetectionRange <= 0f) return; // passive entities never fight back
            if (_state != EntityState.Idle && _state != EntityState.Patrolling && _state != EntityState.Suspicious &&
                _state != EntityState.Searching && _state != EntityState.Warning && _state != EntityState.Watching &&
                _state != EntityState.Pursuing && _state != EntityState.Confronting) return;
            FactionSO faction = _selfFactionMember.Faction;
            if (faction == null) return;

            FactionMember candidate;
            if (attacker != null)
            {
                // Known attacker: fight back only if it is hostile — never redirect onto a bystander.
                if (!IsLive(attacker) || attacker.Faction == null || !faction.IsHostileTo(attacker.Faction)) return;
                candidate = attacker;
            }
            else
            {
                // Unknown source: the hostile stealth target only if it is in sight (never a witnessed, non-hostile
                // one), else the closest radius hostile.
                candidate = PerceptionActive && !_perception.IsWitnessing && _perception.CanSeeTarget && IsLive(_perception.Target)
                    ? _perception.Target
                    : TargetRegistry.FindClosestHostile(faction, transform.position,
                        _persistentID.Entity.DisengageRange, skipStealthTargets: PerceptionActive);
            }
            if (candidate == null) return;

            if (PerceptionActive && candidate.StealthTarget != null) _perception.ForceAware(candidate);
            _currentTarget = candidate;
            GameLog.Info(TAG, $"{gameObject.name} was hit — engaging {candidate.Transform.name}");
            TransitionToEngaging();
        }

        // --- ISneakAttackTarget ---

        public bool IsUnawareOf(GameObject attacker)
        {
            if (_state != EntityState.Idle && _state != EntityState.Patrolling && _state != EntityState.Suspicious)
                return false;
            if (Time.time - _lastDamagedTime < DamageAlertDuration) return false; // already hit recently
            return !(PerceptionActive && _perception.IsFullyAware);
        }

        public void NotifyHitBy(GameObject attacker)
        {
            _pendingAttacker = null;
            if (attacker == null) return;
            if (!attacker.TryGetComponent(out FactionMember member))
                member = attacker.GetComponentInParent<FactionMember>();
            _pendingAttacker = member;
            _pendingAttackerFrame = Time.frameCount;
        }

        // --- Combat ---

        // Range check only starts the attack — damage is resolved by EntityMeleeAttacker when the
        // clip's hit window overlaps a hostile hurtbox.
        private void ExecuteAttack()
        {
            Entity entity = _persistentID.Entity;
            int hits = Random.Range(entity.ComboHitsMin, entity.ComboHitsMax + 1);
            if (_meleeAttacker != null) _meleeAttacker.BeginAttack(entity.AttackDamage, hits);
            _animationDriver?.TriggerAttack();
            _attackCooldownTimer = entity.AttackCooldown;
            GameLog.Info(TAG, $"{gameObject.name} attacks {_currentTarget.Transform.name} ({hits}-hit combo)");
        }

        // --- Movement helpers ---

        private void AdvanceToNextWaypoint()
        {
            if (_waypoints == null || _waypoints.Length == 0) return;

            _currentWaypoint = (_currentWaypoint + 1) % _waypoints.Length;
            _agent.isStopped = false;
            _agent.stoppingDistance = 0f;
            _agent.speed = _persistentID.Entity.BaseSpeed;
            _agent.SetDestination(_waypoints[_currentWaypoint].position);
            _waitTimer = _persistentID.Entity.PatrolWaitTime;
        }

        // --- State transitions ---

        private void TransitionToIdle(Vector3 origin)
        {
            _state = EntityState.Idle;
            _idleOrigin = origin;
            _waitTimer = 0f; // pick a wander target immediately on first HandleIdle tick
            _agent.isStopped = false;
            _agent.stoppingDistance = 0f;
            _agent.speed = _persistentID.Entity.BaseSpeed;
            SetCombatState(false);
            GameLog.Info(TAG, $"{gameObject.name} transitioned to Idle at {origin}");
        }

        private void TransitionToWarning()
        {
            if (_state == EntityState.Idle || _state == EntityState.Patrolling)
                _disengageState = _state;
            _state = EntityState.Warning;
            _agent.isStopped = true;
            _warningTimer = _persistentID.Entity.WarningEngageTime;
            _animationDriver?.SetWarning(true);
            SetCombatState(true);
            GameLog.Info(TAG, $"{gameObject.name} detected target — warning");
        }

        private void CancelWarning()
        {
            _animationDriver?.SetWarning(false);
            SetCombatState(false);
            // Target escaped the detection range: drop the held awareness, otherwise Idle would re-detect at once.
            if (_perception != null) _perception.ResetPerception();
            GameLog.Info(TAG, $"{gameObject.name} lost target during warning — standing down");
            if (_disengageState == EntityState.Idle) TransitionToIdle(_idleOrigin);
            else TransitionToPatrol();
        }

        private void TransitionToEngaging()
        {
            _animationDriver?.SetWarning(false);
            SetCombatState(true);
            // Only capture return state from non-combat states. Warning→Engaging and Attacking→Engaging
            // both preserve the disengage state already captured on the original Idle/Patrol entry.
            if (_state == EntityState.Idle || _state == EntityState.Patrolling)
                _disengageState = _state;
            _state = EntityState.Engaging;
            _agent.isStopped = false;
            _agent.stoppingDistance = _persistentID.Entity.EngageStoppingDistance;
            _agent.speed = _persistentID.Entity.EngageSpeed;
            _agent.SetDestination(_currentTarget.Transform.position);
            GameLog.Info(TAG, $"{gameObject.name} engaged {_currentTarget.Transform.name}");
        }

        private void TransitionToAttacking()
        {
            _state = EntityState.Attacking;
            _agent.isStopped = true;
            SetCombatState(true);
            GameLog.Info(TAG, $"{gameObject.name} entering attack range — switching to Attacking");
        }

        private void TransitionToSuspicious()
        {
            if (_state == EntityState.Idle || _state == EntityState.Patrolling)
                _disengageState = _state;
            _state = EntityState.Suspicious;
            _agent.isStopped = true;
            GameLog.Info(TAG, $"{gameObject.name} is suspicious ({_perception.Awareness * 100f:0}% aware)");
        }

        // Non-hostile witness spotted the sneaking player: stop and watch. No combat state (dialogue stays open).
        private void TransitionToWatching()
        {
            if (_state == EntityState.Idle || _state == EntityState.Patrolling)
                _disengageState = _state;
            _state = EntityState.Watching;
            _agent.isStopped = true;
            _watchLostTimer = 0f;
            GameLog.Info(TAG, $"{gameObject.name} is watching {_currentTarget.Transform.name}");
            TryWarn();
        }

        private void TransitionToSearching()
        {
            _animationDriver?.SetWarning(false);
            SetCombatState(true); // still alert while searching
            if (_meleeAttacker != null) _meleeAttacker.EndAttack();
            _state = EntityState.Searching;
            _currentTarget = null;
            _agent.isStopped = false;
            _agent.stoppingDistance = 0f;
            _agent.speed = _persistentID.Entity.EngageSpeed;
            _searchTimer = _persistentID.Entity.SearchDuration;
            Vector3 destination = _perception.HasLastSeenPosition ? _perception.LastSeenPosition : transform.position;
            // Unreachable last seen position → search from where we stand.
            _searchArrived = !_agent.isOnNavMesh || !_agent.SetDestination(destination);
            if (_searchArrived) _agent.isStopped = true;
            _perception.SetAwareness(StealthConfig.searchStartAwareness);
            GameLog.Info(TAG, $"{gameObject.name} lost sight of its target — searching last seen position");
        }

        // Back to the pre-alert behaviour, resuming the current waypoint rather than skipping it.
        private void ResumeNonCombat()
        {
            SetCombatState(false);
            if (_disengageState == EntityState.Idle || _waypoints == null || _waypoints.Length == 0)
            {
                TransitionToIdle(_idleOrigin);
                return;
            }
            _state = EntityState.Patrolling;
            _agent.isStopped = false;
            _agent.stoppingDistance = 0f;
            _agent.speed = _persistentID.Entity.BaseSpeed;
            _agent.SetDestination(_waypoints[_currentWaypoint].position);
            _waitTimer = _persistentID.Entity.PatrolWaitTime;
            GameLog.Info(TAG, $"{gameObject.name} resumed patrol to waypoint {_currentWaypoint}");
        }

        private void TransitionToPatrol()
        {
            _state = EntityState.Patrolling;
            SetCombatState(false);
            AdvanceToNextWaypoint();
            GameLog.Info(TAG, $"{gameObject.name} returned to patrol");
        }

        private void TransitionToDead()
        {
            _animationDriver?.SetWarning(false);
            SetCombatState(false);
            _state = EntityState.Dead;
            // Guarded: a corpse restored from a save may have an agent that is off the NavMesh.
            if (_agent.isActiveAndEnabled && _agent.isOnNavMesh) _agent.isStopped = true;
            if (_meleeAttacker != null) _meleeAttacker.EndAttack();
            if (_perception != null) _perception.ResetPerception();
            GameLog.Info(TAG, $"{gameObject.name} transitioned to Dead state");
        }

        private void DisengageFromCombat()
        {
            _currentTarget = null;
            SetCombatState(false);
            if (_meleeAttacker != null) _meleeAttacker.EndAttack();
            if (_perception != null) _perception.ResetPerception();
            if (_disengageState == EntityState.Idle)
            {
                GameLog.Info(TAG, $"{gameObject.name} disengaged — resuming Idle at origin");
                TransitionToIdle(_idleOrigin);
            }
            else
            {
                GameLog.Info(TAG, $"{gameObject.name} disengaged — returning to patrol");
                TransitionToPatrol();
            }
        }
    }
}
