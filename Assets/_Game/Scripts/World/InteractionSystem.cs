using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.World
{
    public class InteractionSystem : MonoBehaviour
    {
        private const string TAG = "[Interaction]";

        [SerializeField] private InteractionConfigSO _config;
        [SerializeField] private Image _crosshairImage;
        [SerializeField] private DialogueSystem _dialogueSystem;
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

        private struct NameTagEntry { public string label; public Vector3 worldPos; }
        private NameTagEntry[] _nameTagEntries = new NameTagEntry[16];
        private int _nameTagCount;

        public IInteractable CurrentInteractable { get; private set; }

        private GUIStyle _promptStyle;
        private GUIStyle _nameTagStyle;


        private void OnEnable()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
        }

        private void OnDisable()
        {
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Disable();
            _input.Dispose();
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
            int hitCount = Physics.SphereCastNonAlloc(ray, _config.scanRadius,
                                                      _sphereHitBuffer, _config.interactionRange,
                                                      _raycastMask);

            IInteractable best = null;
            float bestAngle = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var candidate = _sphereHitBuffer[i].collider.GetComponentInParent<IInteractable>();
                if (candidate == null) continue;

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

            // Name-range scan
            _nameTagCount = 0;
            _nameTagSeen.Clear();

            int nameHitCount = Physics.SphereCastNonAlloc(
                ray, _config.scanRadius,
                _nameTagHitBuffer, _config.nameRange,
                _raycastMask);

            for (int i = 0; i < nameHitCount && _nameTagCount < _nameTagEntries.Length; i++)
            {
                var candidate = _nameTagHitBuffer[i].collider.GetComponentInParent<IInteractable>();
                if (candidate == null) continue;
                if (string.IsNullOrEmpty(candidate.NameTag)) continue;
                if (!_nameTagSeen.Add(candidate)) continue; // dedup

                Bounds b = _nameTagHitBuffer[i].collider.bounds;
                _nameTagEntries[_nameTagCount++] = new NameTagEntry
                {
                    label = candidate.NameTag,
                    worldPos = b.center + Vector3.up * (b.extents.y + 0.3f)
                };
            }
        }

        private void LateUpdate()
        {
            if (_dialogueSystem != null && _dialogueSystem.IsOpen) return;
            if (CurrentInteractable != null && _input.Player.Interact.WasPressedThisFrame())
                CurrentInteractable.Interact();
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

        private void OnGUI()
        {
            if (CurrentInteractable != null)
            {
                if (_promptStyle == null)
                    _promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };

                GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height * 0.55f, 400, 30),
                    CurrentInteractable.InteractPrompt, _promptStyle);
            }

            if (_nameTagCount > 0)
            {
                if (_nameTagStyle == null)
                    _nameTagStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 16,
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    };

                for (int i = 0; i < _nameTagCount; i++)
                {
                    Vector3 screenPos = _mainCamera.WorldToScreenPoint(_nameTagEntries[i].worldPos);
                    if (screenPos.z <= 0f) continue; // behind camera

                    float guiY = Screen.height - screenPos.y;
                    GUI.Label(
                        new Rect(screenPos.x - 100f, guiY - 20f, 200f, 25f),
                        _nameTagEntries[i].label,
                        _nameTagStyle);
                }
            }
        }
    }
}
