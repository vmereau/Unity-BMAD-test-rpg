using UnityEngine;

namespace Game.Core
{
    /// <summary>Request to show a speech bubble above a speaker (consumed by <c>SpeechBubbleUI</c>).</summary>
    [System.Serializable]
    public struct SpeechBubbleRequest
    {
        /// <summary>
        /// Runtime scene ref passed through Raise() — never stored in an SO asset. The bubble's anchor point (pass a
        /// dedicated anchor transform such as an entity's <c>SpeechAnchor</c>) and the identity for "one bubble per
        /// speaker".
        /// </summary>
        public Transform speaker;
        /// <summary>Line to show. Null / empty → the request is ignored with a warning.</summary>
        public string text;
        /// <summary>Seconds the bubble is fully visible (excluding fades). ≤ 0 → the UI default.</summary>
        public float duration;
        /// <summary>Higher wins: a lower-priority request never replaces a visible bubble of the same speaker.</summary>
        public int priority;
        /// <summary>Extra metres above <c>speaker.position</c> (default 0).</summary>
        public float anchorHeight;
    }

    [CreateAssetMenu(menuName = "Game/Events/Speech Bubble Request", fileName = "NewSpeechBubbleRequestEvent")]
    public class GameEventSO_SpeechBubbleRequest : GameEventSO<SpeechBubbleRequest> { }
}
