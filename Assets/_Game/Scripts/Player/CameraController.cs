using Unity.Cinemachine;
using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Reads mouse Look input and drives the CameraTarget pivot rotation (yaw + pitch).
    /// Cinemachine (CinemachineThirdPersonFollow) orbits the camera around CameraTarget and owns
    /// obstacle collision. This script does NOT move the camera directly: it only sets the rig
    /// shoulder offset and arm distance (look-up shortening, bow aim).
    /// Cursor is locked on start; Escape unlocks, left-click re-locks.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        private const string TAG = "[Camera]";

        [SerializeField] private Transform _cameraTarget;
        [SerializeField] private float _mouseSensitivity = 1f;
        [SerializeField] private float _pitchMin = -70f;
        [SerializeField] private float _pitchMax = 70f;
        [Tooltip("Arm distance multiplier at full look-up (pitch = Pitch Min). 1 = constant distance.")]
        [SerializeField, Range(0.1f, 1f)] private float _lookUpDistanceScale = 0.5f;
        [Tooltip("Most upward pitch allowed while swimming, so the camera stays above the water surface " +
                 "(-5° keeps a 4.5 m arm ≈ 0.25 m above it at swimFloatDepth 1.45).")]
        [SerializeField, Range(-70f, 0f)] private float _swimPitchMin = -5f;
        [Tooltip("Degrees per second the pitch limit eases to / from the swim limit (no snap on entering water).")]
        [SerializeField, Min(1f)] private float _swimPitchEaseSpeed = 180f;
        [Tooltip("A CameraTarget jump longer than this in one frame (teleport, load, spawn) warps the camera " +
                 "instead of letting the rig damping sweep it across the distance.")]
        [SerializeField, Min(0.5f)] private float _warpDistance = 3f;
        [SerializeField] private CinemachineThirdPersonFollow _thirdPersonFollow;
        [SerializeField] private LockOnSystem _lockOnSystem;
        [SerializeField] private CombatConfigSO _combatConfig;
        [Tooltip("Bow aim zoom + swimming: reads IsAiming / IsSwimming. Optional — no aim camera or swim limit without it.")]
        [SerializeField] private PlayerStateManager _stateManager;
        [Tooltip("Lens owner for the bow aim FOV. Optional — no FOV change without it.")]
        [SerializeField] private CinemachineCamera _cinemachineCamera;

        private InputSystem_Actions _input;
        private float _yaw;
        private float _pitch;
        private float _pitchFloor; // current most-upward pitch (eases between _pitchMin and the swim limit)
        private Vector3 _defaultShoulderOffset;
        private float _defaultCameraDistance;
        private float _defaultFieldOfView;
        private float _aimBlend; // 0 = normal camera, 1 = full bow aim (blended at CombatConfigSO.aimBlendSpeed)
        private Vector3 _lastTargetPosition;

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
            if (_thirdPersonFollow == null)
                GameLog.Warn(TAG, "CameraController: _thirdPersonFollow not assigned — no look-up shortening or aim offset");

            CursorManager.Lock();

            // Initialize yaw/pitch from current target rotation to avoid snap on first frame.
            // Use DeltaAngle to convert eulerAngles [0, 360] → signed [-180, 180] for pitch,
            // so an initial downward tilt (e.g. 350°) is read as -10° instead of clamping to +70°.
            _yaw   = _cameraTarget.eulerAngles.y;
            _pitch = Mathf.DeltaAngle(0f, _cameraTarget.eulerAngles.x);
            _pitchFloor = _pitchMin;
            _lastTargetPosition = _cameraTarget.position;

            if (_thirdPersonFollow != null)
            {
                _defaultShoulderOffset = _thirdPersonFollow.ShoulderOffset;
                _defaultCameraDistance = _thirdPersonFollow.CameraDistance;
            }
            if (_cinemachineCamera != null)
                _defaultFieldOfView = _cinemachineCamera.Lens.FieldOfView;
            if (_stateManager == null)
                GameLog.Warn(TAG, "CameraController: _stateManager not assigned — no bow aim zoom or swim pitch limit");
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
            DetectWarp();
            UpdateAimBlend();
            UpdatePitchFloor();

            // Only rotate camera when cursor is locked (the rig still eases back from aim while a menu is open)
            if (CursorManager.IsLocked)
            {
                if (_lockOnSystem != null && _lockOnSystem.IsLockedOn)
                    TrackLockedTarget();
                else
                    RotateCamera();
            }

            ApplyRig();
        }

        private void RotateCamera()
        {
            Vector2 lookDelta = _input.Player.Look.ReadValue<Vector2>();
            float aimMultiplier = _combatConfig != null ? _combatConfig.aimSensitivityMultiplier : 1f;
            float sensitivity = _mouseSensitivity * Mathf.Lerp(1f, aimMultiplier, _aimBlend);

            _yaw   += lookDelta.x * sensitivity;
            _yaw   %= 360f; // Prevent float precision loss over extended play sessions
            _pitch -= lookDelta.y * sensitivity; // inverted: mouse up = look up
            _pitch  = Mathf.Clamp(_pitch, _pitchFloor, _pitchMax);

            _cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void TrackLockedTarget()
        {
            float heightOffset = _combatConfig != null ? _combatConfig.lockOnTargetHeightOffset : 1.0f;
            Vector3 dir = (_lockOnSystem.LockedTarget.position + Vector3.up * heightOffset) - _cameraTarget.position;
            if (dir.sqrMagnitude < 0.0001f) return; // Avoid NaN when coincident

            dir.Normalize();

            float desiredYaw   = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float desiredPitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            desiredPitch = Mathf.Clamp(desiredPitch, _pitchFloor, _pitchMax);

            float lerpSpeed = _combatConfig != null ? _combatConfig.lockOnLerpSpeed : 5f;

            _yaw   = Mathf.LerpAngle(_yaw,   desiredYaw,   lerpSpeed * Time.deltaTime);
            _pitch = Mathf.Lerp(_pitch,       desiredPitch, lerpSpeed * Time.deltaTime);

            _cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        // Teleports (load, spawn, story sequences) move the player in one frame: tell Cinemachine, so the rig
        // damping doesn't sweep the camera across the whole distance (and through the geometry on the way).
        private void DetectWarp()
        {
            Vector3 position = _cameraTarget.position;
            Vector3 delta = position - _lastTargetPosition;
            _lastTargetPosition = position;
            if (delta.sqrMagnitude > _warpDistance * _warpDistance)
                CinemachineCore.OnTargetObjectWarped(_cameraTarget, delta);
        }

        // Eases the upward pitch limit to the swim limit (and back) and keeps the pitch above it — also while a
        // menu is open — so entering water never snaps the view.
        private void UpdatePitchFloor()
        {
            bool swimming = _stateManager != null && _stateManager.IsSwimming;
            float target = CameraRigMath.EffectivePitchMin(_pitchMin, swimming, _swimPitchMin);
            float before = _pitch;
            CameraRigMath.StepPitchFloor(ref _pitchFloor, ref _pitch, target, _swimPitchEaseSpeed * Time.deltaTime);
            if (!Mathf.Approximately(before, _pitch))
                _cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        // Sets the ThirdPersonFollow shoulder / distance: bow aim blend, then the look-up shortening.
        private void ApplyRig()
        {
            if (_thirdPersonFollow == null) return;

            Vector3 shoulder = _defaultShoulderOffset;
            float distance = _defaultCameraDistance;
            if (_aimBlend > 0f && _combatConfig != null)
                CameraRigMath.BlendRig(_defaultShoulderOffset, _defaultCameraDistance,
                    _combatConfig.aimFollowOffset, _aimBlend, out shoulder, out distance);

            distance *= CameraRigMath.RigDistanceScale(_pitch, _pitchMin, _lookUpDistanceScale, _aimBlend);

            _thirdPersonFollow.ShoulderOffset = shoulder;
            _thirdPersonFollow.CameraDistance = distance;
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
