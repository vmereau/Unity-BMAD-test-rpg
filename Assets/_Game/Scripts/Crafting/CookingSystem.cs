using Game.Core;
using Game.UI;
using UnityEngine;

namespace Game.Crafting
{
    /// <summary>
    /// Player-side listener for <see cref="CookingStation"/> open requests → opens the <see cref="CookingUI"/>.
    /// Mirrors <c>ContainerSystem</c>. Station gates live here, not on the station: a station's required skill
    /// (e.g. Smithing on the anvil) refuses the open with a toast while the player lacks it.
    /// </summary>
    public class CookingSystem : MonoBehaviour
    {
        private const string TAG = "[CookingSystem]";

        [SerializeField] private GameEventSO_CookingStationOpenRequest _onOpenRequested;
        [SerializeField] private CookingUI _cookingUI;
        [Tooltip("Toast shown when a station's required skill is missing.")]
        [SerializeField] private GameEventSO_String _onCraftingRefused;

        private void OnEnable()
        {
            if (_onOpenRequested == null)
            {
                GameLog.Warn(TAG, "No cooking open event assigned — CookingSystem will not respond");
                return;
            }
            if (_onCraftingRefused == null)
                GameLog.Warn(TAG, "No crafting-refused event assigned — skill-gated stations will refuse silently");
            _onOpenRequested.AddListener(HandleOpenRequested);
        }

        private void OnDisable()
        {
            if (_onOpenRequested == null) return;
            _onOpenRequested.RemoveListener(HandleOpenRequested);
        }

        private void HandleOpenRequested(CookingStationOpenRequestData data)
        {
            if (_cookingUI == null)
            {
                GameLog.Warn(TAG, "CookingUI is not assigned — cannot open cooking station");
                return;
            }
            if (data.requiredSkill != null && !HasSkill(data.requiredSkill))
            {
                string skillName = data.requiredSkill.Skill != null ? data.requiredSkill.Skill.displayName : data.requiredSkill.name;
                GameLog.Info(TAG, $"{data.stationName} refused — {skillName} not learned");
                _onCraftingRefused?.Raise(FormatSkillRequired(skillName));
                return;
            }
            _cookingUI.Open(data.recipes, data.stationName, data.kind);
        }

        /// <summary>"You need the Smithing skill".</summary>
        public static string FormatSkillRequired(string skillName) => $"You need the {skillName} skill";

        private static bool HasSkill(SkillFact fact)
        {
            if (WorldStateManager.Instance == null)
            {
                GameLog.Warn(TAG, "No WorldStateManager (Core not loaded?) — skill-gated station refused");
                return false;
            }
            return WorldStateManager.Instance.PlayerHasSkill(fact);
        }
    }
}
