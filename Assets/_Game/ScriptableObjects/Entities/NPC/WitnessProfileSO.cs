using Game.Core;
using Game.Dialogue;
using UnityEngine;

namespace Game.NPC
{
    /// <summary>
    /// Shared witness behaviour for non-hostile NPCs: how far they notice a <b>sneaking</b> player, how long they keep
    /// watching, and what they say. Referenced by <see cref="NPCEntity"/> assets — one profile (e.g.
    /// <c>WitnessProfile_Humanoid</c>) gives every humanoid the same base behaviour; tune one NPC by pointing it to
    /// another profile. Only used against stealth targets whose faction is not hostile to the NPC.
    /// Also covers <b>thefts</b>: the range at which the NPC notices the player stealing (standing or sneaking), the
    /// alert shouted when it starts chasing and the scold line said when it catches the thief.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/NPC/Witness Profile", fileName = "WitnessProfile_")]
    public class WitnessProfileSO : ScriptableObject
    {
        private const string TAG = "[NPC]";

        [Tooltip("Sight range (m) against a SNEAKING non-hostile player. 0 = never reacts.")]
        [SerializeField] private float _witnessRange = 6f;
        [Tooltip("A watching witness keeps facing the player while closer than this (m). Never below WitnessRange.")]
        [SerializeField] private float _watchRange = 9f;
        [Tooltip("Lines said in a speech bubble when the player is spotted sneaking. Null = watches silently.")]
        [SerializeField] private BarkSetSO _barks;

        [Header("Theft")]
        [Tooltip("Range (m) at which the NPC notices a theft by a standing player; shortened by StealthConfigSO.sneakSightRangeMultiplier while sneaking. 0 = never notices thefts.")]
        [SerializeField] private float _theftSightRange = 10f;
        [Tooltip("Shouted in a speech bubble when the NPC starts chasing a thief. Null = chases silently.")]
        [SerializeField] private BarkSetSO _theftAlertBarks;
        [Tooltip("Forced-dialogue line said when the NPC catches a thief (any theft).")]
        [SerializeField] private BarkSetSO _theftScoldBarks;
        [Tooltip("Forced-dialogue line for item pickups. Null = falls back to _theftScoldBarks.")]
        [SerializeField] private BarkSetSO _itemTheftScoldBarks;

        public float WitnessRange => _witnessRange;
        public float WatchRange => ResolveWatchRange(_witnessRange, _watchRange);
        public BarkSetSO Barks => _barks;
        public float TheftSightRange => _theftSightRange;
        public BarkSetSO TheftAlertBarks => _theftAlertBarks;
        public BarkSetSO TheftScoldBarks => _theftScoldBarks;
        public BarkSetSO ItemTheftScoldBarks => _itemTheftScoldBarks;

        /// <summary>Watch range, never below the witness range (otherwise a witness would stop watching at once).</summary>
        public static float ResolveWatchRange(float witnessRange, float watchRange) =>
            Mathf.Max(Mathf.Max(0f, witnessRange), watchRange);

        /// <summary>True when <paramref name="profile"/> is assigned and has a positive range.</summary>
        public static bool IsEnabled(WitnessProfileSO profile) => profile != null && profile.WitnessRange > 0f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            _witnessRange = Mathf.Max(0f, _witnessRange);
            _theftSightRange = Mathf.Max(0f, _theftSightRange);
            if (_watchRange < _witnessRange)
            {
                GameLog.Warn(TAG, $"'{name}': _watchRange ({_watchRange}) must be >= _witnessRange ({_witnessRange}) — clamped.");
                _watchRange = _witnessRange;
            }
        }
#endif
    }
}
