using UnityEngine;

namespace Game.World
{
    [CreateAssetMenu(fileName = "InteractionConfig", menuName = "Game/Config/Interaction Config")]
    public class InteractionConfigSO : ScriptableObject
    {
        [Header("Detection")]
        public float interactionRange = 3f;
        public float nameRange = 8f;
        public float scanRadius = 0.5f;
        public float scanInterval = 0.2f;

        [Header("Highlight")]
        [Tooltip("Default outline color of the focused interactable (InteractionHighlight can override it).")]
        public Color outlineColor = new Color(1f, 0.85f, 0.45f, 1f);
        [Tooltip("Rendering layer drawn by InteractionOutlineFeature. Must match the feature's outlineLayer.")]
        public RenderingLayerMask outlineRenderingLayer;
    }
}
