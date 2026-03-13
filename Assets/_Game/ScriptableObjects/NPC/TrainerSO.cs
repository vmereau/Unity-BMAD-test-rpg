using Game.Player;
using UnityEngine;

namespace Game.NPC
{
    [System.Serializable]
    public struct StatUpgradeEntry
    {
        public StatType stat;       // Which stat to upgrade
        public string label;        // Display label (e.g. "Strength +1")
        public int lpCost;          // LP cost per upgrade
        public int goldCost;        // Gold cost per upgrade
        public int maxLevel;        // Max upgrades purchasable
    }

    /// <summary>
    /// ScriptableObject data asset for a trainer NPC. Defines available stat upgrades.
    /// Assigned to TrainerNPC components in the scene.
    /// Story 3.4: Initial implementation.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/NPC/Trainer", fileName = "NewTrainer")]
    public class TrainerSO : ScriptableObject
    {
        public string trainerName;
        public StatUpgradeEntry[] upgrades;
    }
}
