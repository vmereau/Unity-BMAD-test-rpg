using System.Collections;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Interactable door that smoothly rotates its Visual child between its authored closed pose
    /// and an open angle. When unlocked (or no sibling Lockable), toggling happens locally with no
    /// event. When a sibling Lockable reports locked, it raises a GameEventSO_DoorOpenRequest so the
    /// player-side DoorSystem can resolve the skill check (mirrors ContainerInteractable).
    /// </summary>
    public class DoorInteractable : MonoBehaviour, IInteractable
    {
        private const string TAG = "[Door]";

        [SerializeField] private Transform _visual;
        [SerializeField] private float _openAngle = 90f;
        [SerializeField] private float _openDuration = 0.5f;
        [SerializeField] private string _interactPrompt = "Open Door";
        [SerializeField] private string _closePrompt = "Close Door";
        [SerializeField] private GameEventSO_DoorOpenRequest _onDoorOpenRequested;

        private Lockable _lockable;
        private bool _isOpen;
        private Coroutine _rotateRoutine;
        private Quaternion _closedRotation = Quaternion.identity; // authored rest pose, captured in Awake

        public string InteractPrompt
        {
            get
            {
                if (_lockable != null && _lockable.IsLocked) return _lockable.LockedPrompt;
                return _isOpen ? _closePrompt : _interactPrompt;
            }
        }

        public string NameTag => "";
        public bool CanInteract => true;

        private void Awake()
        {
            _lockable = GetComponent<Lockable>(); // optional → null means never locked
            if (_visual == null)
            {
                GameLog.Warn(TAG, $"{gameObject.name} has no Visual transform assigned — door cannot rotate");
                return;
            }
            // Capture the authored rest pose as "closed" so a non-identity Visual rotation is respected.
            _closedRotation = _visual.localRotation;
        }

        public void Interact()
        {
            if (_lockable == null || !_lockable.IsLocked)
            {
                ToggleOpen();
                return;
            }

            if (_onDoorOpenRequested == null)
            {
                GameLog.Warn(TAG, $"{gameObject.name} is locked but no door-open event assigned — cannot request unlock");
                return;
            }

            _onDoorOpenRequested.Raise(new DoorOpenRequestData
            {
                door = this,
                isLocked = true,
                requiredSkillId = _lockable.RequiredSkillId
            });
        }

        /// <summary>Unlocks the sibling Lockable, if any. Safe to call when already unlocked.</summary>
        public void Unlock() => _lockable?.Unlock();

        /// <summary>
        /// Public so the same-system DoorSystem can drive an open after a successful skill check.
        /// Starts/replaces the rotation coroutine toward the closed/open pose and flips _isOpen.
        /// </summary>
        public void ToggleOpen()
        {
            if (_visual == null)
            {
                GameLog.Warn(TAG, $"{gameObject.name} has no Visual transform — cannot toggle");
                return;
            }
            if (!gameObject.activeInHierarchy) return; // coroutines silently stop when disabled

            if (_rotateRoutine != null) StopCoroutine(_rotateRoutine);

            _isOpen = !_isOpen;
            _rotateRoutine = StartCoroutine(RotateCoroutine(PoseFor(_isOpen)));
        }

        // Closed = authored rest pose; open = rest pose rotated by _openAngle around local Y.
        private Quaternion PoseFor(bool open) =>
            open ? _closedRotation * Quaternion.Euler(0f, _openAngle, 0f) : _closedRotation;

        private IEnumerator RotateCoroutine(Quaternion end)
        {
            Quaternion start = _visual.localRotation;

            // Per-frame lerp (yield return null) — the cached-WaitForSeconds rule does not apply here.
            float elapsed = 0f;
            while (elapsed < _openDuration)
            {
                elapsed += Time.deltaTime;
                float t = _openDuration > 0f ? Mathf.Clamp01(elapsed / _openDuration) : 1f;
                _visual.localRotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }

            _visual.localRotation = end; // snap to exact target
            _rotateRoutine = null;
        }

        private void OnDisable()
        {
            // Unity silently stops coroutines on disable. Snap to the logical pose so _isOpen and the
            // transform never desync, and clear the stale handle (F4).
            if (_rotateRoutine == null) return;
            _rotateRoutine = null;
            if (_visual != null) _visual.localRotation = PoseFor(_isOpen);
        }
    }
}
