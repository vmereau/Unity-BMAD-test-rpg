using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
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

        private Camera _mainCamera;
        private IInteractable _previousInteractable;
        private InputSystem_Actions _input;
        private float _scanTimer;
        private RaycastHit[] _sphereHitBuffer = new RaycastHit[16];

        private RaycastHit[] _nameTagHitBuffer = new RaycastHit[16];
        private readonly HashSet<IInteractable> _nameTagSeen = new HashSet<IInteractable>();
        private readonly List<EntityUI> _activeUIs = new List<EntityUI>();
        private readonly HashSet<EntityUI> _uiToHide = new HashSet<EntityUI>();

        public IInteractable CurrentInteractable { get; private set; }

        private GUIStyle _promptStyle;

        private void OnEnable()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
        }

        private void OnDisable()
        {
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

            if (best != _previousInteractable)
            {
                CurrentInteractable = best;
                _previousInteractable = best;
                _crosshairImage.color = best != null ? _highlightColor : _defaultColor;
            }

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
            if (!CursorManager.IsLocked) return;
            // Re-check CanInteract: the scan is throttled by _config.scanInterval, so combat could have
            // started on the cached target since the last scan.
            if (CurrentInteractable != null && CurrentInteractable.CanInteract
                && _input.Player.Interact.WasPressedThisFrame())
                CurrentInteractable.Interact();
        }

        private void OnGUI()
        {
            if (CurrentInteractable != null)
            {
                if (_promptStyle == null)
                {
                    _promptStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 20,
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    };
                    _promptStyle.normal.textColor = Color.white;
                }

                // Draw prompt near crosshair (slightly below center)
                GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height * 0.55f, 400, 30),
                    $"[E] {CurrentInteractable.InteractPrompt}", _promptStyle);
            }
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
