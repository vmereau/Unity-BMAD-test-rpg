using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Generic screen-space speech bubbles (<c>UICanvas/Game/SpeechBubbleLayer</c>). Any system raises a
    /// <see cref="SpeechBubbleRequest"/> on <c>OnSpeechBubbleRequested</c>; this layer shows a pooled
    /// <see cref="SpeechBubbleEntryUI"/> above the speaker (constant pixel size), fades it in / holds / fades it out.
    /// One bubble per speaker: a new request replaces it in place unless its priority is lower. Hidden behind the
    /// camera, beyond <c>_maxVisibleDistance</c> or while the cursor is unlocked (menus); released when the speaker
    /// is destroyed / disabled. No allocation per request or per frame once the pool is warm.
    /// </summary>
    public class SpeechBubbleUI : MonoBehaviour
    {
        private const string TAG = "[SpeechBubble]";

        [Header("Event Channels")]
        [SerializeField] private GameEventSO_SpeechBubbleRequest _onSpeechBubbleRequested;

        [Header("Pool")]
        [SerializeField] private SpeechBubbleEntryUI _entryPrefab;
        [SerializeField] private RectTransform _container;
        [SerializeField] private int _prewarmCount = 4;
        [SerializeField] private int _maxBubbles = 6;

        [Header("Timing")]
        [Tooltip("Hold time when a request has duration ≤ 0.")]
        [SerializeField] private float _defaultDuration = 3f;
        [SerializeField] private float _fadeInTime = 0.15f;
        [SerializeField] private float _fadeOutTime = 0.4f;

        [Header("Layout")]
        [Tooltip("Pixel offset from the anchor's screen point. The entry pivot is bottom-centre, so it grows upward.")]
        [SerializeField] private Vector2 _screenOffset = new Vector2(0f, 8f);
        [SerializeField] private float _screenMargin = 16f;
        [Tooltip("Bubbles farther than this from the camera (m) are hidden.")]
        [SerializeField] private float _maxVisibleDistance = 25f;

        private class ActiveBubble
        {
            public SpeechBubbleEntryUI Entry;
            public Transform Speaker;
            public float AnchorHeight;
            public float Elapsed;
            public float Duration;
            public int Priority;
        }

        private readonly List<ActiveBubble> _active = new List<ActiveBubble>();
        private readonly Stack<SpeechBubbleEntryUI> _pool = new Stack<SpeechBubbleEntryUI>();
        private readonly Stack<ActiveBubble> _recordPool = new Stack<ActiveBubble>();
        private Camera _camera;

        private void Awake()
        {
            if (_container == null) _container = transform as RectTransform;
            if (_entryPrefab == null || _container == null)
            {
                GameLog.Error(TAG, "SpeechBubbleUI: _entryPrefab or _container not assigned — bubbles disabled");
                enabled = false;
                return;
            }

            _camera = Camera.main;
            if (_camera == null)
            {
                GameLog.Error(TAG, "SpeechBubbleUI: Camera.main not found — bubbles disabled");
                enabled = false;
                return;
            }

            if (_maxBubbles < 1) _maxBubbles = 1;
            for (int i = 0; i < _prewarmCount; i++)
            {
                _pool.Push(CreateEntry());
                _recordPool.Push(new ActiveBubble());
            }

            if (_onSpeechBubbleRequested == null)
                GameLog.Warn(TAG, "SpeechBubbleUI: _onSpeechBubbleRequested not assigned — bubbles will never show");
        }

        private void OnEnable()
        {
            if (_onSpeechBubbleRequested != null) _onSpeechBubbleRequested.AddListener(HandleSpeechBubbleRequested);
        }

        private void OnDisable()
        {
            if (_onSpeechBubbleRequested != null) _onSpeechBubbleRequested.RemoveListener(HandleSpeechBubbleRequested);
            for (int i = _active.Count - 1; i >= 0; i--) Release(i);
        }

        private void HandleSpeechBubbleRequested(SpeechBubbleRequest request)
        {
            if (request.speaker == null || string.IsNullOrEmpty(request.text))
            {
                GameLog.Warn(TAG, "SpeechBubbleUI: request with a null speaker or empty text — ignored");
                return;
            }

            float duration = SpeechBubbleRules.ResolveDuration(request.duration, _defaultDuration);
            int existing = IndexOf(request.speaker);
            if (existing >= 0)
            {
                ActiveBubble bubble = _active[existing];
                if (!SpeechBubbleRules.ShouldReplace(true, bubble.Priority, request.priority)) return;
                // Restart from the current alpha (computed with the old duration) so the bubble never flashes.
                bubble.Elapsed = SpeechBubbleRules.RestartElapsed(bubble.Elapsed, bubble.Duration, _fadeInTime, _fadeOutTime);
                bubble.Priority = request.priority;
                bubble.Duration = duration;
                bubble.AnchorHeight = request.anchorHeight;
                bubble.Entry.SetText(request.text);
                return;
            }

            if (_active.Count >= _maxBubbles) Release(IndexOfOldest());

            ActiveBubble record = _recordPool.Count > 0 ? _recordPool.Pop() : new ActiveBubble();
            record.Entry = _pool.Count > 0 ? _pool.Pop() : CreateEntry();
            record.Speaker = request.speaker;
            record.AnchorHeight = request.anchorHeight;
            record.Elapsed = 0f;
            record.Duration = duration;
            record.Priority = request.priority;
            record.Entry.SetAlpha(0f);
            record.Entry.SetVisible(true);
            record.Entry.SetText(request.text);
            _active.Add(record);
        }

        private void LateUpdate()
        {
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            float maxDistSqr = _maxVisibleDistance * _maxVisibleDistance;
            Vector3 cameraPos = _camera.transform.position;
            bool cursorLocked = CursorManager.IsLocked;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ActiveBubble bubble = _active[i];
                if (bubble.Speaker == null || !bubble.Speaker.gameObject.activeInHierarchy ||
                    SpeechBubbleRules.IsExpired(bubble.Elapsed, bubble.Duration, _fadeInTime, _fadeOutTime))
                {
                    Release(i);
                    continue;
                }
                bubble.Elapsed += Time.deltaTime; // bubbles pause with the game

                Vector3 anchor = bubble.Speaker.position + Vector3.up * bubble.AnchorHeight;
                Vector3 screen = _camera.WorldToScreenPoint(anchor);
                bool visible = cursorLocked && screen.z > 0f && (anchor - cameraPos).sqrMagnitude <= maxDistSqr;

                if (visible) Place(bubble.Entry.Rect, screen, screenSize);
                bubble.Entry.SetAlpha(visible
                    ? SpeechBubbleRules.Alpha(bubble.Elapsed, bubble.Duration, _fadeInTime, _fadeOutTime)
                    : 0f);
            }
        }

        private void Place(RectTransform rect, Vector3 screen, Vector2 screenSize)
        {
            Vector2 pos = InteractionPromptUI.ClampToScreen((Vector2)screen + _screenOffset, rect.rect.size, rect.pivot,
                                                            screenSize, _screenMargin);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, pos, null, out Vector2 local)) return;
            // local is relative to the container's pivot; anchoredPosition is relative to the entry's anchor.
            Rect parent = _container.rect;
            Vector2 anchorRef = parent.min + Vector2.Scale(parent.size, rect.anchorMin);
            Vector2 anchored = local - anchorRef;
            if ((anchored - rect.anchoredPosition).sqrMagnitude > 0.25f)
                rect.anchoredPosition = anchored;
        }

        private SpeechBubbleEntryUI CreateEntry()
        {
            SpeechBubbleEntryUI entry = Instantiate(_entryPrefab, _container);
            entry.SetAlpha(0f);
            entry.SetVisible(false);
            return entry;
        }

        private int IndexOf(Transform speaker)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Speaker == speaker) return i;
            return -1;
        }

        private int IndexOfOldest()
        {
            int oldest = 0;
            for (int i = 1; i < _active.Count; i++)
                if (_active[i].Elapsed > _active[oldest].Elapsed) oldest = i;
            return oldest;
        }

        private void Release(int index)
        {
            ActiveBubble bubble = _active[index];
            _active.RemoveAt(index);
            if (bubble.Entry != null)
            {
                bubble.Entry.SetAlpha(0f);
                bubble.Entry.SetVisible(false);
                _pool.Push(bubble.Entry);
            }
            bubble.Entry = null;
            bubble.Speaker = null;
            _recordPool.Push(bubble);
        }
    }
}
