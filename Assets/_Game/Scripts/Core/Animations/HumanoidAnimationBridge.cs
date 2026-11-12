using UnityEngine;

namespace Game.Animations
{
    /// <summary>
    /// Maps code-level commands to the specific parameters of the Humanoid_Template animator controller.
    /// Used by both Players and complex NPCs.
    /// </summary>
    public class HumanoidAnimationBridge : MonoBehaviour
    {
        private const float DAMP_TIME = 0.1f;

        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsRisingHash = Animator.StringToHash("IsRising");
        private static readonly int VelocityXHash = Animator.StringToHash("VelocityX");
        private static readonly int VelocityZHash = Animator.StringToHash("VelocityZ");
        private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking");
        private static readonly int IsDodgingHash = Animator.StringToHash("IsDodging");
        private static readonly int IsDodgingBackwardsHash = Animator.StringToHash("IsDodgingBackwards");
        private static readonly int IsInCombatHash = Animator.StringToHash("IsInCombat");
        private static readonly int GetHitHash = Animator.StringToHash("GetHit");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int StaggerHash = Animator.StringToHash("Stagger");
        private static readonly int StaggerIndexHash = Animator.StringToHash("StaggerIndex");
        private static readonly int KnockDownHash = Animator.StringToHash("KnockDown");
        private static readonly int IsUnconsciousHash = Animator.StringToHash("IsUnconscious");
        private static readonly int Attack1Hash = Animator.StringToHash("Attack_1");
        private static readonly int Attack2Hash = Animator.StringToHash("Attack_2");
        private static readonly int Attack3Hash = Animator.StringToHash("Attack_3");
        private static readonly int IsSneakingHash = Animator.StringToHash("IsSneaking");
        private static readonly int SneakToSprintHash = Animator.StringToHash("SneakToSprint");
        private static readonly int DrawWeaponHash = Animator.StringToHash("DrawWeapon");
        private static readonly int SheatheWeaponHash = Animator.StringToHash("SheatheWeapon");
        private static readonly int WeaponSwitchSpeedHash = Animator.StringToHash("WeaponSwitchSpeed");
        private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
        private static readonly int BowFireHash = Animator.StringToHash("BowFire");
        private static readonly int IsSwimmingHash = Animator.StringToHash("IsSwimming");

        private const string UPPER_BODY_LAYER = "UpperBody";
        private static readonly int LockOnLocomotionHash = Animator.StringToHash("LockOn Locomotion");

        [SerializeField] private Animator _animator;

        private int _upperBodyLayer = -1; // cached once found; looked up again while missing (controller assigned late)

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        /// <summary>
        /// The Animator this bridge writes to — drivers use it for lifecycle work (ragdoll, root-motion guard) so they
        /// act on the same Animator. Resolves lazily: a driver's Awake may run before this bridge's.
        /// </summary>
        public Animator Animator
        {
            get
            {
                if (_animator == null) _animator = GetComponentInChildren<Animator>();
                return _animator;
            }
        }

        private bool IsAnimatorLive => _animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null;

        private int UpperBodyLayer
        {
            get
            {
                if (_upperBodyLayer < 0 && IsAnimatorLive) _upperBodyLayer = _animator.GetLayerIndex(UPPER_BODY_LAYER);
                return _upperBodyLayer;
            }
        }

        // The state a layer is heading into: the next state during a transition, else the current one.
        private bool DestinationHasTag(int layer, string tag) => _animator.IsInTransition(layer)
            ? _animator.GetNextAnimatorStateInfo(layer).IsTag(tag)
            : _animator.GetCurrentAnimatorStateInfo(layer).IsTag(tag);

        /// <summary>True while the Base Layer is in (or heading into) an attack or another full-body state (tags in <see cref="UpperBodyWeight"/>).</summary>
        public bool IsBaseFullBody => IsAnimatorLive
            && (DestinationHasTag(0, UpperBodyWeight.ATTACK_TAG) || DestinationHasTag(0, UpperBodyWeight.FULL_BODY_TAG));

