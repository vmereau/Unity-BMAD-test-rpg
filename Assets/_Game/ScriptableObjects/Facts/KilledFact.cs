using Game.AI;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Key format: Killed.{guid}
    /// Assign one KilledFact asset per tracked entity. The GUID uniquely identifies
    /// the entity across sessions. Use the Generate GUID context menu to create one.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Facts/Killed Fact", fileName = "KilledFact_")]
    public class KilledFact : Fact
    {
        [SerializeField] private string _guid;
        [SerializeField] private MonsterEntity monsterType;

        /// <summary>The entity's unique identifier.</summary>
        public string EntityGuid => _guid;
        public MonsterEntity MonsterType => monsterType;

        public KilledFact Init(string guid, MonsterEntity monsterType = null)
        {
            Prefix = WorldFactPrefix.Killed;
            _guid = guid;
            this.monsterType = monsterType;
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
