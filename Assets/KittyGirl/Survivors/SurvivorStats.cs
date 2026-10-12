using System;
using System.Collections.Generic;
using UnityEngine;

namespace KittyGirl.Survivors
{
    public enum SurvivorUpgradeId
    {
        Power,
        Haste,
        FleetPaws,
        Vitality,
        Recovery,
        Growth,
        Fortune,
        ProjectileSpeed,
        Area,
        ArcDuration,
        Magnetism,
        BonusTime
    }

    public readonly struct SurvivorUpgradeDefinition
    {
        public readonly SurvivorUpgradeId Id;
        public readonly string Name;
        public readonly string Description;
        public readonly int MaxLevel;
        public readonly float Weight;

        public SurvivorUpgradeDefinition(
            SurvivorUpgradeId id,
            string name,
            string description,
            int maxLevel,
            float weight = 1f)
        {
            Id = id;
            Name = name;
            Description = description;
            MaxLevel = maxLevel;
            Weight = weight;
        }
    }

    [DisallowMultipleComponent]
    public sealed class SurvivorStats : MonoBehaviour
    {
        public static readonly SurvivorUpgradeDefinition[] UpgradeCatalog =
        {
            new SurvivorUpgradeDefinition(SurvivorUpgradeId.Power, "Power Paws", "+20% damage", 5),
            new SurvivorUpgradeDefinition(SurvivorUpgradeId.Haste, "Quick Claws", "+12% attack speed", 5),
            new SurvivorUpgradeDefinition(SurvivorUpgradeId.FleetPaws, "Fleet Paws", "+10% movement speed", 5),
            new SurvivorUpgradeDefinition(SurvivorUpgradeId.Vitality, "Nine Lives", "+2 maximum health and heal 2", 5, 0.85f),
            new SurvivorUpgradeDefinition(SurvivorUpgradeId.Recovery, "Cat Nap", "Recover health over time", 5, 0.8f),
            new SurvivorUpgradeDefinition(SurvivorUpgradeId.Growth, "Curiosity", "+15% experience gained", 5),
            new SurvivorUpgradeDefinition(SurvivorUpgradeId.Fortune, "Lucky Cat", "Better upgrade rolls and elite rewards", 5, 0.7f),
            new SurvivorUpgradeDefinition(SurvivorUpgradeId.ProjectileSpeed, "Zoomies", "+18% projectile speed", 5),
            new SurvivorUpgradeDefinition(SurvivorUpgradeId.Area, "Big Energy", "+12% projectile size", 5),
            new SurvivorUpgradeDefinition(SurvivorUpgradeId.ArcDuration, "Nine Volt Lives", "+15s Arc Beam duration", 4, 0.7f),
            new SurvivorUpgradeDefinition(SurvivorUpgradeId.Magnetism, "Cattraction", "+2.5 pickup magnet range", 5),
            new SurvivorUpgradeDefinition(SurvivorUpgradeId.BonusTime, "Time To Pounce", "+15 seconds", 5, 0.8f)
        };

        public event Action Changed;

        public float DamageMultiplier { get; private set; } = 1f;
        public float AttackSpeedMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public int BonusMaxHealth { get; private set; }
        public float RecoveryPerSecond { get; private set; }
        public float ExperienceMultiplier { get; private set; } = 1f;
        public float Luck { get; private set; }
        public float ProjectileSpeedMultiplier { get; private set; } = 1f;
        public float AreaMultiplier { get; private set; } = 1f;
        public float SpecialWeaponDurationBonus { get; private set; }
        public float PickupMagnetRangeBonus { get; private set; }

        private readonly Dictionary<SurvivorUpgradeId, int> levels =
            new Dictionary<SurvivorUpgradeId, int>();

        public int GetLevel(SurvivorUpgradeId id)
        {
            int level;
            return levels.TryGetValue(id, out level) ? level : 0;
        }

        public bool IsMaxed(SurvivorUpgradeDefinition upgrade)
        {
            return GetLevel(upgrade.Id) >= upgrade.MaxLevel;
        }

        public void Apply(SurvivorUpgradeDefinition upgrade)
        {
            int currentLevel = GetLevel(upgrade.Id);
            if (currentLevel >= upgrade.MaxLevel)
                return;

            levels[upgrade.Id] = currentLevel + 1;

            switch (upgrade.Id)
            {
                case SurvivorUpgradeId.Power:
                    DamageMultiplier += 0.20f;
                    break;
                case SurvivorUpgradeId.Haste:
                    AttackSpeedMultiplier += 0.12f;
                    break;
                case SurvivorUpgradeId.FleetPaws:
                    MoveSpeedMultiplier += 0.10f;
                    break;
                case SurvivorUpgradeId.Vitality:
                    BonusMaxHealth += 2;
                    break;
                case SurvivorUpgradeId.Recovery:
                    RecoveryPerSecond += 0.10f;
                    break;
                case SurvivorUpgradeId.Growth:
                    ExperienceMultiplier += 0.15f;
                    break;
                case SurvivorUpgradeId.Fortune:
                    Luck += 0.15f;
                    break;
                case SurvivorUpgradeId.ProjectileSpeed:
                    ProjectileSpeedMultiplier += 0.18f;
                    break;
                case SurvivorUpgradeId.Area:
                    AreaMultiplier += 0.12f;
                    break;
                case SurvivorUpgradeId.ArcDuration:
                    SpecialWeaponDurationBonus += 15f;
                    break;
                case SurvivorUpgradeId.Magnetism:
                    PickupMagnetRangeBonus += 2.5f;
                    break;
                case SurvivorUpgradeId.BonusTime:
                    break;
            }

            Changed?.Invoke();
        }

        public void ResetForNewRun()
        {
            levels.Clear();
            DamageMultiplier = 1f;
            AttackSpeedMultiplier = 1f;
            MoveSpeedMultiplier = 1f;
            BonusMaxHealth = 0;
            RecoveryPerSecond = 0f;
            ExperienceMultiplier = 1f;
            Luck = 0f;
            ProjectileSpeedMultiplier = 1f;
            AreaMultiplier = 1f;
            SpecialWeaponDurationBonus = 0f;
            PickupMagnetRangeBonus = 0f;
            Changed?.Invoke();
        }
    }
}
