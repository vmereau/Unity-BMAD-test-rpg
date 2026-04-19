using Game.Core;
using UnityEngine;

namespace Game.Progression
{
    public class PlayerRewards : MonoBehaviour
    {
        private const string TAG = "[PlayerRewards]";

        [SerializeField] private GameEventSO_KilledFact _onEntityKilled;
        [SerializeField] private XPSystem _xpSystem;

        private void Awake()
        {
            if (_xpSystem == null)
            {
                GameLog.Error(TAG, "XPSystem not assigned — PlayerRewards disabled.");
                enabled = false;
                return;
            }
            if (_onEntityKilled == null)
                GameLog.Warn(TAG, "OnEntityKilled event not assigned — player will earn no XP from kills.");
        }

        private void OnEnable()
        {
            if (_onEntityKilled != null)
                _onEntityKilled.AddListener(HandleEntityKilled);
        }

        private void OnDisable()
        {
            // Guard: _onEntityKilled may be null (unassigned in Inspector), or Awake disabled
            // this component before OnEnable ran — RemoveListener is a safe no-op either way,
            // but skip the call entirely when unassigned to avoid confusing future readers.
            if (_onEntityKilled == null) return;
            _onEntityKilled.RemoveListener(HandleEntityKilled);
        }

        private void HandleEntityKilled(KilledFact fact)
        {
            int xp = fact?.EnemyType?.XpOnKill ?? 0;
            if (xp > 0)
                _xpSystem.GiveExperience(xp);
        }
    }
}
