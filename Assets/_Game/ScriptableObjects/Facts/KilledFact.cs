using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Key format: Killed.{guid}
    /// Pure persistence identity for a tracked entity (GUID + Killed prefix). The entity
    /// definition (XpOnKill, etc.) lives solely on PersistentID.Entity — not here.
    /// Assign one KilledFact asset per tracked entity. The GUID uniquely identifies
    /// the entity across sessions. Use the Generate GUID context menu to create one.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Facts/Killed Fact", fileName = "KilledFact_")]
    public class KilledFact : Fact
    {
        [SerializeField] private string _guid;

        /// <summary>The entity's unique identifier.</summary>
        public string EntityGuid => _guid;

        public KilledFact Init(string guid)
        {
            Prefix = WorldFactPrefix.Killed;
            _guid = guid;
            return this;
        }

        private void OnEnable() => Prefix = WorldFactPrefix.Killed;

        public override string ToString() => $"{WorldFactPrefix.Killed}.{_guid}";

#if UNITY_EDITOR
        [ContextMenu("Generate GUID")]
        private void GenerateGUID()
        {
            _guid = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
