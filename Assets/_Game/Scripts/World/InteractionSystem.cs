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
        [SerializeField] private Color _defaultColor = Color.white;
        [SerializeField] private Color _highlightColor = Color.yellow;

        [SerializeField] private LayerMask _raycastMask;

        private Camera _mainCamera;
        private IInteractable _previousInteractable;
        private InputSystem_Actions _input;

        public IInteractable CurrentInteractable { get; private set; }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private GUIStyle _promptStyle;
#endif

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
            Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            IInteractable found;

            if (Physics.Raycast(ray, out RaycastHit hitInfo, _config.interactionRange, _raycastMask))
            {
                found = hitInfo.collider.GetComponentInParent<IInteractable>();
            }
            else
            {
                found = null;
            }

            if (found == _previousInteractable) return;

            CurrentInteractable = found;
            _previousInteractable = found;
            _crosshairImage.color = found != null ? _highlightColor : _defaultColor;
        }

        private void LateUpdate()
        {
            if (CurrentInteractable != null && _input.Player.Interact.WasPressedThisFrame())
                CurrentInteractable.Interact();
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (CurrentInteractable == null) return;

            if (_promptStyle == null)
                _promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };

            GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height * 0.55f, 400, 30),
                CurrentInteractable.InteractPrompt, _promptStyle);
        }
#endif
    }
}
