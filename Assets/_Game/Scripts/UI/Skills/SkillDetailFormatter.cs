using System;
using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Progression;

namespace Game.UI
{
    /// <summary>
    /// Pure display logic for <see cref="SkillDetailPanelUI"/>: status line, text sections and requirement rows.
    /// Player state comes in as delegates so it is covered by EditMode tests without MonoBehaviours.
    /// </summary>
    public static class SkillDetailFormatter
    {
        private const string TAG = "[SkillDetailFormatter]";

        public const string STATUS_LEARNED = "Learned";
        public const string STATUS_NOT_LEARNED = "Not learned";

        public const string LABEL_LP_COST = "LP Cost";
        public const string LABEL_REQUIRES = "Requires";
        public const string LABEL_STRENGTH = "Strength";
        public const string LABEL_DEXTERITY = "Dexterity";
        public const string LABEL_ENDURANCE = "Endurance";
        public const string LABEL_INTELLIGENCE = "Intelligence";
        public const string LABEL_DEFENSE = "Defense";

        public static string GetStatus(bool learned) => learned ? STATUS_LEARNED : STATUS_NOT_LEARNED;

        /// <summary>The skill's trimmed description, or null when the skill is null or the text is blank.</summary>
        public static string GetDescription(SkillSO skill) => skill == null ? null : TrimOrNull(skill.description);

        /// <summary>The skill's trimmed effect text, or null when the skill is null or the text is blank.</summary>
        public static string GetEffect(SkillSO skill) => skill == null ? null : TrimOrNull(skill.effectDescription);

        public static string GetStatLabel(StatType stat) => stat switch
        {
            StatType.Strength => LABEL_STRENGTH,
            StatType.Dexterity => LABEL_DEXTERITY,
            StatType.Endurance => LABEL_ENDURANCE,
            StatType.Intelligence => LABEL_INTELLIGENCE,
            StatType.Defense => LABEL_DEFENSE,
            _ => stat.ToString()
        };

        /// <summary>
        /// Clears <paramref name="into"/> and fills it with LP cost, stat requirements and prerequisite skills, in that order.
        /// Requirement rows are Positive / Negative against the player's current state, and Neutral once the skill is
        /// learned (they are history) or when the matching delegate is null.
        /// </summary>
        public static void BuildDetailLines(SkillSO skill, bool learned, Func<StatType, int> getStat,
                                            Func<string, bool> hasSkill, List<ItemStatLine> into)
        {
            if (into == null) { GameLog.Warn(TAG, "BuildDetailLines: target list is null"); return; }
            into.Clear();
            if (skill == null) return;

            into.Add(new ItemStatLine(LABEL_LP_COST, skill.lpCost.ToString()));

            foreach (StatRequirement req in skill.statsRequirements)
            {
                StatPolarity polarity = learned || getStat == null
                    ? StatPolarity.Neutral
                    : getStat(req.statType) >= req.value ? StatPolarity.Positive : StatPolarity.Negative;
                into.Add(new ItemStatLine(GetStatLabel(req.statType), req.value.ToString(), polarity));
            }

            foreach (SkillSO prereq in skill.skillRequirements)
            {
                if (prereq == null) continue;
                StatPolarity polarity = learned || hasSkill == null
                    ? StatPolarity.Neutral
                    : hasSkill(prereq.skillId) ? StatPolarity.Positive : StatPolarity.Negative;
                into.Add(new ItemStatLine(LABEL_REQUIRES, prereq.displayName, polarity));
            }
        }

        private static string TrimOrNull(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
