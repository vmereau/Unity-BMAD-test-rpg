using Game.Audio;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Request to play a one-shot sound effect at a world position (ignored by 2D cues).</summary>
    [System.Serializable]
    public struct SfxRequest
    {
        public SoundCueSO cue;
        public Vector3 position;

        public SfxRequest(SoundCueSO cue, Vector3 position)
        {
            this.cue = cue;
            this.position = position;
        }
    }

    [CreateAssetMenu(menuName = "Game/Events/Sfx Request", fileName = "NewSfxRequestEvent")]
    public class GameEventSO_SfxRequest : GameEventSO<SfxRequest>
    {
        /// <summary>Raises a request when <paramref name="cue"/> is set — null cues (optional sounds) are skipped.</summary>
        public void Play(SoundCueSO cue, Vector3 position)
        {
            if (cue == null) return;
            Raise(new SfxRequest(cue, position));
        }
    }
}
