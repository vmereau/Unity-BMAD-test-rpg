using System.Collections.Generic;
using Game.AI;
using Game.Combat;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Handles lock-on targeting: acquires the nearest valid enemy in the camera cone
    /// on Middle Mouse Button press, tracks break conditions each frame.
    /// CameraController reads LockedTarget to drive camera tracking (Story 2.10).
    /// </summary>
    public class LockOnSystem : MonoBehaviour
    {
        private const string TAG = "[Player]";

        [SerializeField] private CombatConfigSO _config;
        [SerializeField] private LayerMask _lockOnLayerMask;

        public Transform LockedTarget { get; private set; }
        public bool IsLockedOn => LockedTarget != null;

        // One entity contributes many colliders on the lock-on layer (the spider's ragdoll bones alone are ~13),
        // so the buffer must hold several entities or the aimed one gets truncated out of the query.
        private const int LOCK_ON_BUFFER_SIZE = 64;
        private readonly Collider[] _lockOnBuffer = new Collider[LOCK_ON_BUFFER_SIZE];
        private readonly List<EntityHealth> _candidates = new();
        private readonly List<Vector3> _candidatePositions = new();
        private InputSystem_Actions _input;
        private Camera _mainCamera;

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "LockOnSystem: _config not assigned — LockOnSystem disabled");
                enabled = false;
                return;
            }

            _mainCamera = Camera.main;
            if (_mainCamera == null)
                GameLog.Warn(TAG, "LockOnSystem: Camera.main not found — cone detection will fall back to transform.forward");
        }

        private void OnEnable()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.Player.LockOn.performed += OnLockOnPressed;
        }

        private void OnDisable()
        {
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.LockOn.performed -= OnLockOnPressed;
            _input.Player.Disable();
            _input.Dispose();
        }

        private void OnLockOnPressed(InputAction.CallbackContext _)
        {
            if (IsLockedOn)
                ClearLock();
            else
                TryAcquireTarget();
        }

        private void TryAcquireTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, _config.lockOnRange, _lockOnBuffer,
                _lockOnLayerMask, QueryTriggerInteraction.Collide);
            if (count == _lockOnBuffer.Length)
                GameLog.Warn(TAG, $"Overlap buffer full ({count}) — some targets may be skipped");

            Vector3 camForward = _mainCamera != null
                ? Vector3.Scale(_mainCamera.transform.forward, new Vector3(1f, 0f, 1f)).normalized
                : transform.forward;

            if (camForward == Vector3.zero)
                camForward = transform.forward;

            // One candidate per entity, measured at its EntityHealth root — not per collider / ragdoll bone
            _candidates.Clear();
            _candidatePositions.Clear();
            for (int i = 0; i < count; i++)
            {
                var health = _lockOnBuffer[i].GetComponentInParent<EntityHealth>();
                if (health == null || health.IsDead) continue;
                if (_candidates.Contains(health)) continue;
                _candidates.Add(health);
                _candidatePositions.Add(health.transform.position);
            }

            int best = SelectNearestInCone(transform.position, camForward, _candidatePositions,
                _config.lockOnFOV, _config.lockOnRange);
            EntityHealth nearestHealth = best >= 0 ? _candidates[best] : null;

            if (nearestHealth != null)
            {
                LockedTarget = nearestHealth.transform;
                GameLog.Info(TAG, $"Locked on to {LockedTarget.name}");
            }
            else
            {
                GameLog.Info(TAG, "No valid targets in cone");
            }
        }

        /// <summary>
        /// True when <paramref name="target"/> lies within <paramref name="halfAngle"/> degrees of
        /// <paramref name="forward"/> on the horizontal plane (boundary inclusive). A target at the origin is rejected.
        /// </summary>
        public static bool IsInCone(Vector3 origin, Vector3 forward, Vector3 target, float halfAngle)
        {
            forward.y = 0f;
            Vector3 toTarget = target - origin;
            toTarget.y = 0f;
            if (forward.sqrMagnitude < 1e-6f || toTarget.sqrMagnitude < 1e-6f) return false;
            return Vector3.Angle(forward, toTarget) <= halfAngle;
        }

        /// <summary>
        /// Index of the candidate nearest to <paramref name="origin"/> that is inside the cone and within
        /// <paramref name="range"/> (measured to the candidate itself — a trigger collider reaching into the overlap
        /// sphere does not make a far entity valid). -1 when none qualifies.
        /// </summary>
        public static int SelectNearestInCone(Vector3 origin, Vector3 forward, IReadOnlyList<Vector3> candidates,
            float halfAngle, float range)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!IsInCone(origin, forward, candidates[i], halfAngle)) continue;
                float dist = Vector3.Distance(origin, candidates[i]);
                if (dist > range || dist >= bestDist) continue;
                bestDist = dist;
                best = i;
            }
            return best;
        }

        private void Update()
        {
            if (!IsLockedOn) return;

            if (LockedTarget == null || !LockedTarget.gameObject.activeInHierarchy)
            {
                ClearLock();
                return;
            }

            if (LockedTarget.TryGetComponent<EntityHealth>(out var h) && h.IsDead)
            {
                ClearLock();
                return;
            }

            if (Vector3.Distance(transform.position, LockedTarget.position) > _config.lockOnBreakDistance)
            {
                GameLog.Info(TAG, "Lock broken — target out of range");
                ClearLock();
            }
        }

        private void ClearLock()
        {
            LockedTarget = null;
            GameLog.Info(TAG, "Lock-on cleared");
        }
    }
}
