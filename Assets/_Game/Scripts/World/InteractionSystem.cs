using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Player;
using Game.UI;

namespace Game.World
{
    public class InteractionSystem : MonoBehaviour
    {
        private const string TAG = "[Interaction]";

        [SerializeField] private InteractionConfigSO _config;
        [SerializeField] private Image _crosshairImage;
        [SerializeField] private Color _defaultColor = Color.white;
        [SerializeField] private Color _highlightColor = Color.yellow;

        [SerializeField] private LayerMask _raycastMask;

        [Header("Event Channels")]
        [SerializeField] private GameEventSO_InteractionFocus _onFocusChanged;
        [Tooltip("Raised before an illegal interaction (owned object, owner alive) — NPC witnesses listen to it.")]
        [SerializeField] private GameEventSO_TheftCommitted _onTheftCommitted;

        private static readonly int OutlineColorId =
            Shader.PropertyToID(GameConstants.INTERACTION_OUTLINE_COLOR_PROPERTY);

        private Camera _mainCamera;
        private PlayerStateManager _playerState;

        private bool IsPlayerDead => _playerState != null && _playerState.IsDead;
        private IInteractable _previousInteractable;
        private InputSystem_Actions _input;
        private float _scanTimer;
        private RaycastHit[] _sphereHitBuffer = new RaycastHit[16];

        private RaycastHit[] _nameTagHitBuffer = new RaycastHit[16];
        private readonly HashSet<IInteractable> _nameTagSeen = new HashSet<IInteractable>();
        private readonly List<EntityUI> _activeUIs = new List<EntityUI>();
        private readonly HashSet<EntityUI> _uiToHide = new HashSet<EntityUI>();

        private InteractionHighlight _focusedHighlight;
        private string _focusedVerb = "";
        private string _focusedName = "";
        private uint _outlineBits;
        private Ownership _focusedOwnership;
        private bool _focusedIllegal;

        private static int s_nextTheftId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => s_nextTheftId = 0;

        public IInteractable CurrentInteractable { get; private set; }

        private void OnEnable()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
        }

        private void OnDisable()
        {
            // Before the _input guard: a focused target must lose its outline even if Awake disabled us.
            ClearFocus();

            if (_input == null) return;
            _input.Player.Disable();
            _input.Dispose();
            
            // Cleanup: Hide all active UIs
            foreach (var ui in _activeUIs)
            {
                if (ui != null) ui.Show(false);
            }
            _activeUIs.Clear();
        }

        private void Awake()
        {
            // Same Player rig; null-safe (no state manager → only the cursor gates interaction).
            _playerState = GetComponentInParent<PlayerStateManager>();
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                GameLog.Error(TAG, "Camera.main not found — InteractionSystem disabled");
                enabled = false;
                return;
            }

            if (_config == null)
            {
                GameLog.Error(TAG, "_config is null — InteractionSystem disabled");
                enabled = false;
                return;
            }

            if (_crosshairImage == null)
            {
                GameLog.Error(TAG, "_crosshairImage is null — InteractionSystem disabled");
                enabled = false;
                return;
            }

            if (_raycastMask == 0)
                GameLog.Warn(TAG, "_raycastMask is 0 (Nothing) — no interactables will be detected. Assign the Interactable layer in Inspector.");

            if (_onFocusChanged == null)
                GameLog.Warn(TAG, "_onFocusChanged is null — outline still works but no prompt card will be shown");

            if (_onTheftCommitted == null)
                GameLog.Warn(TAG, "_onTheftCommitted is null — thefts go unnoticed by NPCs");

