using UnityEngine;
using VacuumShaders.CurvedWorld;

namespace ParallaxVisions
{
    /// <summary>
    /// Owns music playback and exposes the current source to music-reactive visuals.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicController : MonoBehaviour
    {
        [SerializeField]
        private AudioSource menuMusic;

        [SerializeField]
        private AudioSource gameplayMusic;

        private MusicSpectrumReactor spectrumReactor;
        private bool muted;

        public AudioSource ActiveSource
        {
            get
            {
                if (gameplayMusic != null && gameplayMusic.isPlaying)
                    return gameplayMusic;
                return menuMusic != null && menuMusic.isPlaying ? menuMusic : null;
            }
        }

        public bool IsMuted => muted;

        public void Configure(KaleidoscopeEffect backgroundEffect)
        {
            spectrumReactor = GetComponent<MusicSpectrumReactor>();
            if (spectrumReactor == null)
                spectrumReactor = gameObject.AddComponent<MusicSpectrumReactor>();
            spectrumReactor.Configure(this, backgroundEffect);
        }

        public void PlayMenu()
        {
            Stop(gameplayMusic);
            Play(menuMusic);
        }

        public void PlayGameplay()
        {
            Stop(menuMusic);
            Play(gameplayMusic);
        }

        public void PlayAttractMode()
        {
            // The unattended demo intentionally retains the title music.
            PlayMenu();
        }

        public void StopAll()
        {
            Stop(menuMusic);
            Stop(gameplayMusic);
        }

        public void SetMuted(bool value)
        {
            muted = value;
            if (menuMusic != null)
                menuMusic.mute = value;
            if (gameplayMusic != null)
                gameplayMusic.mute = value;
        }

        private void Play(AudioSource source)
        {
            if (source == null)
                return;
            source.mute = muted;
            if (!source.isPlaying)
                source.Play();
        }

        private static void Stop(AudioSource source)
        {
            if (source != null && source.isPlaying)
                source.Stop();
        }
    }
}
