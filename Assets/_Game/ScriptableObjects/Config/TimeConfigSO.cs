using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(fileName = "TimeConfig", menuName = "Game/Config/Time Config")]
    public class TimeConfigSO : ScriptableObject
    {
        [Header("Clock")]
        [Tooltip("Real minutes one game day lasts (48 → 1 game hour = 2 real minutes).")]
        [Min(1f)] public float realMinutesPerGameDay = 48f;
        [Tooltip("Hour of day 1 when a new game starts.")]
        [Range(0, 23)] public int startHour = 8;

        [Header("Sleep")]
        [Tooltip("Hour \"Until morning\" sleeps to.")]
        [Range(0, 23)] public int morningHour = 6;
        [Tooltip("Hour \"Until evening\" sleeps to.")]
        [Range(0, 23)] public int eveningHour = 18;
        [Tooltip("Sleep is refused while a hostile is within this distance (m) of the player.")]
        [Min(0f)] public float sleepHostileRadius = 15f;
        [Tooltip("Duration (real seconds, unscaled) of each half of the sleep fade.")]
        [Min(0f)] public float sleepFadeSeconds = 0.5f;
    }
}
