using Game.AI;
using Game.Core;
using Game.Player;
using Game.UI;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Player-side listener for <see cref="Bed"/> sleep requests (on Player.prefab, next to <c>CookingSystem</c>).
    /// Refuses while dead or with a hostile within <see cref="TimeConfigSO.sleepHostileRadius"/> (toast), otherwise
    /// opens the <see cref="SleepUI"/>. A chosen sleep is gated again, then — at the black of the fade — the
    /// <see cref="GameClock"/> skips to the wake time and the player is fully healed.
    /// <see cref="_gameClock"/> lives in Core.unity: it is a scene override on the Player instance there.
    /// </summary>
    public class SleepSystem : MonoBehaviour
    {
        private const string TAG = "[SleepSystem]";

        [SerializeField] private TimeConfigSO _config;
        [SerializeField] private GameClock _gameClock;
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private FactionMember _factionMember;
        [SerializeField] private SleepUI _sleepUI;

        [Header("Event Channels")]
        [SerializeField] private GameEventSO_Void _onSleepRequested;
        [SerializeField] private GameEventSO_String _onSleepRefused;

        private void Awake()
        {
            if (_config == null) GameLog.Warn(TAG, "TimeConfigSO not assigned — beds will refuse to work");
            if (_gameClock == null) GameLog.Warn(TAG, "GameClock not assigned — beds will refuse to work");
            if (_playerHealth == null) GameLog.Warn(TAG, "PlayerHealth not assigned — sleep won't heal");
            if (_factionMember == null) GameLog.Warn(TAG, "FactionMember not assigned — the nearby-hostile check is skipped");
            if (_sleepUI == null) GameLog.Warn(TAG, "SleepUI not assigned — beds will refuse to work");
            if (_onSleepRefused == null) GameLog.Warn(TAG, "_onSleepRefused not assigned — refusals will only be logged");
        }

        private void OnEnable()
        {
            if (_onSleepRequested == null)
            {
                GameLog.Warn(TAG, "No sleep request event assigned — SleepSystem will not respond");
                return;
            }
            _onSleepRequested.AddListener(HandleSleepRequested);
        }

        private void OnDisable()
        {
            if (_onSleepRequested == null) return;
            _onSleepRequested.RemoveListener(HandleSleepRequested);
        }

        private void HandleSleepRequested(bool _)
        {
            if (_config == null || _gameClock == null || _sleepUI == null)
            {
                GameLog.Warn(TAG, "Sleep requested but SleepSystem is not fully wired — ignored");
                return;
            }
            if (_sleepUI.IsOpen || !CanSleep()) return;
            _sleepUI.Open(GameTime.Format(_gameClock.Now), HandleChoice);
        }

        // Re-gated: an enemy may have walked up while the panel was open.
        private void HandleChoice(SleepChoice choice)
        {
            if (_gameClock == null || _config == null || !CanSleep())
            {
                if (_sleepUI != null) _sleepUI.Close();
                return;
            }
            if (_sleepUI != null) _sleepUI.PlaySleep(() => ApplySleep(choice));
        }

        private void ApplySleep(SleepChoice choice)
        {
            if (_gameClock == null || _config == null) return;
            double wake = SleepRules.WakeTime(choice, _gameClock.Now, _config.morningHour, _config.eveningHour);
            if (!_gameClock.SkipTo(wake)) return; // no rest without the time passing
            if (_playerHealth != null && !_playerHealth.IsDead)
                _playerHealth.Heal(_playerHealth.MaxHealth);
            GameLog.Info(TAG, $"Slept ({choice}) → {GameTime.Format(_gameClock.Now)}");
        }

        /// <summary>False (and raises the refusal toast) while dead or with a live hostile nearby.</summary>
        private bool CanSleep()
        {
            if (_playerHealth != null && _playerHealth.IsDead)
            {
                Refuse(SleepRules.MSG_DEAD);
                return false;
            }
            if (_factionMember != null
                && TargetRegistry.FindClosestHostile(_factionMember.Faction, transform.position, _config.sleepHostileRadius) != null)
            {
                Refuse(SleepRules.MSG_HOSTILES_NEARBY);
                return false;
            }
            return true;
        }

        private void Refuse(string message)
        {
            GameLog.Info(TAG, $"Sleep refused: {message}");
            _onSleepRefused?.Raise(message);
        }
    }
}
