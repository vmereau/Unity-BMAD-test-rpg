using Game.Animations;
using Game.Combat;
using Game.Core;
using Game.Inventory;
using Game.World;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// NPC bow shots (spec-ai-ranged-combat-style), on the NPC root next to <see cref="NpcArmament"/>. Driven by
    /// <see cref="EntityCombatTactics"/>' Shoot action: <see cref="BeginShot"/> raises the aim (<c>IsAiming</c>), the nock
    /// comes from <c>SMB_BowAimState</c> (through <see cref="EntityAnimationEventReceiver"/>) or a watchdog, the arrow
    /// leaves <see cref="CombatStyleSO.aimTime"/> after it (the tell), then the bow is held up briefly for the release clip
    /// and lowered. Arrows are <see cref="ArrowProjectile"/>s owned by this shooter: faction-filtered like melee hits,
    /// <c>TryReceiveHit</c> → <c>TakeDamage(amount, shooter)</c> on <c>NotBlocked</c>, stance, <c>NotifyHitBy</c>.
    /// One arrow (the bow's ammo item) leaves the inventory per shot. Also the <see cref="IBowAimSource"/> of the NPC's
    /// <see cref="BowAimCorrection"/>.
    /// </summary>
    public class EntityArcher : MonoBehaviour, IBowAimSource
    {
        private const string TAG = "[AI]";
        private const float MIN_DIRECTION_SQR = 1e-4f;
        private const float MIN_SWEEP_DISTANCE = 0.01f;
        private const int SWEEP_BUFFER_SIZE = 16;

        [SerializeField] private PersistentID _persistentID;
        [Tooltip("Plays the bow aim states. Auto-resolved on the same GO if null.")]
        [SerializeField] private AIAnimationDriver _animationDriver;

        private readonly RaycastHit[] _sweepBuffer = new RaycastHit[SWEEP_BUFFER_SIZE];

        private NpcArmament _armament;
        private EntityMeleeAttacker _attacker;
        private EquipmentVisuals _visuals;
        private InventorySystem _inventory;
        private EntityHealth _health;
        private FactionMember _target;
        private int _sweepMask;
        private int _hurtboxLayer;

        private bool _aiming;
        private bool _nocked;
        private bool _nockForced;
        private bool _released;
        private float _aimStartTime;
        private float _nockTime;
        private float _releaseTime;
        private float _nextShotTime;

        private FactionMember _trackedTarget;
        private Vector3 _trackedPosition;
        private Vector3 _targetVelocity;

        /// <summary>The bow is raised (draw → aim → release, until lowered).</summary>
        public bool IsAiming => _aiming;

        /// <summary>A shot is in progress (from <see cref="BeginShot"/> until the bow is lowered or the shot is ended).</summary>
        public bool IsShooting => _aiming;

        /// <summary>The shot cooldown has elapsed.</summary>
        public bool IsOffCooldown => Time.time >= _nextShotTime;

        private CombatStyleSO Style => _persistentID != null && _persistentID.Entity != null ? _persistentID.Entity.CombatStyle : null;
        private CombatConfigSO Config => _health != null ? _health.CombatConfig : null;

        private void Awake()
        {
            if (_persistentID == null) _persistentID = GetComponent<PersistentID>();
            if (_animationDriver == null) _animationDriver = GetComponent<AIAnimationDriver>();
            _armament = GetComponent<NpcArmament>();
            _attacker = GetComponent<EntityMeleeAttacker>();
            _visuals = GetComponent<EquipmentVisuals>();
            _inventory = GetComponent<InventorySystem>();
            _health = GetComponent<EntityHealth>();
            _sweepMask = LayerMask.GetMask(GameConstants.DEFAULT_LAYER_NAME, GameConstants.CHARACTER_HITBOX_LAYER_NAME);
            _hurtboxLayer = LayerMask.NameToLayer(GameConstants.CHARACTER_HITBOX_LAYER_NAME);
        }

        private void OnDisable() => End();

        /// <summary>
        /// Ranged fighting is possible against <paramref name="target"/>: a configured bow carried with arrows left, a
        /// faction-hostile target (brawls stay melee), no fists request, a combat config.
        /// </summary>
        public bool CanUseRangedAgainst(FactionMember target)
        {
            if (!isActiveAndEnabled || target == null || _armament == null || _attacker == null || Config == null) return false;
            BowSO bow = _armament.CarriedBow;
            if (bow == null || _inventory == null) return false;
            FactionMember self = FactionMember.Of(gameObject);
            bool hostile = self != null && self.Faction != null && target.Faction != null && self.Faction.IsHostileTo(target.Faction);
            return CombatTacticsRules.CanUseRanged(bow.IsConfigured && bow.equipVisualPrefab != null,
                _inventory.CountOf(bow.ammoItem), hostile, _armament.FistsRequested);
        }

        /// <summary>
        /// Can start a shot at <paramref name="target"/> now: bow in hand (not switching), off cooldown, arrows left and a
        /// clear line of fire (the first thing on the sweep to the aim point is the target, a hostile body or nothing).
        /// </summary>
        public bool CanShootAt(FactionMember target)
        {
            if (_aiming || !IsOffCooldown || target == null || _armament == null) return false;
            if (!_armament.IsRanged || _armament.IsSwitchingWeapon || _armament.AmmoCount <= 0) return false;
            return HasClearShot(target);
        }

        /// <summary>Raises the bow at <paramref name="target"/>. False when no shot is possible.</summary>
        public bool BeginShot(FactionMember target)
        {
            if (!CanShootAt(target)) return false;
            _target = target;
            _aiming = true;
            _nocked = false;
            _nockForced = false;
            _released = false;
            _aimStartTime = Time.time;
            if (_animationDriver != null)
            {
                _animationDriver.ClearBowFire();
                _animationDriver.SetAiming(true);
            }
            return true;
        }

        /// <summary>Lowers the bow at once (shot over, interrupted, tactics ended). No arrow if not yet released. Idempotent.</summary>
        public void End()
        {
            if (!_aiming) return;
            _aiming = false;
            _nocked = false;
            _target = null;
            if (_animationDriver != null)
            {
                _animationDriver.SetAiming(false);
                _animationDriver.ClearBowFire();
            }
        }

        /// <summary>From <c>SMB_BowAimState</c> (via the receiver): the aim pose is reached — the arrow is nocked.</summary>
        public void OnBowAimEntered()
        {
            if (!_aiming || _released || _nocked) return; // a re-nock after the release is ignored
            _nocked = true;
            _nockForced = false;
            _nockTime = Time.time;
        }

        private void Update()
        {
            TrackTarget();
            if (!_aiming) return;

            CombatConfigSO config = Config;
            CombatStyleSO style = Style;
            if (config == null || style == null || _target == null || _armament == null || !_armament.IsRanged
                || _target.Damageable == null || _target.Damageable.IsDead || _target.Damageable.IsUnconscious)
            {
                End(); // includes a target that died / went down mid-aim: no arrow at a body
                return;
            }
            // Controller swaps reset parameters: re-push the aim every frame (like IsDrinking / IsBlocking).
            if (_animationDriver != null) _animationDriver.SetAiming(true);

            if (_released)
            {
                if (Time.time - _releaseTime >= config.npcBowReleaseHold) End();
                return;
            }
            if (!_nocked && Time.time - _aimStartTime > config.npcBowNockTimeout)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: bow nock stalled ({config.npcBowNockTimeout:F2}s) — forcing nock");
                _nocked = true;
                _nockForced = true;
                _nockTime = Time.time;
            }
            if (_nocked && Time.time - _nockTime >= style.aimTime) Release(style, config);
        }

        // Smoothed velocity of the current target (lead), from its position each frame.
        private void TrackTarget()
        {
            FactionMember target = _target;
            if (target == null)
            {
                _trackedTarget = null;
                return;
            }
            Vector3 position = target.Transform.position;
            if (target != _trackedTarget)
            {
                _trackedTarget = target;
                _trackedPosition = position;
                _targetVelocity = Vector3.zero;
                return;
            }
            CombatConfigSO config = Config;
            _targetVelocity = RangedAimMath.SmoothVelocity(_targetVelocity, _trackedPosition, position, Time.deltaTime,
                config != null ? config.npcTargetVelocitySmoothTime : 0f);
            _trackedPosition = position;
        }

        private void Release(CombatStyleSO style, CombatConfigSO config)
        {
            _released = true;
            _releaseTime = Time.time;
            _nextShotTime = Time.time + style.shotCooldown;
            BowSO bow = _armament.WieldedWeapon as BowSO;
            if (bow == null || !bow.IsConfigured || _inventory == null)
            {
                End();
                return;
            }
            if (!bow.projectilePrefab.TryGetComponent(out ArrowProjectile _))
            {
                GameLog.Error(TAG, $"Projectile prefab '{bow.projectilePrefab.name}' has no ArrowProjectile");
                End();
                return;
            }
            // The line may have closed during the draw (an ally stepped in): lower the bow, no arrow spent.
            if (!HasClearShot(_target))
            {
                GameLog.Info(TAG, $"{gameObject.name}: line of fire blocked at the release — shot held");
                End();
                return;
            }
            if (!_inventory.TryConsume(bow.ammoItem))
            {
                GameLog.Info(TAG, $"{gameObject.name}: no arrow to shoot");
                End();
                return;
            }
            // A forced nock never reached BowAim_State: BowFire would latch and replay on the next aim enter.
            if (!_nockForced && _animationDriver != null) _animationDriver.TriggerBowFire();

            Vector3 origin = LaunchOrigin();
            Vector3 aimPoint = RangedAimMath.LeadPoint(origin, AimPointOf(_target, config), _targetVelocity,
                bow.projectileSpeed, style.leadFactor);
            Vector3 direction = aimPoint - origin;
            if (direction.sqrMagnitude < MIN_DIRECTION_SQR) direction = transform.forward;
            direction = BowSpreadMath.SampleCone(direction, style.shotSpreadAngle, Random.value, Random.value);

            ArrowProjectile projectile = Instantiate(bow.projectilePrefab, origin, Quaternion.LookRotation(direction))
                .GetComponent<ArrowProjectile>();
            float damage = (_persistentID != null && _persistentID.Entity != null ? _persistentID.Entity.AttackDamage : 0f)
                           + bow.damageBonus;
            float stanceDamage = bow.ResolvedStanceDamage;
            bool canKnockDown = bow.ResolvedCanKnockDown;
            projectile.Launch(transform, direction, bow.projectileSpeed, bow.projectileLifetime,
                (hit, point) => ResolveArrowHit(hit, point, damage, stanceDamage, canKnockDown),
                bow.ammoItem.worldItemPrefab, bow.gravityScale, bow.gravityDelay, bow.gravityRampTime);
            GameLog.Info(TAG, $"{gameObject.name} shoots at {_target.name} ({damage} dmg), {_inventory.CountOf(bow.ammoItem)} arrows left");
        }

        // Same resolution as a melee hit (EntityMeleeAttacker.HandleHit), owned by the shooter. A non-hostile body absorbs
        // the arrow; a parry at range costs the shooter nothing.
        private void ResolveArrowHit(IDamageable target, Vector3 point, float damage, float stanceDamage, bool canKnockDown)
        {
            if (this == null) return; // arrow landing after the shooter was destroyed
            if (target == null || target.IsDead || _attacker == null) return;
            if (!_attacker.IsHostileTarget(target))
            {
                GameLog.Info(TAG, $"{gameObject.name}'s arrow hit a non-hostile — absorbed");
                return;
            }
            HitResult result = target.TryReceiveHit(gameObject);
            _attacker.PlayHitSound(result, point);
            switch (result)
            {
                case HitResult.Blocked:
                    if (target is Component blocker && blocker != null && blocker.TryGetComponent(out IStanceTarget blockerStance))
                        blockerStance.ApplyBlockedStanceDamage(stanceDamage);
                    GameLog.Info(TAG, $"{gameObject.name}'s arrow blocked");
                    break;
                case HitResult.PerfectBlock:
                case HitResult.Dodged:
                    GameLog.Info(TAG, $"{gameObject.name}'s arrow {result}");
                    break;
                case HitResult.NotBlocked:
                    if (target is Component victimComponent && victimComponent.TryGetComponent(out ISneakAttackTarget victim))
                        victim.NotifyHitBy(gameObject);
                    target.TakeDamage(damage, gameObject);
                    GameLog.Info(TAG, $"{gameObject.name}'s arrow hit at {point} ({damage} dmg)");
                    if (!target.IsDead && !target.IsUnconscious && target is Component stanceComponent
                        && stanceComponent.TryGetComponent(out IStanceTarget stanceTarget))
                        stanceTarget.ApplyStanceDamage(stanceDamage, canKnockDown);
                    break;
            }
        }

        // Sweep bow → aim point: the first collider must be the target, a hostile body, or nothing (owner and non-hurtbox
        // triggers skipped, like the arrow). A wall or a non-hostile body holds the shot.
        private bool HasClearShot(FactionMember target)
        {
            CombatConfigSO config = Config;
            if (config == null) return false;
            Vector3 origin = LaunchOrigin();
            Vector3 toTarget = AimPointOf(target, config) - origin;
            float distance = toTarget.magnitude;
            if (distance < MIN_SWEEP_DISTANCE) return true;
            int count = Physics.SphereCastNonAlloc(origin, config.npcClearShotRadius, toTarget / distance, _sweepBuffer,
                distance, _sweepMask, QueryTriggerInteraction.Collide);
            if (count >= _sweepBuffer.Length) return false; // results truncated (unsorted): a wall may be missing — hold
            float best = float.MaxValue;
            Collider first = null;
            for (int i = 0; i < count; i++)
            {
                Collider col = _sweepBuffer[i].collider;
                if (col == null || col.transform.IsChildOf(transform)) continue;
                if (col.isTrigger && col.gameObject.layer != _hurtboxLayer) continue;
                IDamageable damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsDead) continue; // the arrow flies through corpses too
                if (_sweepBuffer[i].distance >= best) continue;
                best = _sweepBuffer[i].distance;
                first = col;
            }
            if (first == null) return true;
            if (first.transform.IsChildOf(target.Transform)) return true;
            IDamageable body = first.GetComponentInParent<IDamageable>();
            return body != null && _attacker != null && _attacker.IsHostileTarget(body);
        }

        // Bow (drawn: left-hand socket) — else chest height in front of the NPC.
        private Vector3 LaunchOrigin()
        {
            GameObject weapon = _visuals != null ? _visuals.ActiveWeaponGO : null;
            if (weapon != null && _armament != null && _armament.IsRanged) return weapon.transform.position;
            CombatConfigSO config = Config;
            if (config == null) return transform.position;
            return transform.position + Vector3.up * (BodyHeight(gameObject, config) * config.npcAimHeightFraction);
        }

        private static Vector3 AimPointOf(FactionMember target, CombatConfigSO config) =>
            target.Transform.position + Vector3.up * (BodyHeight(target.gameObject, config) * config.npcAimHeightFraction);

        // Body height: CharacterController (player) or NavMeshAgent (NPCs, monsters); else the fallback aim height.
        private static float BodyHeight(GameObject body, CombatConfigSO config)
        {
            if (body.TryGetComponent(out CharacterController controller)) return controller.height;
            if (body.TryGetComponent(out NavMeshAgent agent)) return agent.height;
            return config.npcAimFallbackHeight / Mathf.Max(0.01f, config.npcAimHeightFraction);
        }

        /// <summary><see cref="IBowAimSource"/>: the bow is up.</summary>
        public bool IsBowAiming => _aiming;

        /// <summary><see cref="IBowAimSource"/>: the target's aim point, on the chest → target ray.</summary>
        public bool TryGetBowAim(out Vector3 point, out Ray viewRay)
        {
            CombatConfigSO config = Config;
            if (!_aiming || _target == null || config == null)
            {
                point = default;
                viewRay = default;
                return false;
            }
            point = AimPointOf(_target, config);
            Vector3 eye = transform.position + Vector3.up * (BodyHeight(gameObject, config) * config.npcAimHeightFraction);
            viewRay = new Ray(eye, point - eye);
            return true;
        }
    }
}
