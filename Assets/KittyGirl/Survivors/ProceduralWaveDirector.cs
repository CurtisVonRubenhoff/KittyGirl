using System;
using System.Collections.Generic;
using UnityEngine;
using ParallaxVisions;

namespace KittyGirl.Survivors
{
    [DisallowMultipleComponent]
    public sealed class ProceduralWaveDirector : MonoBehaviour
    {
        public event Action StateChanged;

        public int CurrentWave { get; private set; }
        public int RemainingBudget { get; private set; }
        public int AliveCount { get { return aliveEnemies.Count; } }
        public float WaveTimeRemaining { get; private set; }
        public bool IsResting { get; private set; }

        private readonly HashSet<EnemyRunModifier> aliveEnemies =
            new HashSet<EnemyRunModifier>();
        private readonly List<Transform> spawnPoints = new List<Transform>();
        private readonly List<SurvivorGameConfig.EnemyArchetype> candidates =
            new List<SurvivorGameConfig.EnemyArchetype>();

        private SurvivorGameConfig config;
        private SurvivorProgression progression;
        private SurvivorStats stats;
        private Transform player;
        private System.Random random;
        private float nextSpawnAt;
        private float restEndsAt;
        private float initialStartAt;
        private bool runStarted;

        public void Initialize(
            SurvivorGameConfig gameConfig,
            SurvivorProgression runProgression,
            SurvivorStats survivorStats,
            Transform playerTransform,
            int runSeed)
        {
            config = gameConfig;
            progression = runProgression;
            stats = survivorStats;
            player = playerTransform;
            random = new System.Random(runSeed);
            DiscoverSpawnPoints();
            initialStartAt = Time.unscaledTime + config.initialDelay;
        }

        public void ResetForNewRun(int runSeed)
        {
            random = new System.Random(runSeed);
            CurrentWave = 0;
            RemainingBudget = 0;
            WaveTimeRemaining = 0f;
            IsResting = false;
            runStarted = false;
            nextSpawnAt = 0f;
            restEndsAt = 0f;
            initialStartAt = Time.unscaledTime + 0.2f;
            aliveEnemies.Clear();
            StateChanged?.Invoke();
        }

        public void NotifyEnemyRemoved(EnemyRunModifier enemy)
        {
            aliveEnemies.Remove(enemy);
            StateChanged?.Invoke();
        }

        private void Update()
        {
            if (config == null || player == null)
                return;

            RemoveMissingEnemies();
            if (!runStarted)
            {
                if (Time.unscaledTime < initialStartAt || !LegacyRunIsActive())
                    return;
                runStarted = true;
                BeginNextWave();
            }

            if (!LegacyRunIsActive())
                return;

            if (IsResting)
            {
                if (Time.time >= restEndsAt)
                    BeginNextWave();
                return;
            }

            WaveTimeRemaining = Mathf.Max(0f, WaveTimeRemaining - Time.deltaTime);
            if (WaveTimeRemaining > 0f && RemainingBudget > 0 &&
                aliveEnemies.Count < config.maxAlive && Time.time >= nextSpawnAt)
                SpawnFromBudget();

            bool finishedSpawning = RemainingBudget <= 0 || WaveTimeRemaining <= 0f;
            if (finishedSpawning && aliveEnemies.Count == 0)
                BeginRest();
        }

        private void BeginNextWave()
        {
            bool wasResting = IsResting;
            CurrentWave++;
            if (GameManager.Instance != null)
            {
                if (wasResting)
                    GameManager.Instance.HideWaveClearMessage();
                GameManager.Instance.SetProceduralWaveNumber(CurrentWave);
                GameManager.Instance.SetWaveTheme(CurrentWave);
            }
            RemainingBudget = config.BudgetForWave(CurrentWave);
            WaveTimeRemaining = config.waveDuration;
            IsResting = false;
            nextSpawnAt = Time.time + 0.25f;
            StateChanged?.Invoke();
        }

        private void BeginRest()
        {
            IsResting = true;
            restEndsAt = Time.time + config.restDuration;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddTime(WaveTimeRemaining);
                GameManager.Instance.ShowWaveClearMessage(CurrentWave);
            }
            StateChanged?.Invoke();
        }

