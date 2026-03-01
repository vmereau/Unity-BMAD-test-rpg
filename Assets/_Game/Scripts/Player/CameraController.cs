using Game.Core;
using UnityEngine;
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

        private InputSystem_Actions _input;
        private float _yaw;
        private float _pitch;

        private void Awake()
        {
            if (_cameraTarget == null)
            {
                GameLog.Error(TAG, "CameraTarget not assigned — CameraController disabled");
                enabled = false;
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Initialize yaw/pitch from current target rotation to avoid snap on first frame
            _yaw   = _cameraTarget.eulerAngles.y;
            _pitch = _cameraTarget.eulerAngles.x;
        }

        private void OnEnable()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
        }

        private void OnDisable()
        {
            _input.Player.Disable();
            _input.Dispose();
        }

        private void Update()
        {
            HandleCursorLock();

            // Only rotate camera when cursor is locked
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            RotateCamera();
        }

        private void HandleCursorLock()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame
                && Cursor.lockState == CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void RotateCamera()
        {
            Vector2 lookDelta = _input.Player.Look.ReadValue<Vector2>();

            _yaw   += lookDelta.x * _mouseSensitivity;
            _pitch -= lookDelta.y * _mouseSensitivity; // inverted: mouse up = look up
            _pitch  = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);

            _cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}
