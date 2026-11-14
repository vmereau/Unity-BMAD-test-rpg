using UnityEngine;

namespace Game.NPC
{
    /// <summary>
    /// A named skin tone (spec-npc-skin-tones). Referenced from <see cref="NPCEntity"/>; applied at spawn by
    /// <c>Game.AI.NpcSkinTone</c> to every <c>SG_SkinLit</c> material of the NPC model (body, head, armor hands).
    /// The shader maps masked skin of average luminance onto <see cref="Tone"/> and keeps the texture's light/dark detail.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/NPC/Skin Tone", fileName = "Tone_")]
    public class SkinToneSO : ScriptableObject
    {
        [Tooltip("Mid skin colour (sRGB) that average skin becomes.")]
        [SerializeField] private Color _tone = Color.white;

        [Tooltip("Blend between the texture's own skin (0) and the tone (1).")]
        [SerializeField, Range(0f, 1f)] private float _strength = 1f;

        public Color Tone => _tone;
        public float Strength => _strength;

#if UNITY_EDITOR
        /// <summary>Editor-only setter (tests, asset setup).</summary>
        public void EditorSet(Color tone, float strength)
        {
            _tone = tone;
            _strength = strength;
        }
#endif
    }
}
