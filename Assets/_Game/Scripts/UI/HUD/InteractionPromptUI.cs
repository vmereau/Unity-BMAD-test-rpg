using Game.Combat;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// HUD prompt card ("[E] Verb / Name") anchored on screen above the focused interactable.
    /// Driven by <c>OnInteractionFocusChanged</c> (raised by InteractionSystem): texts are set only when
    /// the focus event fires; LateUpdate just tracks the target's screen position and fades the card.
    /// Hidden while the cursor is unlocked (menus / inventory open). An illegal (theft) focus draws the verb and name in
    /// <c>_illegalColor</c>; the key label keeps its colour.
    /// A second row <c>[F] Execute</c> (spec-execute-unconscious-npc) shows while <see cref="PlayerExecution.CurrentTarget"/>
    /// is set (polled, like AimSpreadUI). PlayerExecution only keeps a target that is the focused entity or, with no focus,
    /// the nearest body — so the row always matches what F does. With no focus the card anchors above the body's chest.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        private const string TAG = "[InteractionPrompt]";

        [Header("Event Channels")]
        [SerializeField] private GameEventSO_InteractionFocus _onFocusChanged;

        [Header("UI References")]
        [SerializeField] private RectTransform _card;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _verbText;
        [SerializeField] private TMP_Text _nameText;
        [Tooltip("Row holding the [E] key badge + verb / name. Hidden when only the execute row shows.")]
        [SerializeField] private GameObject _primaryRow;
        [Tooltip("[F] Execute row (spec-execute-unconscious-npc).")]
        [SerializeField] private GameObject _executeRow;

        [Header("Execute")]
        [Tooltip("Player-side execute (Player.prefab override). Null → the execute row never shows.")]
        [SerializeField] private PlayerExecution _playerExecution;
        [Tooltip("Card anchor height above the lying body's chest when the execute row shows alone.")]
        [SerializeField] private float _executeAnchorHeight = 0.6f;

        [Header("Layout")]
        [SerializeField] private Vector2 _screenOffset = new Vector2(0f, 24f);
        [SerializeField] private float _screenMargin = 16f;
        [SerializeField] private float _fadeSpeed = 10f;

        [Header("Style")]
        [Tooltip("Verb / name colour when the focused interaction is a theft (owned by a living NPC).")]
        [SerializeField] private Color _illegalColor = new Color(1f, 0.35f, 0.3f, 1f);

        private Camera _camera;
        private RectTransform _parentRect;
        private Component _target;
        private Collider _anchorCollider;
        private bool _primaryShown = true;
        private bool _executeShown;
        private Color _verbDefaultColor;
        private Color _nameDefaultColor;

        private void Awake()
        {
            if (_card == null || _canvasGroup == null || _verbText == null || _nameText == null)
            {
                GameLog.Error(TAG, "InteractionPromptUI: _card, _canvasGroup, _verbText or _nameText not assigned — prompt disabled");
                enabled = false;
                return;
            }

            _verbDefaultColor = _verbText.color;
            _nameDefaultColor = _nameText.color;

            _camera = Camera.main;
            if (_camera == null)
            {
                GameLog.Error(TAG, "InteractionPromptUI: Camera.main not found — prompt disabled");
                enabled = false;
                return;
            }

            _parentRect = _card.parent as RectTransform;
            if (_parentRect == null)
            {
                GameLog.Error(TAG, "InteractionPromptUI: _card has no RectTransform parent — prompt disabled");
                enabled = false;
                return;
            }

            _canvasGroup.alpha = 0f;
            if (_executeRow != null) _executeRow.SetActive(false);
            if (_primaryRow == null || _executeRow == null || _playerExecution == null)
                GameLog.Warn(TAG, "InteractionPromptUI: _primaryRow, _executeRow or _playerExecution not assigned — no [F] Execute row");

            if (_onFocusChanged == null)
                GameLog.Warn(TAG, "InteractionPromptUI: _onFocusChanged not assigned — the prompt will never show");
        }

        private void OnEnable()
        {
            _onFocusChanged?.AddListener(HandleFocusChanged);
        }

        private void OnDisable()
        {
            _onFocusChanged?.RemoveListener(HandleFocusChanged);
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        private void HandleFocusChanged(InteractionFocusData data)
        {
            if (_verbText == null || _nameText == null) return;

            _target = data.target;
            _anchorCollider = data.target != null ? data.target.GetComponentInChildren<Collider>() : null;

            _verbText.text = data.verb;
            _nameText.text = data.name;
            _verbText.color = data.illegal ? _illegalColor : _verbDefaultColor;
            _nameText.color = data.illegal ? _illegalColor : _nameDefaultColor;
            _nameText.gameObject.SetActive(!string.IsNullOrEmpty(data.name));
            // Resize now (event-time only) so this frame's screen clamp uses the new card width.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_card);
        }

        private void LateUpdate()
        {
            bool visible = false;
            bool hasFocus = _target != null;
            bool hasExecute = UpdateExecuteRow(hasFocus);

            if ((hasFocus || hasExecute) && CursorManager.IsLocked)
            {
                Vector3 anchor;
                if (hasFocus)
                    anchor = _anchorCollider != null
                        ? _anchorCollider.bounds.center + Vector3.up * _anchorCollider.bounds.extents.y
                        : _target.transform.position;
                else
                    anchor = _playerExecution.CurrentTargetPoint + Vector3.up * _executeAnchorHeight;
                Vector3 screen = _camera.WorldToScreenPoint(anchor);

                if (screen.z > 0f)
                {
                    visible = true;
                    Vector2 pos = ClampToScreen((Vector2)screen + _screenOffset, _card.rect.size, _card.pivot,
                                                new Vector2(Screen.width, Screen.height), _screenMargin);
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, pos, null, out Vector2 local))
                    {
                        // local is relative to the parent's pivot; anchoredPosition is relative to the card's anchor.
                        Rect parent = _parentRect.rect;
                        Vector2 anchorRef = parent.min + Vector2.Scale(parent.size, _card.anchorMin);
                        Vector2 anchored = local - anchorRef;
                        if ((anchored - _card.anchoredPosition).sqrMagnitude > 0.25f)
                            _card.anchoredPosition = anchored;
                    }
                }
            }

            float alpha = Mathf.MoveTowards(_canvasGroup.alpha, visible ? 1f : 0f, _fadeSpeed * Time.unscaledDeltaTime);
            if (!Mathf.Approximately(alpha, _canvasGroup.alpha))
                _canvasGroup.alpha = alpha;
        }

        // Polls the execute target and toggles the rows (re-laying the card) only when what shows changes. With nothing to
        // show the rows are left as they are, so the fading card keeps its last text. Returns whether an execute target exists.
        private bool UpdateExecuteRow(bool hasFocus)
        {
            if (_primaryRow == null || _executeRow == null || _playerExecution == null) return false;

            bool executeShown = _playerExecution.CurrentTarget != null;
            if (!hasFocus && !executeShown) return false;
            if (executeShown == _executeShown && hasFocus == _primaryShown) return executeShown;

            _executeShown = executeShown;
            _primaryShown = hasFocus;
            _executeRow.SetActive(executeShown);
            _primaryRow.SetActive(hasFocus);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_card);
            return executeShown;
        }

        /// <summary>
        /// Clamps a card's pivot position so the card rect <c>[pos - size*pivot, pos + size*(1-pivot)]</c>
        /// stays inside <c>[margin, screen - margin]</c> on each axis.
        /// </summary>
        public static Vector2 ClampToScreen(Vector2 pos, Vector2 size, Vector2 pivot, Vector2 screen, float margin)
        {
            return new Vector2(
                ClampAxis(pos.x, size.x, pivot.x, screen.x, margin),
                ClampAxis(pos.y, size.y, pivot.y, screen.y, margin));
        }

        private static float ClampAxis(float pos, float size, float pivot, float screen, float margin)
        {
            float min = margin + size * pivot;
            float max = screen - margin - size * (1f - pivot);
            // Card larger than the screen: keep its leading edge on the margin.
            if (max < min) return min;
            return Mathf.Clamp(pos, min, max);
        }
    }
}
