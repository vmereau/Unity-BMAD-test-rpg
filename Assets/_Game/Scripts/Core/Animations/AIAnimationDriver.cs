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
        public virtual void TriggerDeath() { }
        public virtual void EnableRagdoll() { }
        public virtual void SetWarning(bool active) { }
        public virtual void SetInCombat(bool active) { }
        }
        }
