using System.Collections.Generic;
using Game.Core;
using Game.StatusEffects;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// HUD row of active status effects (<c>UICanvas/Game/StatusEffectBar</c>, above the HealthBar): one
    /// <see cref="StatusEffectIconUI"/> per effect type with the stack count and the longest remaining time.
    /// Rebuilt on <c>_onStatusEffectsChanged</c> and every <see cref="_refreshInterval"/> real seconds while effects
    /// are active. Entries are pooled under <see cref="_container"/>.
    /// </summary>
    public class StatusEffectBarUI : MonoBehaviour
    {
        private const string TAG = "[UI]";

        // Cross-system read (like HealthBarUI → PlayerHealth): the event carries no payload.
        [SerializeField] private StatusEffectHost _host;
        [SerializeField] private GameEventSO_Void _onStatusEffectsChanged;
        [SerializeField] private RectTransform _container;
        [SerializeField] private StatusEffectIconUI _iconPrefab;
        [SerializeField, Min(0.1f)] private float _refreshInterval = 0.5f;

        private readonly List<StatusEffectIconUI> _icons = new List<StatusEffectIconUI>();
        private readonly List<StatusEffectSO> _groupEffects = new List<StatusEffectSO>();
        private readonly List<int> _groupCounts = new List<int>();
        private readonly List<double> _groupExpiry = new List<double>();
        private float _nextRefresh;

        private void Awake()
        {
            if (_container == null || _iconPrefab == null)
            {
                GameLog.Error(TAG, "StatusEffectBarUI: _container / _iconPrefab not assigned");
                enabled = false;
                return;
            }
            if (_host == null) GameLog.Warn(TAG, "StatusEffectBarUI: _host not assigned — no effects will show");
            if (_onStatusEffectsChanged == null) GameLog.Warn(TAG, "StatusEffectBarUI: _onStatusEffectsChanged not assigned — the bar only updates when re-enabled");
        }

        private void OnEnable()
        {
            _onStatusEffectsChanged?.AddListener(HandleStatusEffectsChanged);
            Refresh();
        }

        private void OnDisable()
        {
            _onStatusEffectsChanged?.RemoveListener(HandleStatusEffectsChanged);
        }

        private void Update()
        {
            // Timer only for the countdown labels — showing / hiding entries is driven by the event.
            if (_groupEffects.Count == 0 || Time.unscaledTime < _nextRefresh) return;
            Refresh();
        }

        private void HandleStatusEffectsChanged(bool _) => Refresh();

        private void Refresh()
        {
            _nextRefresh = Time.unscaledTime + _refreshInterval;
            if (_container == null || _iconPrefab == null) return;

            _groupEffects.Clear();
            _groupCounts.Clear();
            _groupExpiry.Clear();
            if (_host != null)
            {
                var active = _host.Active;
                for (int i = 0; i < active.Count; i++)
                {
                    var instance = active[i];
                    int g = _groupEffects.IndexOf(instance.Effect);
                    if (g < 0)
                    {
                        _groupEffects.Add(instance.Effect);
                        _groupCounts.Add(1);
                        _groupExpiry.Add(instance.ExpiresAt);
                    }
                    else
                    {
                        _groupCounts[g]++;
                        if (instance.ExpiresAt > _groupExpiry[g]) _groupExpiry[g] = instance.ExpiresAt;
                    }
                }
            }

            double now = _host != null ? _host.Now : 0d;
            while (_icons.Count < _groupEffects.Count)
                _icons.Add(Instantiate(_iconPrefab, _container));
            for (int i = 0; i < _icons.Count; i++)
            {
                bool used = i < _groupEffects.Count;
                if (_icons[i].gameObject.activeSelf != used) _icons[i].gameObject.SetActive(used);
                if (used) _icons[i].Set(_groupEffects[i], _groupCounts[i], _groupExpiry[i] - now);
            }
        }
    }
}
