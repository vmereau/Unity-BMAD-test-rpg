using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// One pooled speech-bubble card (<c>SpeechBubble.prefab</c>): black faded rectangle + white wrapped text.
    /// Owned and positioned by <see cref="SpeechBubbleUI"/>.
    /// </summary>
    public class SpeechBubbleEntryUI : MonoBehaviour
    {
        private const string TAG = "[SpeechBubble]";

        [SerializeField] private RectTransform _rect;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private LayoutElement _textLayout;
        [Tooltip("Lines wider than this (px) wrap onto several lines; shorter lines hug the text.")]
        [SerializeField] private float _maxTextWidth = 320f;

        public RectTransform Rect => _rect;

        private void Awake()
        {
            if (_rect == null || _canvasGroup == null || _text == null || _textLayout == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: _rect, _canvasGroup, _text or _textLayout not assigned — entry disabled");
                enabled = false;
            }
        }

        public void SetText(string text)
        {
            if (_text == null || _textLayout == null || _rect == null) return;
            _text.text = text;
            _textLayout.preferredWidth = Mathf.Min(_text.GetPreferredValues(text).x, _maxTextWidth);
            // Resize now (request-time only) so this frame's screen clamp uses the new size.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
        }

        public void SetAlpha(float alpha)
        {
            if (_canvasGroup == null) return;
            if (!Mathf.Approximately(_canvasGroup.alpha, alpha)) _canvasGroup.alpha = alpha;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