            _outlineBits = _config.outlineRenderingLayer.value;
            if (_outlineBits == 0)
                GameLog.Warn(TAG, "InteractionConfig.outlineRenderingLayer is Nothing — outline disabled");
        }

        private void Update()
        {
            _scanTimer += Time.deltaTime;
            if (_scanTimer < _config.scanInterval) return;
            _scanTimer = 0f;

            Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            
            // 1. Interaction Scan (Center of Screen)
            int hitCount = Physics.SphereCastNonAlloc(ray, _config.scanRadius,
                                                      _sphereHitBuffer, _config.interactionRange,
                                                      _raycastMask);

            IInteractable best = null;
            float bestAngle = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var candidate = _sphereHitBuffer[i].collider.GetComponentInParent<IInteractable>();
                if (candidate == null) continue;
                if (!candidate.CanInteract) continue; // in-combat NPCs (or dead) offer no prompt/interaction

                Vector3 toCollider = (_sphereHitBuffer[i].collider.bounds.center - ray.origin).normalized;
                float angle = Vector3.Angle(ray.direction, toCollider);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = candidate;
                }
            }

            // Menus / dialogue / containers open: no focus, so the outline and crosshair tint match the hidden card.
            if (!CursorManager.IsLocked || IsPlayerDead) best = null;

            // Also fires when the same target's verb/name/legality changes (e.g. a door's lock prompt after
            // unlocking, or an owner dying while its object is focused).
            string verb = InteractionFocus.ResolveVerb(best);
            string targetName = InteractionFocus.ResolveName(best);
            // TryGetComponent only when the target changes.
            Ownership ownership = ReferenceEquals(best, _previousInteractable) ? _focusedOwnership : GetOwnership(best);
            bool illegal = ownership != null && ownership.IsIllegal;
            if (InteractionFocus.HasFocusChanged(_previousInteractable, _focusedVerb, _focusedName, _focusedIllegal,
                                                 best, verb, targetName, illegal))
                ApplyFocus(best, verb, targetName, illegal, ownership);

            // 2. Name-range scan (Show World-Space UI)
            _uiToHide.Clear();
            foreach (var ui in _activeUIs) _uiToHide.Add(ui);
            _activeUIs.Clear();
            _nameTagSeen.Clear();

            int nameHitCount = Physics.SphereCastNonAlloc(
                ray, _config.scanRadius,
                _nameTagHitBuffer, _config.nameRange,
                _raycastMask);

            for (int i = 0; i < nameHitCount; i++)
            {
                var candidate = _nameTagHitBuffer[i].collider.GetComponentInParent<IInteractable>();
                if (candidate == null) continue;
                if (!_nameTagSeen.Add(candidate)) continue; // dedup

                var comp = (Component)candidate;
                var entityUI = comp.GetComponent<EntityUI>();
                if (entityUI == null) continue;

                // Name is static per entity; health updates itself via EntityHealth.HealthChanged
                // (EntityUI self-subscribes), so the bar stays live whether or not we are scanning it.
                entityUI.SetName(candidate.NameTag);

                // Show world-space UI
                entityUI.Show(true);
                _activeUIs.Add(entityUI);
                _uiToHide.Remove(entityUI);
            }

            // Hide UIs that are no longer in range
            foreach (var ui in _uiToHide)
            {
                if (ui != null) ui.Show(false);
            }
        }

        private void LateUpdate()
        {
            if (!CursorManager.IsLocked || IsPlayerDead) return;
            // Re-check CanInteract: the scan is throttled by _config.scanInterval, so combat could have
            // started on the cached target since the last scan.
            // IsAlive: the target may have been destroyed (e.g. picked up) since the last scan.
            if (InteractionFocus.IsAlive(CurrentInteractable) && CurrentInteractable.CanInteract
                && _input.Player.Interact.WasPressedThisFrame())
            {
                // Before Interact(): a picked-up item destroys itself. Legality re-evaluated at press time.
                if (_focusedOwnership != null && _focusedOwnership.IsIllegal)
                    RaiseTheft(_focusedOwnership);
                CurrentInteractable.Interact();
                // Force a rescan next frame so a destroyed target is dropped before another [E] press.
                _scanTimer = _config.scanInterval;
            }
        }

        private static Ownership GetOwnership(IInteractable interactable)
        {
            if (interactable is Component component && component != null &&
                component.TryGetComponent(out Ownership ownership))
                return ownership;
            return null;
        }

        private void RaiseTheft(Ownership ownership)
        {
            int id = ++s_nextTheftId;
            string objectName = InteractionFocus.ResolveName(CurrentInteractable);
            if (string.IsNullOrEmpty(objectName)) objectName = ownership.gameObject.name; // doors have no name tag
            string ownerName = ownership.Owner != null ? ownership.Owner.name : "nobody";
            GameLog.Info(TAG, $"Theft #{id}: {ownership.Kind} '{objectName}' owned by {ownerName}");
            _onTheftCommitted?.Raise(new TheftCommittedData
            {
                theftId = id,
                thief = _playerState != null ? _playerState.transform : transform.root,
                position = ownership.transform.position,
                owner = ownership.Owner,
                kind = ownership.Kind,
                objectName = objectName,
            });
        }

        private void ApplyFocus(IInteractable next, string verb, string targetName, bool illegal, Ownership ownership)
        {
            if (_focusedHighlight != null) _focusedHighlight.SetHighlighted(false, _outlineBits);

            // GetComponent only when the target itself changes, not on a verb/name refresh.
            if (!ReferenceEquals(next, _previousInteractable))
            {
                _focusedHighlight = null;
                if (next is Component nextComponent && nextComponent != null)
                    nextComponent.TryGetComponent(out _focusedHighlight);
            }

            if (_focusedHighlight != null)
            {
                Shader.SetGlobalColor(OutlineColorId, InteractionFocus.ResolveOutlineColor(illegal,
                    _config.illegalOutlineColor, _focusedHighlight.HasColorOverride, _focusedHighlight.ColorOverride,
                    _config.outlineColor));
                _focusedHighlight.SetHighlighted(true, _outlineBits);
            }

            CurrentInteractable = next;
            _previousInteractable = next;
            _focusedVerb = verb;
            _focusedName = targetName;
            _focusedOwnership = ownership;
            _focusedIllegal = illegal;
            if (_crosshairImage != null)
                _crosshairImage.color = InteractionFocus.SelectCrosshairColor(next != null, _defaultColor, _highlightColor);

            _onFocusChanged?.Raise(new InteractionFocusData
            {
                target = next as Component,
                verb = verb,
                name = targetName,
                illegal = illegal
            });
        }

        private void ClearFocus()
        {
            if (CurrentInteractable == null && _focusedHighlight == null) return;
            ApplyFocus(null, "", "", false, null);
        }

        private void OnDrawGizmos()
{
            Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
            if (cam == null || _config == null) return;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            bool hit = CurrentInteractable != null;

            Gizmos.color = hit ? Color.green : Color.yellow;
            Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * _config.interactionRange);
            Gizmos.DrawWireSphere(ray.origin + ray.direction * _config.interactionRange,
                                  _config.scanRadius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * _config.nameRange);
            Gizmos.DrawWireSphere(
                ray.origin + ray.direction * _config.nameRange,
                _config.scanRadius);
        }
    }
}
