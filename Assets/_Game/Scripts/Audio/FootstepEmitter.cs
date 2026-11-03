using Game.Core;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Distance-based footsteps (Player root, next to the CharacterController). Not animation events: locomotion is a
    /// blend tree, whose blended clips would double-fire them. Raises <c>OnSfxRequested</c> with the walk or run cue.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FootstepEmitter : MonoBehaviour
    {
        private const string TAG = "[FootstepEmitter]";

        [SerializeField] private GameEventSO_SfxRequest _onSfxRequested;
        [SerializeField] private SoundCueSO _walkCue;
        [SerializeField] private SoundCueSO _runCue;

        [Tooltip("Metres between steps while walking.")]
        [SerializeField, Min(0.1f)] private float _walkStride = 1.4f;
        [Tooltip("Metres between steps while running.")]
        [SerializeField, Min(0.1f)] private float _runStride = 1.9f;
        [Tooltip("Horizontal speed (m/s) from which the run cue and stride are used (walk 3, run 6).")]
        [SerializeField, Min(0f)] private float _runThreshold = 4.5f;
        [Tooltip("Below this horizontal speed (m/s) no steps are emitted.")]
        [SerializeField, Min(0f)] private float _minSpeed = 0.5f;
        [Tooltip("Ungrounded time (s) still counted as grounded: CharacterController.isGrounded drops out for single frames on slopes / step edges.")]
        [SerializeField, Min(0f)] private float _groundedGrace = 0.15f;

        private readonly FootstepStride _stride = new();
        private CharacterController _controller;
        private Vector3 _lastPosition;
        private float _ungroundedTime;
        private IFootstepGate _gate;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _gate = GetComponent<IFootstepGate>(); // optional: mutes steps during dodges / attacks / death
            if (_onSfxRequested == null) GameLog.Warn(TAG, "OnSfxRequested channel not assigned — no footsteps");
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            _stride.Reset();
        }

        private void Update()
        {
            Vector3 position = transform.position;
            Vector3 delta = position - _lastPosition;
            _lastPosition = position;
            delta.y = 0f;

            float dt = Time.deltaTime;
            if (dt <= 0f || _onSfxRequested == null) return; // paused

            _ungroundedTime = _controller.isGrounded ? 0f : _ungroundedTime + dt;
            bool grounded = _ungroundedTime <= _groundedGrace;
            if (_gate != null && _gate.SuppressFootsteps) grounded = false; // resets the stride: no step right after a roll

            float distance = delta.magnitude;
            bool running = FootstepStride.IsRunning(distance / dt, _runThreshold);
            float stride = running ? _runStride : _walkStride;
            if (_stride.Advance(distance, dt, grounded, stride, _minSpeed))
                _onSfxRequested.Play(running ? _runCue : _walkCue, position);
        }
    }
}
