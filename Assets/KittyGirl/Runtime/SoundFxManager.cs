using System;
using System.Collections.Generic;
using UnityEngine;

namespace ParallaxVisions
{
    public enum SoundFxEvent
    {
        PlayerShot,
        PlayerHurt,
        EnemyDefeated,
        GameStarted,
        GameOver,
        WaveStarted,
        WaveCompleted,
        GameWon,
        PickupCollected,
        SpecialWeaponActivated
    }

    [Serializable]
    public sealed class SoundFxEventConfiguration
    {
        public SoundFxEvent eventType;
        public List<AudioSource> sounds = new List<AudioSource>();
    }

    /// <summary>
    /// Central event-based sound-effect playback, muting, and attract-mode suppression.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoundFxManager : MonoBehaviour
    {
        [SerializeField]
        private List<SoundFxEventConfiguration> eventSounds =
            new List<SoundFxEventConfiguration>();

        private readonly Dictionary<SoundFxEvent, List<AudioSource>> registeredSources =
            new Dictionary<SoundFxEvent, List<AudioSource>>();

        private bool muted;
        private bool attractMode;

        public bool IsMuted => muted;
        public bool IsAttractMode => attractMode;

        public void RegisterSources(SoundFxEvent eventType, IList<AudioSource> sources)
        {
            if (sources == null)
                return;

            List<AudioSource> registered;
            if (!registeredSources.TryGetValue(eventType, out registered))
            {
                registered = new List<AudioSource>();
                registeredSources.Add(eventType, registered);
            }

            for (int i = 0; i < sources.Count; i++)
            {
                AudioSource source = sources[i];
                if (source != null && !registered.Contains(source))
                    registered.Add(source);
            }
        }

        public bool Play(SoundFxEvent eventType)
        {
            if (muted || attractMode)
                return false;

            int sourceCount = CountSources(eventType);
            if (sourceCount == 0)
                return false;

            int selectedIndex = UnityEngine.Random.Range(0, sourceCount);
            for (int i = 0; i < eventSounds.Count; i++)
            {
                SoundFxEventConfiguration configuration = eventSounds[i];
                if (configuration == null || configuration.sounds == null ||
                    configuration.eventType != eventType)
                    continue;
                for (int sourceIndex = 0; sourceIndex < configuration.sounds.Count; sourceIndex++)
                {
                    AudioSource source = configuration.sounds[sourceIndex];
                    if (source == null)
                        continue;
                    if (selectedIndex-- != 0)
                        continue;

                    source.mute = false;
                    source.Play();
                    return true;
                }
            }

            List<AudioSource> registered;
            if (!registeredSources.TryGetValue(eventType, out registered))
                return false;
            for (int i = 0; i < registered.Count; i++)
            {
                AudioSource source = registered[i];
                if (source == null)
                    continue;
                if (selectedIndex-- != 0)
                    continue;

                source.mute = false;
                source.Play();
                return true;
            }
            return false;
        }

        public void SetAttractMode(bool value)
        {
            attractMode = value;
            if (value)
                StopAll();
        }

        public void SetMuted(bool value)
        {
            muted = value;
            if (value)
                StopAll();
        }

        public void StopAll()
        {
            HashSet<AudioSource> stopped = new HashSet<AudioSource>();
            for (int i = 0; i < eventSounds.Count; i++)
                StopSources(eventSounds[i] != null ? eventSounds[i].sounds : null, stopped);
            foreach (KeyValuePair<SoundFxEvent, List<AudioSource>> entry in registeredSources)
                StopSources(entry.Value, stopped);
        }

        private int CountSources(SoundFxEvent eventType)
        {
            int count = 0;
            for (int i = 0; i < eventSounds.Count; i++)
            {
                SoundFxEventConfiguration configuration = eventSounds[i];
                if (configuration == null || configuration.sounds == null ||
                    configuration.eventType != eventType)
                    continue;
                for (int sourceIndex = 0; sourceIndex < configuration.sounds.Count; sourceIndex++)
                {
                    if (configuration.sounds[sourceIndex] != null)
                        count++;
                }
            }
            List<AudioSource> registered;
            if (registeredSources.TryGetValue(eventType, out registered))
            {
                for (int i = 0; i < registered.Count; i++)
                {
                    if (registered[i] != null)
                        count++;
                }
            }
            return count;
        }

        private static void StopSources(IList<AudioSource> sources, HashSet<AudioSource> stopped)
        {
            if (sources == null)
                return;
            for (int i = 0; i < sources.Count; i++)
            {
                AudioSource source = sources[i];
                if (source != null && stopped.Add(source) && source.isPlaying)
                    source.Stop();
            }
        }
    }
}
