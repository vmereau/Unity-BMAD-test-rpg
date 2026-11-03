using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// One sound effect: its clip variations and how to play them. Played by <see cref="SfxPlayer"/> through the
    /// <c>OnSfxRequested</c> channel. A cue with no clips is silent (SfxPlayer warns once).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Audio/Sound Cue", fileName = "Cue_")]
    public class SoundCueSO : ScriptableObject
    {
        [Tooltip("Variations — one is picked at random, never the same one twice in a row.")]
        public AudioClip[] clips;

        [Header("Volume / pitch")]
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random volume reduction, 0–1 fraction of volume (0.1 = up to 10 % quieter).")]
        [Range(0f, 1f)] public float volumeJitter = 0.1f;
        [Range(0.1f, 3f)] public float pitchMin = 0.95f;
        [Range(0.1f, 3f)] public float pitchMax = 1.05f;

        [Header("Spatial")]
        [Tooltip("0 = 2D (UI-like), 1 = fully 3D at the requested position.")]
        [Range(0f, 1f)] public float spatialBlend = 1f;
        [Min(0.01f)] public float minDistance = 1f;
        [Min(0.01f)] public float maxDistance = 25f;

        [Header("Limits")]
        [Tooltip("Seconds during which a new request for this cue is dropped (stacked hits, double events).")]
        [Min(0f)] public float cooldown = 0.05f;
    }
}
