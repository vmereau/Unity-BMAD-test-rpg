using UnityEngine;

namespace Game.World
{
    [CreateAssetMenu(fileName = "InteractionConfig", menuName = "Game/Config/Interaction Config")]
    public class InteractionConfigSO : ScriptableObject
    {
        [Header("Detection")]
        public float interactionRange = 3f;
    }
}