        private void SpawnFromBudget()
        {
            BuildCandidateList();
            if (candidates.Count == 0)
            {
                RemainingBudget = 0;
                Debug.LogWarning("No procedural enemy is available for wave " + CurrentWave + ".", this);
                return;
            }

            SurvivorGameConfig.EnemyArchetype archetype = ChooseWeighted(candidates);
            Vector3 lanePosition = ChooseSpawnPosition();
            lanePosition.y = Mathf.Max(config.minimumEnemyHeight, lanePosition.y);
            Vector3 entryPosition = lanePosition + Vector3.forward * config.flyInDistance;
            entryPosition.y = Mathf.Max(config.minimumEnemyHeight, entryPosition.y);

            GameObject instance = Instantiate(archetype.prefab, entryPosition, archetype.prefab.transform.rotation);
            instance.name = archetype.prefab.name + " [Wave " + CurrentWave + "]";

            bool elite = CurrentWave >= config.eliteStartsAtWave &&
                random.NextDouble() < Mathf.Clamp01(config.eliteChance * (1f + stats.Luck));
            float waveIndex = Mathf.Max(0, CurrentWave - 1);
            float healthMultiplier = 1f + waveIndex * config.healthGrowthPerWave;
            float damageMultiplier = 1f + waveIndex * config.damageGrowthPerWave;
            float speedMultiplier = 1f + waveIndex * config.speedGrowthPerWave;

            EnemyRunModifier modifier = instance.AddComponent<EnemyRunModifier>();
            modifier.Initialize(
                this,
                progression,
                archetype,
                healthMultiplier,
                speedMultiplier,
                damageMultiplier,
                elite,
                config);

            // Initialize after elite scaling so the fly-in grows to the final
            // gameplay size instead of visibly popping to it on arrival.
            EnemyFlyIn flyIn = instance.GetComponent<EnemyFlyIn>();
            if (flyIn == null)
                flyIn = instance.AddComponent<EnemyFlyIn>();
            flyIn.Initialize(lanePosition, config.flyInSpeed, config.minimumEnemyHeight);
            aliveEnemies.Add(modifier);

            RemainingBudget = Mathf.Max(0, RemainingBudget - archetype.cost);
            float pressure = 1f + CurrentWave * 0.025f;
            nextSpawnAt = Time.time + config.spawnInterval / pressure;
            StateChanged?.Invoke();
        }

        private void BuildCandidateList()
        {
            candidates.Clear();
            for (int i = 0; i < config.enemies.Count; i++)
            {
                SurvivorGameConfig.EnemyArchetype candidate = config.enemies[i];
                if (candidate != null && candidate.prefab != null &&
                    candidate.unlockWave <= CurrentWave && candidate.cost <= RemainingBudget)
                    candidates.Add(candidate);
            }

            if (candidates.Count == 0)
            {
                for (int i = 0; i < config.enemies.Count; i++)
                {
                    SurvivorGameConfig.EnemyArchetype candidate = config.enemies[i];
                    if (candidate != null && candidate.prefab != null && candidate.unlockWave <= CurrentWave)
                        candidates.Add(candidate);
                }
            }
        }

        private SurvivorGameConfig.EnemyArchetype ChooseWeighted(
            List<SurvivorGameConfig.EnemyArchetype> source)
        {
            float total = 0f;
            for (int i = 0; i < source.Count; i++)
                total += Mathf.Max(0.01f, source[i].weight);

            double roll = random.NextDouble() * total;
            for (int i = 0; i < source.Count; i++)
            {
                roll -= Mathf.Max(0.01f, source[i].weight);
                if (roll <= 0d)
                    return source[i];
            }
            return source[source.Count - 1];
        }

        private Vector3 ChooseSpawnPosition()
        {
            if (spawnPoints.Count > 0)
            {
                int start = random.Next(spawnPoints.Count);
                for (int offset = 0; offset < spawnPoints.Count; offset++)
                {
                    Transform point = spawnPoints[(start + offset) % spawnPoints.Count];
                    if (point != null)
                    {
                        float anchorHorizontal = Mathf.Lerp(
                            -config.fallbackSpread.x,
                            config.fallbackSpread.x,
                            (float)random.NextDouble());
                        float anchorVertical = Mathf.Lerp(
                            -config.fallbackSpread.y,
                            config.fallbackSpread.y,
                            (float)random.NextDouble());
                        return point.position + Vector3.right * anchorHorizontal + Vector3.up * anchorVertical;
                    }
                }
            }

            Vector3 direction = config.fallbackSpawnDirection.sqrMagnitude > 0.01f
                ? config.fallbackSpawnDirection.normalized
                : Vector3.forward;
            float distance = Mathf.Lerp(
                config.minimumSpawnDistance,
                config.maximumSpawnDistance,
                (float)random.NextDouble());
            float horizontal = Mathf.Lerp(-config.fallbackSpread.x, config.fallbackSpread.x, (float)random.NextDouble());
            float vertical = Mathf.Lerp(-config.fallbackSpread.y, config.fallbackSpread.y, (float)random.NextDouble());
            return player.position + direction * distance + Vector3.right * horizontal + Vector3.up * vertical;
        }

        private void DiscoverSpawnPoints()
        {
            Transform[] transforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                string candidateName = candidate.name.ToLowerInvariant();
                if (candidateName.Contains("enemyspawn") || candidateName.Contains("enemy spawn"))
                    spawnPoints.Add(candidate);
            }
        }

        private bool LegacyRunIsActive()
        {
            GameManager gameManager = GameManager.Instance;
            if (gameManager == null)
                return true;
            return gameManager.GameISRunning;
        }

        private void RemoveMissingEnemies()
        {
            aliveEnemies.RemoveWhere(enemy => enemy == null);
        }
    }
}
