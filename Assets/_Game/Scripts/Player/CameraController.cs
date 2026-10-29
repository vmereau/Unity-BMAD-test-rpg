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
        [Tooltip("Bow aim zoom: reads IsAiming. Optional — no aim camera without it.")]
        [SerializeField] private PlayerStateManager _stateManager;
        [Tooltip("Lens owner for the bow aim FOV. Optional — no FOV change without it.")]
        [SerializeField] private CinemachineCamera _cinemachineCamera;

        private InputSystem_Actions _input;
        private float _yaw;
        private float _pitch;
        private float _defaultCameraTargetLocalY;
        private float _defaultFollowOffsetZ;
        private float _defaultFollowOffsetY;
        private float _defaultFollowOffsetX;
        private float _defaultFieldOfView;
        private float _aimBlend; // 0 = normal camera, 1 = full bow aim (blended at CombatConfigSO.aimBlendSpeed)

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
            {
                _defaultFollowOffsetZ = _cinemachineFollow.FollowOffset.z;
                _defaultFollowOffsetY = _cinemachineFollow.FollowOffset.y;
                _defaultFollowOffsetX = _cinemachineFollow.FollowOffset.x;
            }
            if (_cinemachineCamera != null)
                _defaultFieldOfView = _cinemachineCamera.Lens.FieldOfView;
            if (_stateManager == null)
                GameLog.Warn(TAG, "CameraController: _stateManager not assigned — no bow aim zoom");
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
            UpdateAimBlend();

            // Only rotate camera when cursor is locked (the aim blend still eases back while a menu is open)
            if (!CursorManager.IsLocked)
            {
                UpdateCameraTargetHeight();
                return;
            }

            if (_lockOnSystem != null && _lockOnSystem.IsLockedOn)
                TrackLockedTarget();
            else
                RotateCamera();
        }

        private void RotateCamera()
        {
            Vector2 lookDelta = _input.Player.Look.ReadValue<Vector2>();
            float aimMultiplier = _combatConfig != null ? _combatConfig.aimSensitivityMultiplier : 1f;
            float sensitivity = _mouseSensitivity * Mathf.Lerp(1f, aimMultiplier, _aimBlend);

            _yaw   += lookDelta.x * sensitivity;
            _yaw   %= 360f; // Prevent float precision loss over extended play sessions
            _pitch -= lookDelta.y * sensitivity; // inverted: mouse up = look up
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
            float t;
            float heightSign;
            if (_pitch > 0f)
            {
                t = Mathf.InverseLerp(0f, _pitchMax, _pitch);
                heightSign = 1f;
            }
            else if (_pitch < 0f)
            {
                t = Mathf.InverseLerp(0f, _pitchMin, _pitch);
                heightSign = -1f;
            }
            else
            {
                t = 0f;
                heightSign = 0f;
            }

            // Keep the raycast origin (CameraTarget) at a constant height to prevent it from entering the ceiling
            Vector3 pos = _cameraTarget.localPosition;
            pos.y = _defaultCameraTargetLocalY;
            _cameraTarget.localPosition = pos;

            if (_cinemachineFollow != null)
            {
                Vector3 offset = _cinemachineFollow.FollowOffset;
                // Apply the height adjustment to the camera offset instead
                offset.y = _defaultFollowOffsetY + (heightSign * t * _lookDownHeightRise);
                offset.z = _defaultFollowOffsetZ + (t * _lookDownZoomIn);
                // Bow aim: shift towards the aim offset on top of the look-down adjustment (the two compose)
                if (_aimBlend > 0f && _combatConfig != null)
                {
                    Vector3 aim = _combatConfig.aimFollowOffset;
                    offset.x = Mathf.Lerp(_defaultFollowOffsetX, aim.x, _aimBlend);
                    offset.y += (aim.y - _defaultFollowOffsetY) * _aimBlend;
                    offset.z += (aim.z - _defaultFollowOffsetZ) * _aimBlend;
                }
                else
                {
                    offset.x = _defaultFollowOffsetX;
                }
                _cinemachineFollow.FollowOffset = offset;
            }
        }

        // Eases _aimBlend towards IsAiming and applies the aim FOV.
        private void UpdateAimBlend()
        {
            bool aiming = _stateManager != null && _stateManager.IsAiming;
            float speed = _combatConfig != null ? _combatConfig.aimBlendSpeed : 8f;
            float previous = _aimBlend;
            // Unscaled: the game menu / death screen set timeScale = 0 and the zoom must still ease out behind them.
            _aimBlend = Mathf.MoveTowards(_aimBlend, aiming ? 1f : 0f, speed * Time.unscaledDeltaTime);

            // Only touch the lens while blending or zoomed, so other lens writers keep control at rest.
            if (_aimBlend <= 0f && previous <= 0f) return;
            if (_cinemachineCamera == null || _combatConfig == null) return;
            LensSettings lens = _cinemachineCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(_defaultFieldOfView, _combatConfig.aimFieldOfView, _aimBlend);
            _cinemachineCamera.Lens = lens;
        }
    }
}
