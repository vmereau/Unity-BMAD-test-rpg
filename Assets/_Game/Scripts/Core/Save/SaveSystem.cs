using System.Collections.Generic;
using Game.Inventory;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Save / load orchestrator singleton (on the SaveSystem GameObject in Core.unity).
    /// Currently only exposes the item catalog and the consumed-object set used by world objects;
    /// slot saving / loading and region flow are added by the save/load spec's Phase C.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        private const string TAG = "[Save]";

        public static SaveSystem Instance { get; private set; }

        [SerializeField] private ItemCatalogSO _catalog;

        // Save keys of scene-authored objects removed from the world (e.g. picked-up items).
        private readonly HashSet<string> _consumed = new HashSet<string>();

        public ItemCatalogSO Catalog => _catalog;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                GameLog.Warn(TAG, "Duplicate SaveSystem detected — destroying new instance");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_catalog == null)
                GameLog.Error(TAG, "ItemCatalog not assigned — saved items cannot be restored");
        }

        /// <summary>Records that a scene-authored saveable object was removed from the world.</summary>
        public void MarkConsumed(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                GameLog.Warn(TAG, "MarkConsumed called with an empty save key — ignored");
                return;
            }
            _consumed.Add(key);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
