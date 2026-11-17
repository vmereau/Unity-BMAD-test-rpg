using _Game.ScriptableObjects.Entities;
using Game.Core;
using Game.World;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Moment-to-moment combat movement for an engaged entity (spec-ai-combat-tactics-foundation). Driven by
    /// <see cref="EntityBrain"/> while Engaging / Attacking: every <see cref="CombatStyleSO.decisionInterval"/> it picks an
    /// action with <see cref="CombatTacticsRules.Decide"/> — Approach, Circle (strafe on a ring around the target, facing
    /// it), BackOff, or Attack (close in, holding an <see cref="AttackTokenRegistry"/> token). The brain keeps its states
    /// and only swings once <see cref="WantsAttackOn"/>. Inactive without a <see cref="CombatStyleSO"/> on the Entity SO.
    /// With a bow in the loadout (spec-ai-ranged-combat-style) it also picks the weapon (<see cref="CombatTacticsRules.WantsRanged"/>
    /// → <see cref="NpcArmament.SetRanged"/>) and, with the bow, keeps the preferred range and shoots (<see cref="EntityArcher"/>).
    /// </summary>
    public class EntityCombatTactics : MonoBehaviour
    {
        private const string TAG = "[AI]";
        // Circle destination leads the strafe by this many decision intervals, so the agent never arrives and stops
        // between two decisions.
        private const float CIRCLE_LEAD_INTERVALS = 2f;

        [SerializeField] private PersistentID _persistentID;

        private NavMeshAgent _agent;
        private EntityHealth _health;
        private EntityPotionDrinker _drinker; // optional: the Drink action (spec-potion-drink-animation)
        private EntityArcher _archer;         // optional: the Shoot action (spec-ai-ranged-combat-style)
        private NpcArmament _armament;        // optional: weapon switching for the ranged mode
        private float _lastModeSwitchTime = float.NegativeInfinity;
        private FactionMember _target;
        private float _decisionTimer;
        private float _circleElapsed;
        private float _circleTime;
        private float _attackElapsed; // seconds going for the current attack (token held) without swinging
        private int _circleDirection = 1;
        private bool _drinkPending;          // backing off to drink
        private float _drinkBackOffElapsed;  // seconds backed off for the pending drink
        private bool _warnedNoConfig;

        /// <summary>Usable: enabled, with an agent and a combat style on the Entity SO.</summary>
        public bool IsAvailable => isActiveAndEnabled && _agent != null && Style != null;

        /// <summary>Currently driving movement (between the first <see cref="Tick"/> and <see cref="End"/>).</summary>
        public bool IsActive { get; private set; }

        public CombatAction CurrentAction { get; private set; } = CombatAction.Approach;

        /// <summary>Going for an attack with a token on <paramref name="target"/> — the brain may enter Attacking in range.</summary>
        public bool WantsAttackOn(FactionMember target) =>
            IsActive && target != null && target == _target && CurrentAction == CombatAction.Attack
            && AttackTokenRegistry.Holds(this, _target);

        private CombatStyleSO Style => _persistentID != null && _persistentID.Entity != null ? _persistentID.Entity.CombatStyle : null;

        // No config → no cap (warned once).
        private int TokenCapacity
        {
            get
            {
                if (_health != null && _health.CombatConfig != null) return _health.CombatConfig.maxAttackersPerTarget;
                if (!_warnedNoConfig) GameLog.Warn(TAG, $"{gameObject.name}: no CombatConfigSO on EntityHealth — attack tokens uncapped");
                _warnedNoConfig = true;
                return int.MaxValue;
            }
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<EntityHealth>();
            _drinker = GetComponent<EntityPotionDrinker>();
            _archer = GetComponent<EntityArcher>();
            _armament = GetComponent<NpcArmament>();
            if (_persistentID == null) _persistentID = GetComponent<PersistentID>();
        }

        private void OnDisable() => End();

        /// <summary>
        /// One frame of tactics against <paramref name="target"/> at <paramref name="distance"/> (the brain's distance). Decides at the
        /// style's interval, moves the agent and faces the target every frame. <paramref name="attackReady"/>: cooldown
        /// elapsed, weapon in hand, no attack state playing.
        /// </summary>
        public void Tick(FactionMember target, float distance, bool attackReady)
        {
            CombatStyleSO style = Style;
            if (style == null || _agent == null || target == null) return;

            if (!IsActive || target != _target) Begin(target, style);

            // Drinking (spec-potion-drink-animation): stand still facing the target until the drink ends or is interrupted.
            if (CurrentAction == CombatAction.Drink)
            {
                if (_drinker != null && _drinker.IsDrinking)
                {
                    HoldStill();
                    FaceTarget();
                    return;
                }
                StartCircling(style);
                CurrentAction = CombatAction.Circle;
                _decisionTimer = 0f;
                if (_agent.isActiveAndEnabled && _agent.isOnNavMesh) _agent.isStopped = false;
            }

            // Shooting (spec-ai-ranged-combat-style): stand still facing the target until the bow is lowered. A drink wish
            // (checked at the decision interval) ends the aim — Drink outranks Shoot; the next decision drinks.
            if (CurrentAction == CombatAction.Shoot)
            {
                if (_archer != null && _archer.IsShooting)
                {
                    _decisionTimer -= Time.deltaTime;
                    if (_decisionTimer <= 0f)
                    {
                        _decisionTimer = style.decisionInterval;
                        if (WantsDrinkNow(style)) _archer.End();
                    }
                    if (_archer.IsShooting)
                    {
                        HoldStill();
                        FaceTarget();
                        return;
                    }
                }
                NotifyShotEnded(style);
            }

            if (_drinkPending) _drinkBackOffElapsed += Time.deltaTime;

            // Circle time counts only while actually on the ring (a long run-in doesn't skip the circling).
            if (CurrentAction == CombatAction.Circle || CurrentAction == CombatAction.BackOff) _circleElapsed += Time.deltaTime;

            // Token held but no swing (target unreachable, kiting, held back by avoidance): give it back to the others.
            if (CurrentAction == CombatAction.Attack)
            {
                _attackElapsed += Time.deltaTime;
                if (_attackElapsed >= style.attackCommitTimeout)
                {
                    AttackTokenRegistry.Release(this);
                    StartCircling(style);
                    CurrentAction = CombatAction.Circle;
                    _decisionTimer = 0f;
                }
            }

            _decisionTimer -= Time.deltaTime;
            if (_decisionTimer <= 0f)
            {
                _decisionTimer = style.decisionInterval;
                Decide(style, distance, attackReady);
                ApplyMovement(style);
            }
            else if (CurrentAction == CombatAction.Attack || CurrentAction == CombatAction.Approach)
            {
                // Chasing a moving target: refresh the destination every frame, like the plain Engaging chase.
                ApplyMovement(style);
            }

            if (!_agent.updateRotation) FaceTarget();
        }

        /// <summary>The swing ended: release the token and circle again for a fresh circle time.</summary>
        public void NotifySwingEnded()
        {
            AttackTokenRegistry.Release(this);
            CombatStyleSO style = Style;
            if (style != null) StartCircling(style);
            CurrentAction = CombatAction.Circle;
            _decisionTimer = 0f; // re-decide on the next tick
        }

        // The shot is over (or was refused): token back, circle again.
        private void NotifyShotEnded(CombatStyleSO style)
        {
            AttackTokenRegistry.Release(this);
            StartCircling(style);
            CurrentAction = CombatAction.Circle;
            _decisionTimer = 0f;
            if (_agent.isActiveAndEnabled && _agent.isOnNavMesh) _agent.isStopped = false;
        }

        /// <summary>Stops driving: releases the token and gives the agent its rotation back. Idempotent.</summary>
        public void End()
        {
            if (!IsActive) return; // tokens are only taken while active
            AttackTokenRegistry.Release(this);
            if (_archer != null) _archer.End(); // a shot in progress is dropped (no arrow)
            ReleaseDrinkHold();
            IsActive = false;
            _target = null;
            _drinkPending = false;
            _drinkBackOffElapsed = 0f;
            CurrentAction = CombatAction.Approach;
            if (_agent != null) _agent.updateRotation = true;
        }

        private void Begin(FactionMember target, CombatStyleSO style)
        {
            AttackTokenRegistry.Release(this);
            if (_drinker != null) _drinker.Interrupt("retarget"); // a fresh fight never inherits a drink in progress
            if (_archer != null) _archer.End(); // nor a shot at the old target
            ReleaseDrinkHold();
            _drinkPending = false;
            _drinkBackOffElapsed = 0f;
            _target = target;
            IsActive = true;
            CurrentAction = CombatAction.Approach;
            _decisionTimer = 0f;
            _circleDirection = Random.value < 0.5f ? -1 : 1;
            StartCircling(style);
        }

        private void StartCircling(CombatStyleSO style)
        {
            _circleElapsed = 0f;
            _circleTime = Random.Range(style.circleTimeMin, style.circleTimeMax);
        }

        private void Decide(CombatStyleSO style, float distance, bool attackReady)
        {
            int capacity = TokenCapacity;
            bool hasToken = AttackTokenRegistry.Holds(this, _target);
            bool rangedMode = DecideWeaponMode(style, distance, hasToken);
            var input = new CombatTacticsInput
            {
                Distance = distance,
                AttackRange = _persistentID.Entity.AttackRange,
                RingMargin = style.ringMargin,
                ApproachMargin = style.approachMargin,
                BackOffFraction = style.backOffFraction,
                Aggression = style.aggression,
                CircleElapsed = _circleElapsed,
                CircleTime = _circleTime,
                HasToken = hasToken,
                TokenAvailable = AttackTokenRegistry.IsAvailable(_target, this, capacity),
                AttackReady = attackReady,
                RangedMode = rangedMode,
                PreferredRange = style.preferredRange,
                RangedBackOffDistance = style.rangedBackOffDistance,
            };
            // The clear-shot sweep only when a shot could be taken.
            input.ShotReady = rangedMode && _archer != null && (input.HasToken || input.TokenAvailable)
                              && _archer.CanShootAt(_target);
            FillDrinkInput(ref input, style);

            CombatAction action = CombatTacticsRules.Decide(input, Random.value);
            bool wantsDrink = CombatTacticsRules.WantsDrink(input);
            if (!wantsDrink) _drinkBackOffElapsed = 0f;
            _drinkPending = wantsDrink && action == CombatAction.BackOff;
            if (wantsDrink && input.HasToken) AttackTokenRegistry.Release(this); // drinking outranks the held attack

            if (action == CombatAction.Drink)
            {
                _drinkPending = false;
                _drinkBackOffElapsed = 0f;
                if (_drinker != null && _drinker.TryBegin())
                {
                    CurrentAction = CombatAction.Drink;
                    HoldStill();
                    return;
                }
                action = CombatAction.Circle; // potion gone between the check and the start
            }

            if (action == CombatAction.Attack && !input.HasToken)
            {
                if (AttackTokenRegistry.TryTake(_target, this, capacity)) _attackElapsed = 0f;
                else action = CombatAction.Circle;
            }

            if (action == CombatAction.Shoot)
            {
                // Same token pool as melee swings: at most N threats on one target at once.
                bool hasShotToken = input.HasToken || AttackTokenRegistry.TryTake(_target, this, capacity);
                if (hasShotToken && _archer != null && _archer.BeginShot(_target))
                {
                    CurrentAction = CombatAction.Shoot;
                    HoldStill();
                    return;
                }
                AttackTokenRegistry.Release(this);
                action = CombatAction.Circle;
            }

            if (action == CombatAction.Circle && CurrentAction == CombatAction.Circle
                && CombatTacticsRules.ShouldFlipDirection(Random.value, style.directionChangeChance))
                _circleDirection = -_circleDirection;

            CurrentAction = action;
        }

        // Bow or melee (spec-ai-ranged-combat-style): requests the switch on NpcArmament; returns the requested mode, which
        // drives the spacing even while the switch clips play. Never while holding a token (mid-attack / mid-shot).
        private bool DecideWeaponMode(CombatStyleSO style, float distance, bool hasToken)
        {
            if (_armament == null || _archer == null) return false;
            bool current = _armament.RangedRequested;
            if (!CombatTacticsRules.ShouldDecideWeaponMode(hasToken, _armament.IsChangingWeapon)) return current;
            var modeInput = new RangedModeInput
            {
                CanUseRanged = _archer.CanUseRangedAgainst(_target),
                IsRanged = current,
                Distance = distance,
                MeleeSwitchDistance = style.meleeSwitchDistance,
                RangedSwitchDistance = style.rangedSwitchDistance,
                SinceLastSwitch = Time.time - _lastModeSwitchTime,
                SwitchCooldown = style.weaponSwitchCooldown,
            };
            bool wanted = CombatTacticsRules.WantsRanged(modeInput);
            if (wanted != current)
            {
                _armament.SetRanged(wanted);
                _lastModeSwitchTime = Time.time;
            }
            return _armament.RangedRequested;
        }

        // The drink rule alone (mid-shot check).
        private bool WantsDrinkNow(CombatStyleSO style)
        {
            var input = new CombatTacticsInput();
            FillDrinkInput(ref input, style);
            return CombatTacticsRules.WantsDrink(input);
        }

        private void FillDrinkInput(ref CombatTacticsInput input, CombatStyleSO style)
        {
            if (_drinker == null || _health == null || _health.MaxHealth <= 0f) return; // DrinkThreshold 0 → never
            input.HealthFraction = _health.CurrentHealth / _health.MaxHealth;
            input.DrinkThreshold = style.drinkHealthThreshold;
            // Inventory scanned only when low enough to want it (and never without a config — TryBegin would always refuse).
            input.HasPotion = style.drinkHealthThreshold > 0f && input.HealthFraction <= style.drinkHealthThreshold
                              && _drinker.CanDrinkAtAll && _drinker.HasPotion;
            input.DrinksUsed = _drinker.DrinksUsed;
            input.MaxDrinks = style.maxDrinksPerFight;
            input.DrinkCooldownLeft = _drinker.CooldownLeft;
            input.DrinkSafeDistance = style.drinkSafeDistance;
            input.DrinkBackOffElapsed = _drinkBackOffElapsed;
            input.DrinkBackOffMaxTime = style.drinkBackOffMaxTime;
        }

        // A drink or a shot held the agent stopped: give movement back when tactics end or restart mid-action (the plain
        // chase, Searching and patrol never clear isStopped themselves).
        private void ReleaseDrinkHold()
        {
            if (CurrentAction != CombatAction.Drink && CurrentAction != CombatAction.Shoot) return;
            CurrentAction = CombatAction.Circle;
            if (_agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh) _agent.isStopped = false;
        }

        // Drinking: the agent stops where it stands (no stale back-off path), facing handled by FaceTarget.
        private void HoldStill()
        {
            _agent.updateRotation = false;
            if (!_agent.isActiveAndEnabled || !_agent.isOnNavMesh) return;
            if (_agent.hasPath) _agent.ResetPath();
            _agent.isStopped = true;
        }

        private void ApplyMovement(CombatStyleSO style)
        {
            if (CurrentAction == CombatAction.Drink || CurrentAction == CombatAction.Shoot) return; // held still by HoldStill
            if (!_agent.isActiveAndEnabled || !_agent.isOnNavMesh) return;
            Entity entity = _persistentID.Entity;
            Vector3 targetPos = _target.Transform.position;
            // Bow requested: the ring is the preferred range (spec-ai-ranged-combat-style).
            float ring = _armament != null && _armament.RangedRequested
                ? style.preferredRange
                : CombatTacticsRules.RingRadius(entity.AttackRange, style.ringMargin);
            _agent.isStopped = false;

            switch (CurrentAction)
            {
                case CombatAction.Approach:
                    _agent.updateRotation = true;
                    _agent.speed = entity.EngageSpeed;
                    _agent.stoppingDistance = ring;
                    _agent.SetDestination(targetPos);
                    break;

                case CombatAction.Attack:
                    _agent.updateRotation = false;
                    _agent.speed = entity.EngageSpeed;
                    _agent.stoppingDistance = entity.AttackRange * style.attackApproachFraction;
                    _agent.SetDestination(targetPos);
                    break;

                case CombatAction.BackOff:
                    _agent.updateRotation = false;
                    _agent.speed = style.backOffSpeed;
                    _agent.stoppingDistance = 0f;
                    // Backing off to drink: out past the safe distance, not just to the ring.
                    float backDistance = _drinkPending ? Mathf.Max(ring, style.drinkSafeDistance + style.drinkBackOffMargin) : ring;
                    if (TrySample(style, targetPos + Away(targetPos) * backDistance, out Vector3 back)) _agent.SetDestination(back);
                    else _agent.ResetPath(); // no room behind (wall / mesh edge): hold position, never keep a stale chase path
                    break;

                case CombatAction.Circle:
                    _agent.updateRotation = false;
                    _agent.speed = style.circleSpeed;
                    _agent.stoppingDistance = 0f;
                    bool found = TryCirclePoint(style, targetPos, ring, _circleDirection, out Vector3 point);
                    if (!found && TryCirclePoint(style, targetPos, ring, -_circleDirection, out point))
                    {
                        _circleDirection = -_circleDirection; // only flip when the other side is open
                        found = true;
                    }
                    if (found)
                    {
                        _agent.SetDestination(point);
                    }
                    else
                    {
                        CurrentAction = CombatAction.Approach; // boxed in: plain chase
                        ApplyMovement(style);
                    }
                    break;
            }
        }

        private bool TryCirclePoint(CombatStyleSO style, Vector3 center, float ring, int direction, out Vector3 point)
        {
            float angle = direction * style.circleSpeed * style.decisionInterval * CIRCLE_LEAD_INTERVALS / Mathf.Max(ring, 0.01f);
            Vector3 offset = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up) * Away(center) * ring;
            return TrySample(style, center + offset, out point);
        }

        // Flat unit vector target → self (forward-away when standing on the target).
        private Vector3 Away(Vector3 targetPos)
        {
            Vector3 away = transform.position - targetPos;
            away.y = 0f;
            return away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
        }

        private static bool TrySample(CombatStyleSO style, Vector3 position, out Vector3 result)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, style.navMeshSampleRadius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
            result = position;
            return false;
        }

        // Faces the target; side-on (+ the archer stance yaw) while a bow is raised — BowAimCorrection turns the spine the rest.
        private void FaceTarget()
        {
            Vector3 dir = _target.Transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion facing = Quaternion.LookRotation(dir);
            if (_archer != null && _archer.IsAiming && _health != null && _health.CombatConfig != null)
                facing *= Quaternion.Euler(0f, _health.CombatConfig.npcAimStanceYaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing,
                _persistentID.Entity.WarningTurnSpeed * Time.deltaTime);
        }
    }
}
