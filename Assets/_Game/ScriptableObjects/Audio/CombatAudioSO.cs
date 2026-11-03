using Game.Combat;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Shared combat sound cues (<c>Data/Audio/CombatAudio.asset</c>), read by <c>PlayerCombat</c>,
    /// <c>EntityMeleeAttacker</c> and <c>PlayerHealth</c>. Weapon swings come from <c>WeaponArchetypeSO.swingSound</c>.
    /// Any cue may be left empty (no sound). <see cref="ForHit"/> maps a melee <see cref="HitResult"/> to its cue.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Audio/Combat Audio", fileName = "CombatAudio")]
    public class CombatAudioSO : ScriptableObject
    {
        [Header("Swings")]
        public SoundCueSO unarmedSwing;

        [Header("Hits")]
        public SoundCueSO hitFlesh;
        public SoundCueSO block;
        public SoundCueSO perfectBlock;

        [Header("Weapon")]
        public SoundCueSO draw;
        public SoundCueSO sheathe;

        [Header("Player")]
        public SoundCueSO playerHurt;

        /// <summary>Cue for a melee hit outcome; null (silent) for a dodge.</summary>
        public SoundCueSO ForHit(HitResult result)
        {
            switch (result)
            {
                case HitResult.NotBlocked: return hitFlesh;
                case HitResult.Blocked: return block;
                case HitResult.PerfectBlock: return perfectBlock;
                default: return null;
            }
        }
    }
}
