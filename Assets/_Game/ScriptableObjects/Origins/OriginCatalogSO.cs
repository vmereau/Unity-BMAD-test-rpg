using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>The origins offered at New Game, in display order, plus the one used when none was picked.</summary>
    [CreateAssetMenu(menuName = "Game/Player/Origin Catalog", fileName = "OriginCatalog")]
    public class OriginCatalogSO : ScriptableObject
    {
        private const string TAG = "[Origin]";

        [SerializeField] private List<OriginSO> _origins = new List<OriginSO>();
        [Tooltip("Used when no origin was picked (Play in Core, failed menu load) or the id is unknown.")]
        [SerializeField] private OriginSO _defaultOrigin;

        public IReadOnlyList<OriginSO> origins => _origins;
        public OriginSO defaultOrigin => _defaultOrigin;

        /// <summary>The default origin, or the first listed one when no default is set.</summary>
        public OriginSO DefaultOrFirst
        {
            get
            {
                if (_defaultOrigin != null) return _defaultOrigin;
                foreach (var origin in _origins)
                    if (origin != null) return origin;
                return null;
            }
        }

        /// <summary>The origin with <paramref name="originId"/>; the default when the id is empty or unknown.</summary>
        public OriginSO Find(string originId)
        {
            if (!string.IsNullOrEmpty(originId))
            {
                foreach (var origin in _origins)
                    if (origin != null && origin.originId == originId) return origin;
                GameLog.Warn(TAG, $"Unknown origin '{originId}' — using the default");
            }
            var fallback = DefaultOrFirst;
            if (fallback == null) GameLog.Error(TAG, "OriginCatalog has no origin — none can be applied");
            return fallback;
        }

        /// <summary>Test / tooling setup.</summary>
        public void Init(List<OriginSO> origins, OriginSO defaultOrigin)
        {
            _origins = origins ?? new List<OriginSO>();
            _defaultOrigin = defaultOrigin;
        }
    }
}
