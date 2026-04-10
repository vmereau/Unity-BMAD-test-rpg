using Game.Player;
using Game.Progression;
using UnityEngine;

namespace Game.Dialogue
{
    [System.Serializable]
    public class TeachChoiceOption : ChoiceOption
    {
        [Header("Costs (0 = free)")]
        [Tooltip("Gold deducted on selection.")]
        public int goldCost;
        [Tooltip("LP cost for stat-upgrade choices. For skill choices this is ignored — LP cost is read from SkillSO.lpCost.")]
        public int lpCost;

        [Header("Effect — Skill OR Stat (mutually exclusive)")]
        [Tooltip("If set, this choice calls PlayerSkills.LearnSkill(). Stat fields below are ignored.")]
        public SkillSO skill;
        [Tooltip("Stat to upgrade. Used only when skill is null.")]
        public StatType statToUpgrade;
        [Tooltip("Points added to the stat. Used only when skill is null. Defense has no base value — authoring a Defense upgrade logs a warning and does nothing.")]
        [Min(1)] public int statPoints = 1;
    }

    [CreateAssetMenu(menuName = "Game/Dialogue/Teach Choice Node", fileName = "TeachChoice_")]
    public class TeachChoiceDialogueNode : DialogueNode
    {
        // nextNode (inherited from DialogueNode) is the destination of the always-present
        // "Exit" button rendered after the teaching choices. Set to a confirmation/farewell
        // TextDialogueNode, or leave null to return the player to the topic list.

        [Header("Teaching Choices")]
        [Tooltip("Options shown to the player. Each option teaches a stat or skill at a cost.")]
        public TeachChoiceOption[] choices;

        public override bool IsEndNode() => false;
    }
}
