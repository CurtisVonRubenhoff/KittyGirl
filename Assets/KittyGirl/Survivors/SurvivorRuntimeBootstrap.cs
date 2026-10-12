using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ParallaxVisions;

namespace KittyGirl.Survivors
{
    public static class SurvivorRuntimeBootstrap
    {
        private const string ConfigResourceName = "SurvivorGameConfig";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindFirstObjectByType<ProceduralWaveDirector>() != null)
                return;

            SurvivorGameConfig config = Resources.Load<SurvivorGameConfig>(ConfigResourceName);
            if (config == null)
            {
                Debug.LogError(
                    "The procedural run could not start because Resources/SurvivorGameConfig is missing.");
                return;
            }

            GameObject host = new GameObject("Survivor Run");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<SurvivorBootstrapBehaviour>().Begin(config);
        }
    }

    internal sealed class SurvivorBootstrapBehaviour : MonoBehaviour
    {
        private SurvivorGameConfig config;
        private SurvivorStats stats;
        private LegacyGameBridge bridge;
        private SurvivorProgression progression;
        private ProceduralWaveDirector director;
        private GameManager gameManager;
        private int baseSeed;
        private int runNumber;

        public void Begin(SurvivorGameConfig config)
        {
            this.config = config;
            StartCoroutine(InstallWhenPlayerExists(config));
        }

        private IEnumerator InstallWhenPlayerExists(SurvivorGameConfig config)
        {
            GameObject player = null;
            float timeout = Time.realtimeSinceStartup + 15f;
            while (player == null && Time.realtimeSinceStartup < timeout)
            {
                player = LegacyReflection.FindPlayer();
                if (player == null)
                    yield return null;
            }

            if (player == null)
            {
                Debug.LogError("The procedural run could not find PlayerController or a Player-tagged object.", this);
                Destroy(gameObject);
                yield break;
            }

            DisableLegacySpawner();
            baseSeed = config.randomizeSeed
                ? unchecked(Environment.TickCount * 397 ^ SceneManager.GetActiveScene().buildIndex)
                : config.seed;

            stats = player.GetComponent<SurvivorStats>();
            if (stats == null)
                stats = player.AddComponent<SurvivorStats>();

            bridge = player.GetComponent<LegacyGameBridge>();
            if (bridge == null)
                bridge = player.AddComponent<LegacyGameBridge>();
            bridge.Initialize(stats);

            progression = gameObject.AddComponent<SurvivorProgression>();
            progression.Initialize(config, stats, baseSeed);

            director = gameObject.AddComponent<ProceduralWaveDirector>();
            director.Initialize(config, progression, stats, player.transform, baseSeed);

            SurvivorRunUI ui = gameObject.AddComponent<SurvivorRunUI>();
            ui.Initialize(progression, director);

            gameManager = GameManager.Instance;
            if (gameManager == null)
                gameManager = FindFirstObjectByType<GameManager>();
            if (gameManager != null)
            {
                gameManager.EnableProceduralWaves();
                gameManager.ProceduralRunStarted += ResetRun;
            }
        }

        private static void DisableLegacySpawner()
        {
            Component legacySpawner = LegacyReflection.FindComponent("EnemySpawner");
            Behaviour behaviour = legacySpawner as Behaviour;
            if (behaviour != null)
                behaviour.enabled = false;
        }

        private void ResetRun()
        {
            int seed = unchecked(baseSeed + ++runNumber * 7919);
            SurvivorPickup.ClearAll();
            bridge.ResetForNewRun();
            stats.ResetForNewRun();
            progression.ResetForNewRun(seed);
            director.ResetForNewRun(seed);
        }

        private void OnDestroy()
        {
            if (gameManager != null)
                gameManager.ProceduralRunStarted -= ResetRun;
        }
    }
}
