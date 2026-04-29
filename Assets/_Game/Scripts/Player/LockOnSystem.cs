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

        private readonly Collider[] _lockOnBuffer = new Collider[10];
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
            int count = Physics.OverlapSphereNonAlloc(transform.position, _config.lockOnRange, _lockOnBuffer, _lockOnLayerMask);

            Vector3 camForward = _mainCamera != null
                ? Vector3.Scale(_mainCamera.transform.forward, new Vector3(1f, 0f, 1f)).normalized
                : transform.forward;

            if (camForward == Vector3.zero)
                camForward = transform.forward;

            EntityHealth nearestHealth = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider hit = _lockOnBuffer[i];
                var health = hit.transform.GetComponentInParent<EntityHealth>();
                if (health == null) continue;
                if (health.IsDead) continue;

                Vector3 toEnemy = hit.transform.position - transform.position;
                toEnemy.y = 0f;
                if (toEnemy == Vector3.zero) continue;

                float angle = Vector3.Angle(camForward, toEnemy.normalized);
                if (angle > _config.lockOnFOV) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestHealth = health;
                }
            }

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