        /// <summary>True while the UpperBody layer is in (or heading into) block, draw / sheathe or a bow aim state.</summary>
        public bool IsUpperBodyAction => IsAnimatorLive && UpperBodyLayer >= 0
            && DestinationHasTag(UpperBodyLayer, UpperBodyWeight.UPPER_BODY_ACTION_TAG);

        /// <summary>Weight of the masked UpperBody layer (drivers step it with <see cref="UpperBodyWeight"/>). No-op without the layer.</summary>
        public void SetUpperBodyWeight(float weight)
        {
            if (!IsAnimatorLive || UpperBodyLayer < 0) return;
            _animator.SetLayerWeight(UpperBodyLayer, weight);
        }

        public void SetMovement(float x, float z)
        {
            if (_animator == null) return;
            _animator.SetFloat(VelocityXHash, x, DAMP_TIME, Time.deltaTime);
            _animator.SetFloat(VelocityZHash, z, DAMP_TIME, Time.deltaTime);
        }

        public void SetGrounded(bool value) => _animator?.SetBool(IsGroundedHash, value);
        public void SetRising(bool value) => _animator?.SetBool(IsRisingHash, value);
        public void SetBlocking(bool value) => _animator?.SetBool(IsBlockingHash, value);
        public void SetInCombat(bool value) => _animator?.SetBool(IsInCombatHash, value);
        public void SetSwimming(bool value) => _animator?.SetBool(IsSwimmingHash, value);

        /// <summary>
        /// Sets the IsSneaking bool. Entering sneak also clears a pending SneakToSprint trigger so a stale one
        /// can't fire on the next sneak exit.
        /// </summary>
        public void SetSneaking(bool value)
        {
            if (_animator == null) return;
            _animator.SetBool(IsSneakingHash, value);
            if (value) _animator.ResetTrigger(SneakToSprintHash);
        }

        /// <summary>Fires SneakToSprint (sneak → sprint transition clip). Call after <see cref="SetSneaking"/>(false).</summary>
        public void TriggerSneakToSprint() => _animator?.SetTrigger(SneakToSprintHash);

        public void PlayAttack(int triggerHash)
        {
            if (_animator != null && triggerHash != 0) _animator.SetTrigger(triggerHash);
        }

        /// <summary>Trigger hash for combo step 1..3 (<c>Attack_1/2/3</c>); 0 for any other step.</summary>
        public int AttackTriggerHash(int step)
        {
            switch (step)
            {
                case 1: return Attack1Hash;
                case 2: return Attack2Hash;
                case 3: return Attack3Hash;
                default: return 0;
            }
        }

        /// <summary>Clears pending Attack_1/2/3 triggers so a stale one can't auto-chain the next attack.</summary>
        public void ResetAttackTriggers()
        {
            if (_animator == null) return;
            _animator.ResetTrigger(Attack1Hash);
            _animator.ResetTrigger(Attack2Hash);
            _animator.ResetTrigger(Attack3Hash);
        }

        /// <summary>Plays the draw (true) or sheathe (false) clip at <paramref name="speed"/> (WeaponSwitchSpeed).</summary>
        public void PlayWeaponSwitch(bool draw, float speed)
        {
            if (_animator == null) return;
            _animator.SetFloat(WeaponSwitchSpeedHash, speed);
            _animator.ResetTrigger(draw ? SheatheWeaponHash : DrawWeaponHash);
            _animator.SetTrigger(draw ? DrawWeaponHash : SheatheWeaponHash);
        }

        /// <summary>Clears pending DrawWeapon / SheatheWeapon triggers so a stale one can't replay the clip.</summary>
        public void ResetWeaponSwitchTriggers()
        {
            if (_animator == null) return;
            _animator.ResetTrigger(DrawWeaponHash);
            _animator.ResetTrigger(SheatheWeaponHash);
        }

        /// <summary>Sets the IsAiming bool (bow aim: UpperBody layer CombatIdle ⇄ BowDrawArrow / BowAim / BowFire).</summary>
        public void SetAiming(bool value)
        {
            if (_animator != null) _animator.SetBool(IsAimingHash, value);
        }

