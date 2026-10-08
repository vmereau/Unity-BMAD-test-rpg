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

        public float WitnessRange => _witnessRange;
        public float WatchRange => ResolveWatchRange(_witnessRange, _watchRange);
        public BarkSetSO Barks => _barks;

        /// <summary>Watch range, never below the witness range (otherwise a witness would stop watching at once).</summary>
        public static float ResolveWatchRange(float witnessRange, float watchRange) =>
            Mathf.Max(Mathf.Max(0f, witnessRange), watchRange);

        /// <summary>True when <paramref name="profile"/> is assigned and has a positive range.</summary>
        public static bool IsEnabled(WitnessProfileSO profile) => profile != null && profile.WitnessRange > 0f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            _witnessRange = Mathf.Max(0f, _witnessRange);
            if (_watchRange < _witnessRange)
            {
                GameLog.Warn(TAG, $"'{name}': _watchRange ({_watchRange}) must be >= _witnessRange ({_witnessRange}) — clamped.");
                _watchRange = _witnessRange;
            }
        }
#endif
    }
}
