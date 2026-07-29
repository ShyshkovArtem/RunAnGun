using System.Collections;
using RunGun.Settings;
using UnityEngine;

namespace RunGun.Levels
{
    [DisallowMultipleComponent]
    [AddComponentMenu("RunGun/Levels/Level Music Controller")]
    public sealed class LevelMusicController : MonoBehaviour
    {
        [SerializeField] private AudioClip musicClip;
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0f, 1f)] private float volume = 0.45f;
        [SerializeField, Range(0.1f, 3f)] private float pitch = 1f;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool persistBetweenScenes;
        [SerializeField] private float fadeInDuration = 1.5f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        private Coroutine _fadeRoutine;

        private void Awake()
        {
            EnsureAudioSource();

            if (persistBetweenScenes)
                DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (playOnStart)
                Play();
        }

        private void OnDisable()
        {
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }
        }

        public void Play()
        {
            if (musicClip == null)
                return;

            EnsureAudioSource();
            audioSource.clip = musicClip;
            audioSource.loop = loop;
            audioSource.pitch = pitch;

            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);

            float targetVolume = volume * GameSettings.MusicVolume;
            audioSource.volume = fadeInDuration > 0f ? 0f : targetVolume;
            if (!audioSource.isPlaying)
                audioSource.Play();

            if (fadeInDuration > 0f)
                _fadeRoutine = StartCoroutine(FadeVolume(targetVolume, fadeInDuration, stopAfterFade: false));
        }

        public void Stop()
        {
            if (audioSource == null || !audioSource.isPlaying)
                return;

            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);

            if (fadeOutDuration > 0f)
                _fadeRoutine = StartCoroutine(FadeVolume(0f, fadeOutDuration, stopAfterFade: true));
            else
                audioSource.Stop();
        }

        public void SetVolume(float newVolume)
        {
            volume = Mathf.Clamp01(newVolume);
            if (audioSource != null)
                audioSource.volume = volume * GameSettings.MusicVolume;
        }

        public void ApplyUserVolume()
        {
            if (audioSource != null)
                audioSource.volume = volume * GameSettings.MusicVolume;
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.loop = loop;
            audioSource.pitch = pitch;
        }

        private IEnumerator FadeVolume(float targetVolume, float duration, bool stopAfterFade)
        {
            float startVolume = audioSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                audioSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
                yield return null;
            }

            audioSource.volume = targetVolume;
            if (stopAfterFade)
                audioSource.Stop();

            _fadeRoutine = null;
        }
    }
}
