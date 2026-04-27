using Unity.Cinemachine;
using Game.Combat;
using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Reads mouse Look input and drives the CameraTarget pivot rotation (yaw + pitch).
    /// Cinemachine follows CameraTarget — this script does NOT move the camera directly.
    /// Cursor is locked on start; Escape unlocks, left-click re-locks.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        private const string TAG = "[Camera]";

        [SerializeField] private Transform _cameraTarget;
        [SerializeField] private float _mouseSensitivity = 1f;
        [SerializeField] private float _pitchMin = -70f;
        [SerializeField] private float _pitchMax = 70f;
        [SerializeField] private float _lookDownHeightRise = 0.7f;
        [SerializeField] private float _lookDownZoomIn = 1.5f;
        [SerializeField] private CinemachineFollow _cinemachineFollow;
        [SerializeField] private LockOnSystem _lockOnSystem;
        [SerializeField] private CombatConfigSO _combatConfig;

        private InputSystem_Actions _input;
        private float _yaw;
        private float _pitch;
        private float _defaultCameraTargetLocalY;
        private float _defaultFollowOffsetZ;

        private void Awake()
        {
            if (_cameraTarget == null)
            {
                GameLog.Error(TAG, "CameraTarget not assigned — CameraController disabled");
                enabled = false;
                return;
            }

            if (_lockOnSystem == null)
                GameLog.Warn(TAG, "CameraController: _lockOnSystem not assigned — lock-on tracking unavailable (free-look still works)");
            if (_combatConfig == null)
                GameLog.Warn(TAG, "CameraController: _combatConfig not assigned — lockOnLerpSpeed will use fallback value");

            CursorManager.Lock();

            // Initialize yaw/pitch from current target rotation to avoid snap on first frame.
            // Use DeltaAngle to convert eulerAngles [0, 360] → signed [-180, 180] for pitch,
            // so an initial downward tilt (e.g. 350°) is read as -10° instead of clamping to +70°.
            _yaw   = _cameraTarget.eulerAngles.y;
            _pitch = Mathf.DeltaAngle(0f, _cameraTarget.eulerAngles.x);
            _defaultCameraTargetLocalY = _cameraTarget.localPosition.y;
            if (_cinemachineFollow != null)
                _defaultFollowOffsetZ = _cinemachineFollow.FollowOffset.z;
        }

        private void OnEnable()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.UI.Enable();
        }

        private void OnDisable()
        {
            if (_input == null) return; // Guard: Awake may disable component before OnEnable runs
            _input.UI.Disable();
            _input.Player.Disable();
            _input.Dispose();
        }

        private void LateUpdate()
        {
            // Only rotate camera when cursor is locked
            if (!CursorManager.IsLocked)
                return;

            if (_lockOnSystem != null && _lockOnSystem.IsLockedOn)
                TrackLockedTarget();
            else
                RotateCamera();
        }

        private void RotateCamera()
        {
            Vector2 lookDelta = _input.Player.Look.ReadValue<Vector2>();

            _yaw   += lookDelta.x * _mouseSensitivity;
            _yaw   %= 360f; // Prevent float precision loss over extended play sessions
            _pitch -= lookDelta.y * _mouseSensitivity; // inverted: mouse up = look up
            _pitch  = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);

            _cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            UpdateCameraTargetHeight();
        }

        private void TrackLockedTarget()
        {
            float heightOffset = _combatConfig != null ? _combatConfig.lockOnTargetHeightOffset : 1.0f;
            Vector3 dir = (_lockOnSystem.LockedTarget.position + Vector3.up * heightOffset) - _cameraTarget.position;
            if (dir.sqrMagnitude < 0.0001f) return; // Avoid NaN when coincident

            dir.Normalize();

            float desiredYaw   = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float desiredPitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            desiredPitch = Mathf.Clamp(desiredPitch, _pitchMin, _pitchMax);

            float lerpSpeed = _combatConfig != null ? _combatConfig.lockOnLerpSpeed : 5f;

            _yaw   = Mathf.LerpAngle(_yaw,   desiredYaw,   lerpSpeed * Time.deltaTime);
            _pitch = Mathf.Lerp(_pitch,       desiredPitch, lerpSpeed * Time.deltaTime);

            _cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            UpdateCameraTargetHeight();
        }

        private void UpdateCameraTargetHeight()
        {
            float t = _pitch > 0f ? Mathf.InverseLerp(0f, _pitchMax, _pitch) : 0f;

            Vector3 pos = _cameraTarget.localPosition;
            pos.y = _defaultCameraTargetLocalY + t * _lookDownHeightRise;
            _cameraTarget.localPosition = pos;

            if (_cinemachineFollow != null)
            {
                Vector3 offset = _cinemachineFollow.FollowOffset;
                offset.z = _defaultFollowOffsetZ + t * _lookDownZoomIn;
                _cinemachineFollow.FollowOffset = offset;
            }
        }
    }
}
