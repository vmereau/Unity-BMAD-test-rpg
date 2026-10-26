using System.Collections.Generic;
using Game.StatusEffects;
using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode
{
    /// <summary>
    /// Data validation: every StatusEffectSO asset is in the hand-maintained StatusEffectCatalog with a unique
    /// effectId — otherwise its active instances are silently dropped on load.
    /// </summary>
    public class StatusEffectCatalogTests
    {
        private const string CATALOG_PATH = "Assets/_Game/Data/StatusEffects/StatusEffectCatalog.asset";

        [Test]
        public void Catalog_ListsEveryStatusEffectWithUniqueId()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StatusEffectCatalogSO>(CATALOG_PATH);
            Assert.IsNotNull(catalog, $"No StatusEffectCatalogSO at {CATALOG_PATH}");

            var ids = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:StatusEffectSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var effect = AssetDatabase.LoadAssetAtPath<StatusEffectSO>(path);
                if (effect == null) continue;
                Assert.IsFalse(string.IsNullOrEmpty(effect.effectId), $"{path} has no effectId");
                Assert.IsTrue(ids.Add(effect.effectId), $"Duplicate effectId '{effect.effectId}' ({path})");
                Assert.AreSame(effect, catalog.FindById(effect.effectId), $"{path} is missing from the StatusEffectCatalog");
            }
        }
    }
}
