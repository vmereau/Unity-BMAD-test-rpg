using Game.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Animations
{
    /// <summary>
    /// Concrete <see cref="AIAnimationDriver"/> for humanoid AI entities (NPCs and, later,
    /// humanoid enemies). Reads <c>NavMeshAgent.velocity</c>, normalizes it into local space
    /// against <c>_runSpeed</c>, and forwards <c>VelocityX</c>/<c>VelocityZ</c>/<c>IsGrounded</c>/<c>IsRising</c>
    /// to <see cref="HumanoidAnimationBridge"/>. Combat triggers are intentional no-op stubs
    /// pending the humanoid AI combat epic.
    /// </summary>
    [RequireComponent(typeof(HumanoidAnimationBridge))]
    public class HumanoidAIAnimationDriver : AIAnimationDriver
    {
        private const string TAG = "[AI]";

        [SerializeField] private HumanoidAnimationBridge _bridge;

        [Tooltip("Velocity at which the humanoid 2D blend tree shows the run clip (normalized = ±1.0). Set to match the entity's NavMeshAgent peak speed. Default 4f matches Entity.EngageSpeed default.")]
        [SerializeField] private float _runSpeed = 4f;

        private void Awake()
        {
            if (_bridge == null) _bridge = GetComponent<HumanoidAnimationBridge>();
            if (_bridge == null)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: No HumanoidAnimationBridge sibling — HumanoidAIAnimationDriver disabled");
                enabled = false;
                return;
            }
            if (_runSpeed <= 0f)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: _runSpeed must be > 0 — HumanoidAIAnimationDriver disabled");
                enabled = false;
            }
        }

        public override void DriveLocomotion(NavMeshAgent agent)
        {
            if (_bridge == null || agent == null) return;
            Vector3 worldHoriz = new Vector3(agent.velocity.x, 0f, agent.velocity.z);
            Vector3 localVelocity = transform.InverseTransformDirection(worldHoriz);
            float normX = Mathf.Clamp(localVelocity.x / _runSpeed, -1f, 1f);
            float normZ = Mathf.Clamp(localVelocity.z / _runSpeed, -1f, 1f);
            _bridge.SetMovement(normX, normZ);
            _bridge.SetGrounded(true);
            _bridge.SetRising(false);
        }

        public override void TriggerAttack() => GameLog.Warn(TAG, $"{name}: humanoid AI attack not implemented yet");
        public override void TriggerGetHit() => GameLog.Warn(TAG, $"{name}: humanoid AI get-hit not implemented yet");
        public override void TriggerDeath()  => GameLog.Warn(TAG, $"{name}: humanoid AI death not implemented yet");
        public override void EnableRagdoll() => GameLog.Warn(TAG, $"{name}: humanoid AI ragdoll not implemented yet");
    }
}
