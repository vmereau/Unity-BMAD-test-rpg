using Game.Core;
using Game.Progression;
using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "Items/Skill Item", fileName = "Item_")]
    public class SkillItemSO : UsableItemSO
    {
        private const string TAG = "[SkillItemSO]";

        [SerializeField] private SkillSO _skill;

        public override bool OnUse(GameObject user)
        {
            if (_skill == null)
            {
                GameLog.Warn(TAG, "OnUse: _skill not assigned.");
                return false;
            }
            var playerSkills = user.GetComponent<PlayerSkills>();
            if (playerSkills == null)
            {
                GameLog.Warn(TAG, $"OnUse: no PlayerSkills on {user.name}");
                return false;
            }
            return playerSkills.LearnSkill(_skill);
        }
    }
}
