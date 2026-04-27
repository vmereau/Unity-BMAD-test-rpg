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

        public IInteractable CurrentInteractable { get; private set; }

        private GUIStyle _promptStyle;


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

            if (best == _previousInteractable) return;

            CurrentInteractable = best;
            _previousInteractable = best;
            _crosshairImage.color = best != null ? _highlightColor : _defaultColor;
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
        }

        private void OnGUI()
        {
            if (CurrentInteractable == null) return;

            if (_promptStyle == null)
                _promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };

            GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height * 0.55f, 400, 30),
                CurrentInteractable.InteractPrompt, _promptStyle);
        }
    }
}