        /// <summary>Fires BowFire (BowAim_State → BowFire_State → BowDrawArrow_State).</summary>
        public void PlayBowFire()
        {
            if (_animator != null) _animator.SetTrigger(BowFireHash);
        }

        /// <summary>Clears a pending BowFire trigger so a stale one can't fire on the next aim.</summary>
        public void ResetBowFireTrigger()
        {
            if (_animator != null) _animator.ResetTrigger(BowFireHash);
        }

        public void PlayDodge(bool isBackwardRoll = false)
        {
            if (_animator != null)
                _animator.SetTrigger(isBackwardRoll ? IsDodgingBackwardsHash : IsDodgingHash);
        }

        /// <summary>
        /// Plain hit reaction (GetHit has no clip yet; Base reaches it from LockOn Locomotion only). Set only while Base
        /// sits in LockOn Locomotion: anywhere else (attack, sneak, dodge…) the trigger would stay latched and fire later.
        /// </summary>
        public void TriggerGetHit()
        {
            if (!IsAnimatorLive || _animator.IsInTransition(0)
                || _animator.GetCurrentAnimatorStateInfo(0).shortNameHash != LockOnLocomotionHash) return;
            _animator.SetTrigger(GetHitHash);
        }

        /// <summary>Plays Death. Clears pending attack triggers — the Base AnyState → Attack_1 would otherwise leave Death.</summary>
        public void TriggerDeath()
        {
            if (_animator == null) return;
            ResetAttackTriggers();
            _animator.SetTrigger(DeathHash);
        }

        /// <summary>
        /// Plays stagger clip <paramref name="index"/> (StaggerIndex 0/1) on the Base Layer. Clears a pending GetHit set by
        /// the same hit (it would otherwise fire right after and cut the stagger) and pending attack triggers.
        /// </summary>
        public void PlayStagger(int index)
        {
            if (_animator == null) return;
            _animator.ResetTrigger(GetHitHash);
            ResetAttackTriggers(); // a latched Attack_1 would cut the reaction through the Base AnyState
            _animator.SetInteger(StaggerIndexHash, index);
            _animator.SetTrigger(StaggerHash);
        }

        /// <summary>Plays knocked down → getting up on the Base Layer. Clears pending GetHit / Stagger / attack triggers.</summary>
        public void PlayKnockDown()
        {
            if (_animator == null) return;
            _animator.ResetTrigger(GetHitHash);
            _animator.ResetTrigger(StaggerHash);
            ResetAttackTriggers(); // a latched Attack_1 would cut the reaction through the Base AnyState
            _animator.SetTrigger(KnockDownHash);
        }

        /// <summary>
        /// Unconscious (spec-unconscious-knockout). <c>true</c>: KnockDown + the bool take AnyState → <c>Unconscious</c>
        /// (the Knocked Down clip, non-looping → holds its last, on-the-ground frame). <c>false</c>: <c>Unconscious →
        /// GettingUp</c> plays, and an unconsumed KnockDown trigger is dropped so it can't replay once standing.
        /// </summary>
        public void SetUnconscious(bool down)
        {
            if (_animator == null) return;
            _animator.SetBool(IsUnconsciousHash, down);
            if (down) PlayKnockDown();
            else _animator.ResetTrigger(KnockDownHash);
        }

        /// <summary>Clears pending Stagger / KnockDown triggers (reaction over before the animator consumed them).</summary>
        public void ResetStanceTriggers()
        {
            if (_animator == null) return;
            _animator.ResetTrigger(StaggerHash);
            _animator.ResetTrigger(KnockDownHash);
        }

        /// <summary>
        /// Returns the Animator to its default state with default parameters (e.g. out of the terminal Death
        /// state after a revive). Callers must re-apply any parameter they still need (e.g. IsInCombat).
        /// </summary>
        public void ResetToDefaultState()
        {
            if (_animator == null) return;
            _animator.Rebind();
            _animator.Update(0f);
        }
    }
}
