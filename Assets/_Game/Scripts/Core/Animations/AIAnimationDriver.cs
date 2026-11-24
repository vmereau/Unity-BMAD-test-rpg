using UnityEngine;
using UnityEngine.AI;

namespace Game.Animations
{
    /// <summary>
    /// Polymorphic seam between <c>EntityBrain</c> / <c>EntityHealth</c> and the entity's animator.
    /// Concrete subclasses: <c>MonsterAnimationDriver</c> (monsters), <c>HumanoidAIAnimationDriver</c>
    /// (humanoid AI). The Player uses <c>PlayerAnimationDriver</c> instead — Player is not
    /// AI-driven and does not flow through this hierarchy.
    /// </summary>
    public abstract class AIAnimationDriver : MonoBehaviour
    {
        public virtual void DriveLocomotion(NavMeshAgent agent) { }
        public virtual void TriggerAttack() { }
        public virtual void TriggerGetHit() { }

        /// <summary>Stance stagger; <paramref name="variant"/> picks the clip (humanoid: 0/1). Default: GetHit.</summary>
        public virtual void TriggerStagger(int variant) => TriggerGetHit();

        /// <summary>Stance broken: knocked down → getting up. Default: GetHit (no knockdown clip).</summary>
        public virtual void TriggerKnockDown() => TriggerGetHit();

        /// <summary>Recovered: drops a Stagger / KnockDown trigger that was never consumed, so it can't replay later.</summary>
        public virtual void ClearStanceReaction() { }

        /// <summary>
        /// Knocked out (true: knocked down clip held on the ground) / gets up (false: getting up clip). Only humanoids
        /// are knocked out; default no-op.
        /// </summary>
        public virtual void SetUnconscious(bool down) { }
        /// <summary>Lying asleep (<c>IsSleeping</c>, spec-npc-routine-sleep) / back up. Default: no clip (monsters).</summary>
        public virtual void SetSleeping(bool asleep) { }
        /// <summary>
        /// Guard up / down (<c>IsBlocking</c>, spec-ai-npc-blocking); <paramref name="shieldInHand"/> picks the shield guard
        /// clip on a raise (spec-one-hand-braced-block). Default: no block animation (monsters).
        /// </summary>
        public virtual void SetBlocking(bool blocking, bool shieldInHand) { }
        /// <summary>Potion drink on / off (<c>IsDrinking</c>, spec-potion-drink-animation). Default: no clip (monsters).</summary>
        public virtual void SetDrinking(bool drinking) { }
        /// <summary>Bow aim on / off (<c>IsAiming</c>, spec-ai-ranged-combat-style). Default: no bow states (monsters).</summary>
        public virtual void SetAiming(bool aiming) { }
        /// <summary>Releases the nocked arrow (<c>BowFire</c>: BowAim → BowFire → BowDrawArrow). Default no-op.</summary>
        public virtual void TriggerBowFire() { }
        /// <summary>Drops a pending <c>BowFire</c> trigger so it can't replay on the next aim. Default no-op.</summary>
        public virtual void ClearBowFire() { }
        /// <summary>Picks something up from the ground (knockout loot). Only humanoids have the clip; default no-op.</summary>
        public virtual void TriggerPickUp() { }

        /// <summary>Drops a pending pick-up and blends a playing one out (loot cancelled). Default no-op.</summary>
        public virtual void CancelPickUp() { }

        /// <summary>The pick-up clip is playing (or blending in). Default false — callers fall back to a timeout.</summary>
        public virtual bool IsPickingUp => false;
        public virtual void TriggerDeath() { }
        public virtual void EnableRagdoll() { }
        public virtual void SetWarning(bool active) { }
        public virtual void SetInCombat(bool active) { }

        /// <summary>
        /// Plays the draw (true) / sheathe (false) clip at <paramref name="speed"/>; the clip's <c>WeaponSwap</c> event moves
        /// the weapon visual. Only humanoids have the clips; default no-op.
        /// </summary>
        public virtual void TriggerWeaponSwitch(bool draw, float speed) { }

        /// <summary>Drops a pending draw / sheathe trigger so it can't replay later. Default no-op.</summary>
        public virtual void ClearWeaponSwitch() { }

        /// <summary>Highest combo step this driver can play (1 = single attacks only).</summary>
        public virtual int MaxComboSteps => 1;

        /// <summary>Plays combo step 2..<see cref="MaxComboSteps"/>; step 1 is <see cref="TriggerAttack"/>.</summary>
        public virtual void TriggerComboStep(int step) { }

        /// <summary>Drops any queued attack/combo trigger so an ended attack can't replay later.</summary>
        public virtual void CancelAttack() { }
    }
}
