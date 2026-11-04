using Game.Audio;
using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Owns swim mode: finds the water surface from the Water layer (trigger boxes, surface = top of the box), starts and
    /// ends the swim, runs the Sprint stamina drain and raises the water sounds. Movement stays in PlayerController.
    /// Attach to the Player root. Runs before PlayerController so its frame uses this frame's surface and sprint state.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(PlayerStateManager))]
    public class PlayerSwimming : MonoBehaviour
    {
        private const string TAG = "[Swimming]";
        private const int OVERLAP_BUFFER_SIZE = 8;
        private const float PROBE_HEIGHT = 0.3f;
        private const float PROBE_RADIUS = 0.1f;
        private const float MOVE_INPUT_THRESHOLD_SQR = 0.01f;
        private const float TELEPORT_DISTANCE_SQR = 4f;

        [SerializeField] private PlayerConfigSO _config;
        [SerializeField] private StaminaSystem _staminaSystem;
        [Tooltip("Layers that hold water volumes. Only trigger boxes on these layers count as water.")]
        [SerializeField] private LayerMask _waterMask = 1 << 4;

        [Header("Audio")]
        [SerializeField] private GameEventSO_SfxRequest _onSfxRequested;
        [SerializeField] private SoundCueSO _enterCue;
        [SerializeField] private SoundCueSO _exitCue;
        [SerializeField] private SoundCueSO _strokeCue;

        private readonly Collider[] _overlapBuffer = new Collider[OVERLAP_BUFFER_SIZE];
        private readonly float[] _surfaceBuffer = new float[OVERLAP_BUFFER_SIZE];
        private PlayerStateManager _stateManager;
        private InputSystem_Actions _input;
        private Vector3 _lastPosition;
        private float _surfaceY;
        private float _staminaDebt;
        private float _strokeTravel;
        private bool _sprintExhausted;

        /// <summary>Feet height PlayerController holds while swimming.</summary>
        public float TargetFeetY => SwimRules.FloatFeetY(_surfaceY, _config.swimFloatDepth);

        /// <summary>True while Sprint speeds up the swim. False once exhausted, until Sprint is pressed again.</summary>
        public bool IsSwimSprinting { get; private set; }

        private void Awake()
        {
            _stateManager = GetComponent<PlayerStateManager>();
            if (_config == null)
            {
                GameLog.Error(TAG, "PlayerConfigSO not assigned — PlayerSwimming disabled");
                enabled = false;
                return;
            }
            if (_staminaSystem == null) _staminaSystem = GetComponent<StaminaSystem>();
            if (_staminaSystem == null)
                GameLog.Warn(TAG, "StaminaSystem not found — no fast swimming");
            if (_onSfxRequested == null)
                GameLog.Warn(TAG, "OnSfxRequested channel not assigned — no water sounds");
            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
        }

        private void OnDisable()
        {
            // Nothing would end the swim otherwise: the player would hang at the old float height with every gate closed.
            if (_stateManager != null) _stateManager.SetSwimming(false);
            IsSwimSprinting = false;
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
            Vector3 position = transform.position;
            Vector3 lastPosition = _lastPosition;
            _lastPosition = position;
            bool teleported = (position - lastPosition).sqrMagnitude > TELEPORT_DISTANCE_SQR;

            bool inWater = TryGetSurface(position, out float surfaceY);

            if (!_stateManager.IsSwimming)
            {
                IsSwimSprinting = false;
                if (inWater && !_stateManager.IsDead && !_stateManager.IsDodging
                    && SwimRules.ShouldStart(surfaceY, position.y, _config.swimEnterDepth))
                {
                    _surfaceY = surfaceY;
                    StartSwimming(position, playSplash: !teleported);
                }
                return;
            }

            if (!inWater || SwimRules.ShouldStop(surfaceY, position.y, _config.swimExitDepth))
            {
                StopSwimming(position, wadedOut: inWater);
                return;
            }

            _surfaceY = surfaceY;
            UpdateSprint();
            if (!teleported) UpdateStrokes(position, lastPosition);
        }

        private bool TryGetSurface(Vector3 feet, out float surfaceY)
        {
            int count = Physics.OverlapSphereNonAlloc(feet + Vector3.up * PROBE_HEIGHT, PROBE_RADIUS, _overlapBuffer,
                _waterMask, QueryTriggerInteraction.Collide);
            int surfaces = 0;
            for (int i = 0; i < count; i++)
            {
                Collider water = _overlapBuffer[i];
                if (water.isTrigger) _surfaceBuffer[surfaces++] = water.bounds.max.y;
            }
            return SwimRules.TryGetHighestSurface(_surfaceBuffer, surfaces, out surfaceY);
        }

        private void StartSwimming(Vector3 position, bool playSplash)
        {
            _stateManager.SetSwimming(true);
            _staminaDebt = 0f;
            _strokeTravel = 0f;
            if (playSplash && _onSfxRequested != null) _onSfxRequested.Play(_enterCue, position);
        }

        private void StopSwimming(Vector3 position, bool wadedOut)
        {
            _stateManager.SetSwimming(false);
            IsSwimSprinting = false;
            // No exit sound when the volume vanished (region unload, load teleport).
            if (wadedOut && _onSfxRequested != null) _onSfxRequested.Play(_exitCue, position);
        }

        private void UpdateSprint()
        {
            bool held = _input.Player.Sprint.IsPressed();
            if (!held) _sprintExhausted = false;

            IsSwimSprinting = held && !_sprintExhausted && _staminaSystem != null && _stateManager.CanMove()
                && _input.Player.Move.ReadValue<Vector2>().sqrMagnitude > MOVE_INPUT_THRESHOLD_SQR;
            if (!IsSwimSprinting) return;

            // Whole points only: StaminaSystem.Consume logs every call and raises the HUD event.
            _staminaDebt += _config.swimSprintStaminaPerSecond * Time.deltaTime;
            if (_staminaDebt < 1f) return;
            float whole = Mathf.Floor(_staminaDebt);
            if (!_staminaSystem.HasEnough(whole))
            {
                _sprintExhausted = true;
                IsSwimSprinting = false;
                _staminaDebt = 0f;
                return;
            }
            _staminaSystem.Consume(whole);
            _staminaDebt -= whole;
        }

        private void UpdateStrokes(Vector3 position, Vector3 lastPosition)
        {
            Vector3 travel = position - lastPosition;
            travel.y = 0f;
            _strokeTravel += travel.magnitude;
            if (_strokeTravel < _config.swimStrokeDistance) return;
            _strokeTravel = 0f;
            if (_onSfxRequested != null) _onSfxRequested.Play(_strokeCue, position);
        }
    }
}
