using Game.Core;
using Game.Progression;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// World item implementing IInteractable. Player looks at it with crosshair and presses E to learn a skill using LP.
    /// Consumes the tome (deactivates GO) on successful skill acquisition.
    /// Cross-system ref to PlayerSkills is an intentional prototype pragmatism (see Dev Notes, Story 3.5).
    /// Story 3.5: Initial implementation. Story 4.6: Refactored to IInteractable pattern.
    /// </summary>
    public class TomePickup : MonoBehaviour, IInteractable
    {
        private const string TAG = "[World]";

        [SerializeField] private SkillSO _skill;
        [SerializeField] private PlayerSkills _playerSkills;

        private PersistentID _persistentID;

        public string InteractPrompt => $"Press E to read: {_skill?.displayName ?? "Tome"}";

        private void Awake()
        {
            if (_skill == null)
            {
                GameLog.Error(TAG, "TomePickup: _skill not assigned — component disabled.");
                enabled = false;
                return;
            }
            if (_playerSkills == null)
            {
                GameLog.Error(TAG, "TomePickup: _playerSkills not assigned — component disabled.");
                enabled = false;
                return;
            }
            _persistentID = GetComponent<PersistentID>();
            if (_persistentID == null)
                GameLog.Warn(TAG, $"TomePickup on {gameObject.name}: no PersistentID — tome won't be tracked by WorldStateManager.");
        }

        public void Interact()
        {
            if (_playerSkills == null || _skill == null) return;
            bool learned = _playerSkills.LearnSkill(_skill);
            if (learned)
            {
                GameLog.Info(TAG, $"Tome consumed: {_skill.displayName}");
                _persistentID?.RegisterDeath();
                gameObject.SetActive(false);
            }
        }
    }
}
