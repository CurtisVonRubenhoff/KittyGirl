using System;
using System.Collections.Generic;
using ParallaxVisions;
using UnityEngine;

namespace KittyGirl.Survivors
{
    [DisallowMultipleComponent]
    public sealed class SurvivorProgression : MonoBehaviour
    {
        public event Action ProgressChanged;
        public event Action<IReadOnlyList<SurvivorUpgradeDefinition>> ChoicesOffered;
        public event Action ChoiceClosed;

        public int Level { get; private set; }
        public int Experience { get; private set; }
        public int ExperienceToNextLevel { get; private set; }
        public bool IsChoosing { get; private set; }
        public SurvivorStats Stats { get; private set; }

        private readonly List<SurvivorUpgradeDefinition> choices =
            new List<SurvivorUpgradeDefinition>();
        private SurvivorGameConfig config;
        private System.Random random;
        private int pendingLevelUps;
        private float timeScaleBeforeChoice = 1f;

        public void Initialize(SurvivorGameConfig gameConfig, SurvivorStats stats, int runSeed)
        {
            config = gameConfig;
            Stats = stats;
            ResetForNewRun(runSeed);
        }

        public void ResetForNewRun(int runSeed)
        {
            if (IsChoosing)
                Time.timeScale = timeScaleBeforeChoice;
            random = new System.Random(unchecked(runSeed * 397 ^ 0x4f9939));
            Level = 0;
            Experience = 0;
            ExperienceToNextLevel = Mathf.Max(1, config.startingExperienceToLevel);
            pendingLevelUps = 0;
            IsChoosing = false;
            choices.Clear();
            ChoiceClosed?.Invoke();
            ProgressChanged?.Invoke();
        }

        private void OnDisable()
        {
            // Upgrade selection pauses the whole game. Unity can disable this
            // component directly when play mode ends, bypassing both Choose and
            // ResetForNewRun, so always release the global pause here as well.
            if (!IsChoosing)
                return;

            Time.timeScale = timeScaleBeforeChoice;
            IsChoosing = false;
            pendingLevelUps = 0;
            choices.Clear();
        }

        public void AwardExperience(int baseAmount)
        {
            if (baseAmount <= 0 || config == null)
                return;

            // Attract mode is a demonstration, not a persistent run. Letting the
            // bot earn a level would pause the game and open a choice that only a
            // human can dismiss.
            GameManager gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.IsAttractMode)
                return;

            Experience += Mathf.Max(1, Mathf.RoundToInt(baseAmount * Stats.ExperienceMultiplier));
            while (Experience >= ExperienceToNextLevel)
            {
                Experience -= ExperienceToNextLevel;
                Level++;
                pendingLevelUps++;
                ExperienceToNextLevel = Mathf.Max(
                    1,
                    config.startingExperienceToLevel + Level * config.experienceGrowthPerLevel);
            }

            ProgressChanged?.Invoke();
            if (pendingLevelUps > 0 && !IsChoosing)
                OfferChoices();
        }

        public void Choose(int index)
        {
            if (!IsChoosing || index < 0 || index >= choices.Count)
                return;

            SurvivorUpgradeDefinition selected = choices[index];
            Stats.Apply(selected);
            if (selected.Id == SurvivorUpgradeId.BonusTime && GameManager.Instance != null)
                GameManager.Instance.AddTime(15f);
            pendingLevelUps = Mathf.Max(0, pendingLevelUps - 1);
            IsChoosing = false;
            choices.Clear();
            Time.timeScale = timeScaleBeforeChoice;
            ChoiceClosed?.Invoke();
            ProgressChanged?.Invoke();

            if (pendingLevelUps > 0)
                OfferChoices();
        }

        private void OfferChoices()
        {
            choices.Clear();
            List<SurvivorUpgradeDefinition> pool = new List<SurvivorUpgradeDefinition>();
            for (int i = 0; i < SurvivorStats.UpgradeCatalog.Length; i++)
            {
                SurvivorUpgradeDefinition definition = SurvivorStats.UpgradeCatalog[i];
                if (!Stats.IsMaxed(definition))
                    pool.Add(definition);
            }

            if (pool.Count == 0)
            {
                pendingLevelUps = 0;
                return;
            }

            int count = Mathf.Min(config.choicesPerLevel, pool.Count);
            while (choices.Count < count && pool.Count > 0)
            {
                int selected = ChooseWeightedIndex(pool);
                choices.Add(pool[selected]);
                pool.RemoveAt(selected);
            }

            timeScaleBeforeChoice = Time.timeScale;
            Time.timeScale = 0f;
            IsChoosing = true;
            ChoicesOffered?.Invoke(choices);
        }

        private int ChooseWeightedIndex(List<SurvivorUpgradeDefinition> pool)
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                int currentLevel = Stats.GetLevel(pool[i].Id);
                float continuationBonus = currentLevel > 0 ? 1f + Stats.Luck : 1f;
                total += pool[i].Weight * continuationBonus;
            }

            double roll = random.NextDouble() * total;
            for (int i = 0; i < pool.Count; i++)
            {
                int currentLevel = Stats.GetLevel(pool[i].Id);
                roll -= pool[i].Weight * (currentLevel > 0 ? 1f + Stats.Luck : 1f);
                if (roll <= 0d)
                    return i;
            }
            return pool.Count - 1;
        }
    }
}
