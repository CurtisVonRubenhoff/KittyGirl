using System.Collections.Generic;
using UnityEngine;
using VacuumShaders.CurvedWorld;

namespace ParallaxVisions
{
    /// <summary>
    /// Samples the active music source once per frame and distributes separate
    /// frequency bands to scenery, the particle kaleidoscope, and KalCamera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicSpectrumReactor : MonoBehaviour
    {
        private const int SpectrumSize = 512;
        private const float EnvironmentRefreshInterval = 2f;
        private const float AttackResponse = 14f;
        private const float ReleaseResponse = 1.35f;
        private const float ScaleResponseMultiplier = 1.75f;
        private const float EnvironmentScaleResponseMultiplier = 3.25f;
        private const float ParticleRadiusResponseMultiplier = 0.65f;

        private enum Band
        {
            Bass,
            LowMid,
            HighMid,
            Treble,
            Count
        }

        private sealed class EnvironmentTarget
        {
            public Transform transform;
            public Vector3 baseScale;
            public Band band;
            public float amount;
        }

        private sealed class ParticleKaleidoscopeTarget
        {
            public Transform transform;
            public Vector3 baseScale;
            public Band band;
            public float amount;
        }

        private sealed class ParticleRadiusTarget
        {
            public Transform transform;
            public Vector3 basePosition;
            public Band band;
        }

        private readonly float[] spectrum = new float[SpectrumSize];
        private readonly float[] bands = new float[(int)Band.Count];
        private readonly List<EnvironmentTarget> environmentTargets = new List<EnvironmentTarget>();
        private readonly List<ParticleKaleidoscopeTarget> particleTargets = new List<ParticleKaleidoscopeTarget>();
        private readonly List<ParticleRadiusTarget> particleRadiusTargets = new List<ParticleRadiusTarget>();

        private MusicController musicController;
        private KaleidoscopeEffect kalCameraEffect;
        private float refreshTimer;

        // Deltas applied on the previous frame are removed before reading the
        // effect again. This lets GameManager's wave themes remain authoritative.
        private float previousKalAngleDelta;
        private float previousKalScaleDelta;
        private float previousKalRadiusDelta;

        public bool HasActiveMusic => musicController != null && musicController.ActiveSource != null;

        public float PowerUpPulse
        {
            get
            {
                float rhythm = bands[(int)Band.Bass] * 0.65f + bands[(int)Band.LowMid] * 0.3f;
                float detail = bands[(int)Band.HighMid] * 0.12f + bands[(int)Band.Treble] * 0.05f;
                return Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(rhythm), 0.85f) + detail);
            }
        }

        public void Configure(MusicController controller, KaleidoscopeEffect backgroundEffect)
        {
            musicController = controller;
            kalCameraEffect = backgroundEffect;
            RefreshTargets();
        }

        private void Start()
        {
            RefreshTargets();
        }

        private void Update()
        {
            AnalyzeMusic();

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
                RefreshTargets();
        }

        private void LateUpdate()
        {
            PulseEnvironment();
            PulseParticleKaleidoscopes();
            PulseParticleRadii();
            ModulateKalCamera();
        }

        private void AnalyzeMusic()
        {
            AudioSource source = musicController != null ? musicController.ActiveSource : null;

            if (source == null)
            {
                SmoothBand(Band.Bass, 0f);
                SmoothBand(Band.LowMid, 0f);
                SmoothBand(Band.HighMid, 0f);
                SmoothBand(Band.Treble, 0f);
                return;
            }

            source.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            int sampleRate = AudioSettings.outputSampleRate;
            SmoothBand(Band.Bass, MeasureBand(35f, 140f, sampleRate, 115f));
            SmoothBand(Band.LowMid, MeasureBand(140f, 500f, sampleRate, 145f));
            SmoothBand(Band.HighMid, MeasureBand(500f, 2000f, sampleRate, 195f));
            SmoothBand(Band.Treble, MeasureBand(2000f, 8000f, sampleRate, 265f));
        }

        private float MeasureBand(float minimumHz, float maximumHz, int sampleRate, float gain)
        {
            if (sampleRate <= 0)
                return 0f;

            float binWidth = sampleRate * 0.5f / SpectrumSize;
            int first = Mathf.Clamp(Mathf.FloorToInt(minimumHz / binWidth), 0, SpectrumSize - 1);
            int last = Mathf.Clamp(Mathf.CeilToInt(maximumHz / binWidth), first + 1, SpectrumSize);
            float sum = 0f;

            for (int i = first; i < last; i++)
                sum += spectrum[i] * spectrum[i];

            return Mathf.Clamp01(Mathf.Sqrt(sum / (last - first)) * gain);
        }

        private void SmoothBand(Band band, float target)
        {
            int index = (int)band;

            // Some audio backends can briefly return non-finite spectrum samples
            // while a source or output device is changing. Never let one bad frame
            // permanently poison the smoothed bands and every transform they drive.
            if (!IsFinite(target))
                target = 0f;
            if (!IsFinite(bands[index]))
                bands[index] = 0f;

            // Let beats register quickly, then hold their energy long enough for
            // the visuals to settle instead of snapping back between samples.
            float response = target > bands[index] ? AttackResponse : ReleaseResponse;
            float blend = 1f - Mathf.Exp(-response * Time.unscaledDeltaTime);
            bands[index] = Mathf.Lerp(bands[index], target, blend);
        }

        private void RefreshTargets()
        {
            RestoreTargetScales();
            environmentTargets.Clear();
            particleTargets.Clear();
            particleRadiusTargets.Clear();

            Runner_Chunk[] chunks = FindObjectsByType<Runner_Chunk>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            HashSet<Transform> registered = new HashSet<Transform>();
            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                Renderer[] renderers = chunks[chunkIndex].GetComponentsInChildren<Renderer>(false);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    Transform target = renderer.transform;
                    if (!registered.Add(target) || IsRoad(renderer, chunks[chunkIndex].transform) ||
                        !IsFinite(target.localScale))
                        continue;

                    int hash = StableHash(GetTransformPath(target, chunks[chunkIndex].transform));
                    environmentTargets.Add(new EnvironmentTarget
                    {
                        transform = target,
                        baseScale = target.localScale,
                        band = (Band)(Mathf.Abs(hash) % (int)Band.Count),
                        amount = 0.11f + Mathf.Abs((hash / 7) % 4) * 0.022f
                    });
                }
            }

            Kaleidoscope[] kaleidoscopes = FindObjectsByType<Kaleidoscope>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            registered.Clear();
            HashSet<Transform> registeredRadiusTargets = new HashSet<Transform>();
            for (int i = 0; i < kaleidoscopes.Length; i++)
            {
                Kaleidoscope kaleidoscope = kaleidoscopes[i];
                Transform target = FindParticleEffectRoot(kaleidoscope.transform);
                if (registered.Add(target) && IsFinite(target.localScale))
                {
                    particleTargets.Add(new ParticleKaleidoscopeTarget
                    {
                        transform = target,
                        baseScale = target.localScale,
                        band = i % 2 == 0 ? Band.Bass : Band.LowMid,
                        amount = 0.34f
                    });
                }

                Transform radiusTarget = kaleidoscope.baseObject;
                if (radiusTarget != null && registeredRadiusTargets.Add(radiusTarget) &&
                    IsFinite(radiusTarget.localPosition))
                {
                    int hash = StableHash(GetTransformPath(radiusTarget, target));
                    particleRadiusTargets.Add(new ParticleRadiusTarget
                    {
                        transform = radiusTarget,
                        basePosition = radiusTarget.localPosition,
                        band = Mathf.Abs(hash) % 2 == 0 ? Band.Bass : Band.LowMid
                    });
                }
            }

            refreshTimer = EnvironmentRefreshInterval;
        }

        private void PulseEnvironment()
        {
            for (int i = 0; i < environmentTargets.Count; i++)
            {
                EnvironmentTarget target = environmentTargets[i];
                if (target.transform == null || !IsFinite(target.baseScale))
                    continue;

                float pulse = bands[(int)target.band] * target.amount * EnvironmentScaleResponseMultiplier;
                if (!IsFinite(pulse))
                    continue;
                target.transform.localScale = Vector3.Scale(
                    target.baseScale,
                    new Vector3(1f + pulse * 0.45f, 1f + pulse, 1f + pulse * 0.45f));
            }
        }

        private void PulseParticleKaleidoscopes()
        {
            for (int i = 0; i < particleTargets.Count; i++)
            {
                ParticleKaleidoscopeTarget target = particleTargets[i];
                if (target.transform == null || !IsFinite(target.baseScale))
                    continue;

                float scale = 1f + bands[(int)target.band] * target.amount * ScaleResponseMultiplier;
                if (!IsFinite(scale))
                    continue;
                target.transform.localScale = target.baseScale * scale;
            }
        }

        private void PulseParticleRadii()
        {
            for (int i = 0; i < particleRadiusTargets.Count; i++)
            {
                ParticleRadiusTarget target = particleRadiusTargets[i];
                if (target.transform == null || !IsFinite(target.basePosition))
                    continue;

                float radiusScale = 1f + bands[(int)target.band] * ParticleRadiusResponseMultiplier;
                if (!IsFinite(radiusScale))
                    continue;

                // Kaleidoscope rotates this local offset around the origin to place
                // every particle, so scaling it changes the ring radius directly.
                target.transform.localPosition = target.basePosition * radiusScale;
            }
        }

        private void ModulateKalCamera()
        {
            if (kalCameraEffect == null)
                return;

            float baseAngle = kalCameraEffect.angle - previousKalAngleDelta;
            float baseScale = kalCameraEffect.scale - previousKalScaleDelta;
            float baseRadius = kalCameraEffect.radius - previousKalRadiusDelta;
            if (!IsFinite(baseAngle)) baseAngle = 0f;
            if (!IsFinite(baseScale)) baseScale = 1f;
            if (!IsFinite(baseRadius)) baseRadius = 0.5f;

            previousKalAngleDelta = bands[(int)Band.HighMid] * 6f;
            previousKalScaleDelta = bands[(int)Band.LowMid] * 0.075f * ScaleResponseMultiplier;
            previousKalRadiusDelta = bands[(int)Band.Treble] * 0.038f;

            kalCameraEffect.angle = baseAngle + previousKalAngleDelta;
            kalCameraEffect.scale = Mathf.Max(0.05f, baseScale + previousKalScaleDelta);
            kalCameraEffect.radius = Mathf.Max(0.01f, baseRadius + previousKalRadiusDelta);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsRoad(Renderer renderer, Transform chunkRoot)
        {
            Transform current = renderer.transform;
            while (current != null)
            {
                if (current.name.IndexOf("road", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (current == chunkRoot)
                    break;
                current = current.parent;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null &&
                filter.sharedMesh.name.IndexOf("road", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null &&
                    materials[i].name.IndexOf("road", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static Transform FindParticleEffectRoot(Transform child)
        {
            Transform current = child;
            while (current.parent != null && current.parent.GetComponent<Camera>() == null)
                current = current.parent;
            return current;
        }

        private static string GetTransformPath(Transform target, Transform root)
        {
            string path = target.name;
            while (target.parent != null && target != root)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return path;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                return hash == int.MinValue ? int.MaxValue : hash;
            }
        }

        private void RestoreTargetScales()
        {
            for (int i = 0; i < environmentTargets.Count; i++)
            {
                if (environmentTargets[i].transform != null)
                    environmentTargets[i].transform.localScale = environmentTargets[i].baseScale;
            }
            for (int i = 0; i < particleTargets.Count; i++)
            {
                if (particleTargets[i].transform != null)
                    particleTargets[i].transform.localScale = particleTargets[i].baseScale;
            }
            for (int i = 0; i < particleRadiusTargets.Count; i++)
            {
                if (particleRadiusTargets[i].transform != null)
                    particleRadiusTargets[i].transform.localPosition = particleRadiusTargets[i].basePosition;
            }
        }

        private void OnDisable()
        {
            RestoreTargetScales();
            if (kalCameraEffect != null)
            {
                kalCameraEffect.angle -= previousKalAngleDelta;
                kalCameraEffect.scale -= previousKalScaleDelta;
                kalCameraEffect.radius -= previousKalRadiusDelta;
            }
            previousKalAngleDelta = 0f;
            previousKalScaleDelta = 0f;
            previousKalRadiusDelta = 0f;
        }
    }
}
