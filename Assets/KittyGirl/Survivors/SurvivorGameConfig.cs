using System;
using System.Collections.Generic;
using UnityEngine;

namespace KittyGirl.Survivors
{
    [CreateAssetMenu(fileName = "SurvivorGameConfig", menuName = "Kitty Girl/Survivor Game Config")]
    public sealed class SurvivorGameConfig : ScriptableObject
    {
        [Serializable]
        public sealed class EnemyArchetype
        {
            public string id = "enemy";
            public GameObject prefab;
            [Min(0.01f)] public float weight = 1f;
            [Min(1)] public int cost = 1;
            [Min(1)] public int unlockWave = 1;
            [Min(0.01f)] public float healthMultiplier = 1f;
            [Min(0.01f)] public float speedMultiplier = 1f;
            [Min(0.01f)] public float damageMultiplier = 1f;
            [Min(1)] public int experience = 1;
        }

        [Header("Run")]
        public int seed = 1337;
        public bool randomizeSeed = true;
        [Min(0f)] public float initialDelay = 1.5f;
        [Min(0f)] public float restDuration = 3f;
        [Min(1f)] public float waveDuration = 28f;

        [Header("Difficulty")]
        [Min(1)] public int baseBudget = 8;
        [Min(0)] public int budgetPerWave = 3;
        [Min(1f)] public float budgetGrowth = 1.06f;
        [Min(1)] public int maxAlive = 24;
        [Min(0.05f)] public float spawnInterval = 0.65f;
        [Min(0f)] public float healthGrowthPerWave = 0.13f;
        [Min(0f)] public float damageGrowthPerWave = 0.07f;
        [Min(0f)] public float speedGrowthPerWave = 0.025f;

        [Header("Spawning")]
        [Min(1f)] public float minimumSpawnDistance = 16f;
        [Min(2f)] public float maximumSpawnDistance = 25f;
        public Vector3 fallbackSpawnDirection = Vector3.forward;
        public Vector2 fallbackSpread = new Vector2(12f, 5f);
        [Tooltip("Extra depth enemies travel before joining the normal forward-moving lane.")]
        [Min(0f)] public float flyInDistance = 28f;
        [Min(0.1f)] public float flyInSpeed = 22f;
        [Tooltip("Lowest allowed enemy pivot height. Keeps procedural spawns above the floor.")]
        public float minimumEnemyHeight = 1.8f;

        [Header("Elites")]
        [Min(0)] public int eliteStartsAtWave = 4;
        [Range(0f, 1f)] public float eliteChance = 0.08f;
        [Min(1f)] public float eliteHealthMultiplier = 3f;
        [Min(1f)] public float eliteDamageMultiplier = 1.5f;
        [Min(1f)] public float eliteExperienceMultiplier = 4f;
        public Color eliteTint = new Color(0.9f, 0.2f, 0.85f, 1f);

        [Header("Elite Weapon Drop")]
        [Tooltip("How long an uncollected Arc Reactor weapon remains in the arena.")]
        [Min(1f)] public float specialWeaponPickupLifetime = 20f;
        [Tooltip("How quickly the Arc Reactor pickup travels forward toward the player and gutter.")]
        [Min(0.1f)] public float specialWeaponPickupSpeed = 6f;
        [Tooltip("Seconds of Arc Reactor laser use granted when the pickup is touched.")]
        [Min(1f)] public float specialWeaponDuration = 30f;

        [Header("Progression")]
        [Min(1)] public int startingExperienceToLevel = 5;
        [Min(0)] public int experienceGrowthPerLevel = 4;
        [Range(2, 4)] public int choicesPerLevel = 3;

        [Header("Pickups")]
        [Tooltip("Chance for a player-defeated enemy to also drop health.")]
        [Range(0f, 1f)] public float healthPickupChance = 0.08f;
        [Min(1)] public int healthPickupAmount = 1;
        [Min(1f)] public float pickupLifetime = 25f;
        [Tooltip("How quickly XP and health pickups travel down the lane toward the player.")]
        [Min(0.1f)] public float pickupForwardSpeed = 6f;
        [Min(0f)] public float basePickupMagnetRange = 3f;
        [Min(0.1f)] public float pickupMagnetSpeed = 14f;

        public List<EnemyArchetype> enemies = new List<EnemyArchetype>();

        public int BudgetForWave(int wave)
        {
            float linearBudget = baseBudget + Mathf.Max(0, wave - 1) * budgetPerWave;
            return Mathf.Max(1, Mathf.RoundToInt(linearBudget * Mathf.Pow(budgetGrowth, wave - 1)));
        }
    }
}
