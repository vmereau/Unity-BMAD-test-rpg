using Game.Player;
using Game.Progression;
using UnityEngine;

namespace Game.Dialogue
{
    public enum TeachingType { SkillBased, StatBased }

    [System.Serializable]
    public class TeachChoiceOption : ChoiceOption
    {
        // Note: no [Header] attributes here — TeachChoiceOptionDrawer controls all rendering.
        // Headers on fields drawn via EditorGUI.PropertyField with a fixed-height rect cause
        // the header label to overflow into the next field's rect.

        [Tooltip("Select SkillBased to teach a skill; StatBased to upgrade a stat.")]
        public TeachingType teachingType;

        [Tooltip("Gold deducted on selection (0 = free).")]
        public int goldCost;

        [Tooltip("If set, this choice calls PlayerSkills.LearnSkill(). Visible only when teachingType = SkillBased.")]
        public SkillSO skill;
        [Tooltip("Stat to upgrade. Visible only when teachingType = StatBased.")]
        public StatType statToUpgrade;
        [Tooltip("Points added to the stat AND LP cost for this training. Visible only when teachingType = StatBased.")]
        [Min(1)] public int statPoints = 1;
        [Tooltip("If player BASE stat (no equipment bonuses) >= this value, deny training (no resources consumed). 0 = no cap. Visible only when teachingType = StatBased.")]
        public int limitStat;

        [Tooltip("Node to advance to when teaching executes successfully.")]
        public DialogueNode confirmNextNode;
        [Tooltip("Node to advance to when player stat is at the limit (no cost consumed). Visible only when teachingType = StatBased.")]
        public DialogueNode denyNextNode;
    }

    [CreateAssetMenu(menuName = "Game/Dialogue/Teach Choice Node", fileName = "TeachChoice_")]
    public class TeachChoiceDialogueNode : DialogueNode
    {
        [Header("Teaching Choices")]
        [Tooltip("Options shown to the player. Each option teaches a stat or skill at a cost.")]
        public TeachChoiceOption[] choices;

        public override bool IsEndNode() => false;
    }
}
