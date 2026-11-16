using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    /// <summary>
    /// Execute stomp (spec-execute-unconscious-npc): in combat, hold F at an unconscious NPC to stomp it. The player
    /// slides / turns so the clip's landing foot hits the body's chest; the kill happens only at the clip's
    /// <c>ExecuteImpact</c> event. Releasing F, losing the target, taking damage or being interrupted before it cancels.
    /// Target: the focused interactable when it is an executable body in reach; with no focus, the nearest one in the
    /// cone; a focus on anything else = no target (the HUD row and F always agree). <see cref="CurrentTarget"/> feeds the
    /// HUD <c>[F] Execute</c> row (polled). Attach to the Player root.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerExecution : MonoBehaviour
    {
        private const string TAG = "[Execute]";
        private const int BUFFER_SIZE = 32;

        [SerializeField] private CombatConfigSO _config;
        [SerializeField] private PlayerStateManager _stateManager;
        [Tooltip("Damage taken mid-stomp cancels it (Player root PlayerHealth).")]
        [SerializeField] private PlayerHealth _playerHealth;
        [Tooltip("OnInteractionFocusChanged — the focused body wins over the nearest one.")]
        [SerializeField] private GameEventSO_InteractionFocus _onFocusChanged;

        /// <summary>The body F would execute now (null = none). Kept during an execute until the impact.</summary>
        public Component CurrentTarget => _targetComponent;

        /// <summary>Where the HUD anchors the execute row: the body's chest (valid while <see cref="CurrentTarget"/> is set).</summary>
        public Vector3 CurrentTargetPoint => _target != null && _targetComponent != null ? _target.ExecutePoint : transform.position;

        private Collider[] _buffer = new Collider[BUFFER_SIZE];
        private readonly RaycastHit[] _losHits = new RaycastHit[BUFFER_SIZE];
        private Collider[] _ignoredBody;
        private InputSystem_Actions _input;
        private CharacterController _characterController;
        private int _targetMask;

        private Component _focus;
        private IExecutable _focusExecutable;

        private IExecutable _target;
        private Component _targetComponent;

        private bool _executing;
        private bool _impactDone;
        private float _elapsed;
        private uint _damageAtStart;
        private bool _aligned;
        private Vector3 _alignFrom;
        private Vector3 _alignTo;
        private Quaternion _rotFrom;
        private Quaternion _rotTo;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_config == null || _stateManager == null)
            {
                GameLog.Error(TAG, "CombatConfigSO or PlayerStateManager not assigned — execute disabled");
                enabled = false;
                return;
            }
            if (_playerHealth == null)
                GameLog.Warn(TAG, "PlayerHealth not assigned — damage won't cancel a stomp");
            if (_onFocusChanged == null)
                GameLog.Warn(TAG, "_onFocusChanged not assigned — the nearest body is always the target");
            _targetMask = LayerMask.GetMask(GameConstants.CHARACTER_HITBOX_LAYER_NAME);
            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            if (_config == null || _stateManager == null) return;
            _onFocusChanged?.AddListener(HandleFocusChanged);
            if (_input == null) _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.Player.Execute.started += OnExecuteStarted;
            _input.Player.Execute.canceled += OnExecuteCanceled;
        }

        private void OnDisable()
        {
            _onFocusChanged?.RemoveListener(HandleFocusChanged);
            if (_executing) EndExecute("disabled");
            ClearTarget();
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Execute.started -= OnExecuteStarted;
            _input.Player.Execute.canceled -= OnExecuteCanceled;
            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
            if (!_executing)
            {
                FindTarget();
                return;
            }

            _elapsed += Time.deltaTime;

            if (!_impactDone && TryGetCancelReason(out string reason))
            {
                EndExecute(reason);
                return;
            }
            if (_impactDone && !_stateManager.CanContinueExecute)
            {
                EndExecute("interrupted after the kill");
                return;
            }
            if (_elapsed > _config.executeClipDuration + _config.executeWatchdogMargin)
            {
                GameLog.Warn(TAG, "ExecuteEnd event never arrived — watchdog ends the execute");
                EndExecute("watchdog");
                return;
            }

            Align();
        }

        // ── Input / events ────────────────────────────────────────────────────

        private void HandleFocusChanged(InteractionFocusData data)
        {
            _focus = data.target;
            _focusExecutable = data.target != null ? data.target.GetComponentInParent<IExecutable>() : null;
        }

        private void OnExecuteStarted(InputAction.CallbackContext ctx)
        {
            if (_executing || _config == null || _stateManager == null) return;
            FindTarget(); // Input may arrive after movement or a focus change since the last scan.
            if (_executing || _targetComponent == null || !_stateManager.CanExecute() || !_target.CanBeExecuted) return;

            var (pos, yaw) = ExecuteMath.AlignStance(transform.position, transform.forward, _target.ExecutePoint,
                _config.executeFootOffset);
            _alignFrom = transform.position;
            _alignTo = pos;
            _rotFrom = transform.rotation;
            _rotTo = Quaternion.Euler(0f, yaw, 0f);
            _elapsed = 0f;
            _impactDone = false;
            _damageAtStart = _playerHealth != null ? _playerHealth.DamageRevision : 0;
            _aligned = false;
            _executing = true;
            SetBodyCollisionIgnored(true); // the lying body's solid ragdoll colliders would stop the slide short
            _stateManager.SetExecuting(true);
            GameLog.Info(TAG, $"Execute started on {_targetComponent.name}");
        }

        private void OnExecuteCanceled(InputAction.CallbackContext ctx)
        {
            if (_executing && !_impactDone) EndExecute("cancelled — F released");
        }

        // ── Animation events (AnimationEventReceiver) ─────────────────────────

        /// <summary>The foot lands: kill the target if the stomp is still valid this very frame.</summary>
        public void OnExecuteImpact()
        {
            if (!_executing || _impactDone || _elapsed < _config.executeMinEventDelay) return;
            // Re-checked here: a hit resolved after this frame's Update, or a load that just started, must not kill.
            if (TryGetCancelReason(out string reason))
            {
                EndExecute(reason);
                return;
            }
            // Diagnostic only (spec: the kill comes from the clip event): a miss means executeFootOffset needs retuning
            // or the slide was blocked by scenery.
            Vector3 foot = transform.TransformPoint(_config.executeFootOffset);
            if (!ExecuteMath.IsCandidate(foot, transform.forward, _target.ExecutePoint, _config.executeImpactTolerance, 180f))
                GameLog.Warn(TAG, $"Stomp foot lands {Vector3.Distance(new Vector3(foot.x, 0f, foot.z), new Vector3(_target.ExecutePoint.x, 0f, _target.ExecutePoint.z)):F2} m from the body's chest");
            _impactDone = true;
            string targetName = _targetComponent.name;
            if (_target.Execute(gameObject))
                GameLog.Info(TAG, $"{targetName} executed");
            else
                GameLog.Info(TAG, "Impact landed but the target could no longer be executed");
            ClearTarget(); // the body is a corpse now — no [F] row while the clip finishes
        }

        /// <summary>End of the stomp clip: the player is free again.</summary>
        public void OnExecuteEnd()
        {
            if (!_executing || _elapsed < _config.executeMinEventDelay) return;
            EndExecute("finished");
        }

        // ── Internals ─────────────────────────────────────────────────────────

        // Before the impact: everything that must stop the kill.
        private bool TryGetCancelReason(out string reason)
        {
            if (!_stateManager.CanContinueExecute) { reason = "cancelled — player interrupted"; return true; }
            if (_playerHealth != null && _playerHealth.DamageRevision != _damageAtStart) { reason = "cancelled — player hit"; return true; }
            if (SaveSystem.Instance != null && SaveSystem.Instance.IsLoading) { reason = "cancelled — loading"; return true; }
            if (_targetComponent == null || !_target.CanBeExecuted) { reason = "cancelled — target no longer executable"; return true; }
            reason = null;
            return false;
        }

        private void EndExecute(string reason)
        {
            _executing = false;
            _impactDone = false;
            SetBodyCollisionIgnored(false);
            if (_stateManager != null) _stateManager.SetExecuting(false);
            GameLog.Info(TAG, $"Execute ended ({reason})");
        }

        // Slides (CharacterController, so walls stop it) and turns into the stomp stance over executeAlignDuration.
        private void Align()
        {
            if (_aligned) return;
            float duration = _config.executeAlignDuration;
            float before = duration > 0f ? Mathf.Clamp01((_elapsed - Time.deltaTime) / duration) : 0f;
            float now = duration > 0f ? Mathf.Clamp01(_elapsed / duration) : 1f;

            Vector3 delta = Vector3.Lerp(_alignFrom, _alignTo, now) - Vector3.Lerp(_alignFrom, _alignTo, before);
            delta.y = 0f;
            if (_characterController != null && _characterController.enabled) _characterController.Move(delta);
            transform.rotation = Quaternion.Slerp(_rotFrom, _rotTo, now);
            _aligned = now >= 1f;
        }

        private void FindTarget()
        {
            if (!_stateManager.CanExecute())
            {
                ClearTarget();
                return;
            }

            // A focus decides: its own body if executable and in reach, otherwise nothing (F must match the HUD).
            if (_focus != null)
            {
                var focused = _focusExecutable;
                if (focused != null && (Component)focused != null && IsInReach(focused))
                    SetTarget(focused);
                else
                    ClearTarget();
                return;
            }

            Vector3 origin = transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, _config.executeRange + _config.executeScanPadding, _buffer,
                _targetMask, QueryTriggerInteraction.Collide);
            // Grow only on saturation, then retain the buffer for subsequent scans.
            while (count == _buffer.Length)
            {
                System.Array.Resize(ref _buffer, _buffer.Length * 2);
                count = Physics.OverlapSphereNonAlloc(origin, _config.executeRange + _config.executeScanPadding, _buffer,
                    _targetMask, QueryTriggerInteraction.Collide);
            }

            IExecutable best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var candidate = _buffer[i].GetComponentInParent<IExecutable>();
                if (candidate == null || ReferenceEquals(candidate, best)) continue;
                if (((Component)candidate).transform.IsChildOf(transform) || !IsInReach(candidate)) continue;

                Vector3 flat = candidate.ExecutePoint - origin;
                flat.y = 0f;
                if (flat.sqrMagnitude < bestSqr)
                {
                    bestSqr = flat.sqrMagnitude;
                    best = candidate;
                }
            }

            if (best != null) SetTarget(best); else ClearTarget();
        }

        private bool IsInReach(IExecutable candidate) =>
            candidate.CanBeExecuted && ExecuteMath.IsCandidate(transform.position, transform.forward, candidate.ExecutePoint,
                _config.executeRange, _config.executeConeHalfAngle) && HasAccess(candidate);

        // Same floor, and no scenery between the player and the body. The body's own colliders don't count: humanoid
        // ragdoll bones are solid Default colliders and the chest point lies inside them.
        private bool HasAccess(IExecutable candidate)
        {
            Vector3 point = candidate.ExecutePoint;
            if (Mathf.Abs(point.y - transform.position.y) > _config.executeVerticalReach) return false;
            Vector3 origin = transform.position + _characterController.center;
            Vector3 to = point - origin;
            float distance = to.magnitude;
            if (distance < 0.0001f) return true;
            int count = Physics.RaycastNonAlloc(origin, to / distance, _losHits, distance, _config.executeOcclusionMask,
                QueryTriggerInteraction.Ignore);
            Transform body = ((Component)candidate).transform;
            for (int i = 0; i < count; i++)
            {
                Transform hit = _losHits[i].collider.transform;
                if (hit.IsChildOf(body) || hit.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }

        // Lets the CharacterController slide through the target's limbs during the stomp; restored when it ends.
        private void SetBodyCollisionIgnored(bool ignored)
        {
            if (_characterController == null) return;
            if (ignored)
            {
                if (_targetComponent == null) return;
                _ignoredBody = _targetComponent.GetComponentsInChildren<Collider>();
            }
            if (_ignoredBody == null) return;
            foreach (var col in _ignoredBody)
                if (col != null && !col.isTrigger) Physics.IgnoreCollision(_characterController, col, ignored);
            if (!ignored) _ignoredBody = null;
        }

        private void SetTarget(IExecutable target)
        {
            _target = target;
            _targetComponent = (Component)target;
        }

        private void ClearTarget()
        {
            _target = null;
            _targetComponent = null;
        }
    }
}
