using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>
    /// Plays every one-shot sound effect from a pool of child <see cref="AudioSource"/>s (GO <c>AudioManager</c> in
    /// Core.unity). Gameplay never owns sources: it raises <c>OnSfxRequested</c> with a <see cref="SoundCueSO"/>.
    /// When every source is busy, the one that started earliest is reused.
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        private const string TAG = "[SfxPlayer]";

        [SerializeField] private GameEventSO_SfxRequest _onSfxRequested;
        [SerializeField] private AudioMixerGroup _sfxGroup;
        [SerializeField, Range(1, 64)] private int _poolSize = 24;

        private AudioSource[] _sources;
        private float[] _startTimes;
        private readonly Dictionary<SoundCueSO, CueState> _cueStates = new();
        private readonly HashSet<SoundCueSO> _warnedEmpty = new();
        private AudioListener _listener;

        private struct CueState
        {
            public int LastIndex;
            public float LastPlay;
        }

        private void Awake()
        {
            if (_sfxGroup == null) GameLog.Warn(TAG, "No SFX mixer group — effects bypass the SFX volume");
            _sources = new AudioSource[_poolSize];
            _startTimes = new float[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"Sfx_{i:00}");
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0f;
                source.outputAudioMixerGroup = _sfxGroup;
                _sources[i] = source;
                _startTimes[i] = float.NegativeInfinity;
            }
        }

        private void OnEnable()
        {
            if (_onSfxRequested == null)
            {
                GameLog.Warn(TAG, "OnSfxRequested channel not assigned — no sound effects will play");
                return;
            }
            _onSfxRequested.AddListener(HandleSfxRequested);
        }

        private void OnDisable()
        {
            if (_onSfxRequested != null) _onSfxRequested.RemoveListener(HandleSfxRequested);
        }

        private void HandleSfxRequested(SfxRequest request)
        {
            SoundCueSO cue = request.cue;
            if (cue == null || _sources == null) return;

            int count = cue.clips != null ? cue.clips.Length : 0;
            if (count == 0)
            {
                if (_warnedEmpty.Add(cue)) GameLog.Warn(TAG, $"Sound cue '{cue.name}' has no clips — silent");
                return;
            }

            float now = Time.unscaledTime;
            CueState state = _cueStates.TryGetValue(cue, out CueState s) ? s : new CueState { LastIndex = -1, LastPlay = -1f };
            if (SoundCuePicker.IsOnCooldown(now, state.LastPlay, cue.cooldown)) return;

            if (IsOutOfListenerRange(cue, request.position)) return;

            int index = SoundCuePicker.PickIndex(count, state.LastIndex, Random.value);
            index = SoundCuePicker.FirstNonNull(cue.clips, index);
            if (index < 0)
            {
                if (_warnedEmpty.Add(cue)) GameLog.Warn(TAG, $"Sound cue '{cue.name}' has only empty clip slots — silent");
                return;
            }
            AudioClip clip = cue.clips[index];

            int slot = FindSource();
            AudioSource source = _sources[slot];
            source.Stop();
            source.transform.position = request.position;
            source.clip = clip;
            source.volume = SoundCuePicker.JitterVolume(cue.volume, cue.volumeJitter, Random.value);
            source.pitch = SoundCuePicker.PickPitch(cue.pitchMin, cue.pitchMax, Random.value);
            source.spatialBlend = cue.spatialBlend;
            source.minDistance = cue.minDistance;
            source.maxDistance = Mathf.Max(cue.minDistance, cue.maxDistance);
            source.Play();
            _startTimes[slot] = now;

            state.LastIndex = index;
            state.LastPlay = now;
            _cueStates[cue] = state;
        }

        // Fully 3D cues beyond maxDistance are inaudible (linear rolloff): don't spend a voice or the cooldown on them.
        private bool IsOutOfListenerRange(SoundCueSO cue, Vector3 position)
        {
            if (cue.spatialBlend < 1f) return false;
            if (_listener == null || !_listener.isActiveAndEnabled)
                _listener = FindAnyObjectByType<AudioListener>(); // re-resolved only when lost (scene / player reload)
            if (_listener == null) return false;
            float max = Mathf.Max(cue.minDistance, cue.maxDistance);
            return (position - _listener.transform.position).sqrMagnitude > max * max;
        }

        // A free source, else the one that started earliest.
        private int FindSource()
        {
            int oldest = 0;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].isPlaying) return i;
                if (_startTimes[i] < _startTimes[oldest]) oldest = i;
            }
            return oldest;
        }
    }
}
