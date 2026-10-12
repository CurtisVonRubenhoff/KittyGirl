using UnityEngine;

namespace KittyGirl.Survivors
{
    [DisallowMultipleComponent]
    public sealed class EnemyRunModifier : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private ProceduralWaveDirector director;
        private SurvivorProgression progression;
        private Component[] components;
        private int experience;
        private float minimumHeight;
        private bool isElite;
        private float specialWeaponPickupLifetime;
        private float specialWeaponPickupSpeed;
        private float specialWeaponDuration;
        private SurvivorStats stats;
        private SurvivorGameConfig config;
        private bool initialized;
        private bool deathReported;

        public void Initialize(
            ProceduralWaveDirector waveDirector,
            SurvivorProgression runProgression,
            SurvivorGameConfig.EnemyArchetype archetype,
            float healthMultiplier,
            float speedMultiplier,
            float damageMultiplier,
            bool elite,
            SurvivorGameConfig config)
        {
            director = waveDirector;
            progression = runProgression;
            stats = runProgression != null ? runProgression.Stats : null;
            this.config = config;
            components = GetComponents<Component>();
            experience = Mathf.Max(1, Mathf.RoundToInt(
                archetype.experience * (elite ? config.eliteExperienceMultiplier : 1f)));
            minimumHeight = config.minimumEnemyHeight;
            isElite = elite;
            specialWeaponPickupLifetime = config.specialWeaponPickupLifetime;
            specialWeaponPickupSpeed = config.specialWeaponPickupSpeed;
            specialWeaponDuration = config.specialWeaponDuration;

            LegacyReflection.ScaleNumber(
                components,
                "EnemyHealth",
                healthMultiplier * archetype.healthMultiplier * (elite ? config.eliteHealthMultiplier : 1f));
            LegacyReflection.ScaleNumber(
                components,
                "moveSpeed",
                speedMultiplier * archetype.speedMultiplier);
            LegacyReflection.ScaleNumber(
                components,
                "PowerBullet",
                damageMultiplier * archetype.damageMultiplier * (elite ? config.eliteDamageMultiplier : 1f));

            if (elite)
                MakeElite(config.eliteTint);

            initialized = true;
        }

        public void ReportKilled(bool defeatedByPlayer = true)
        {
            if (deathReported)
                return;
            deathReported = true;
            if (!defeatedByPlayer)
                return;

            SurvivorPickup.SpawnExperience(
                transform.position,
                progression,
                stats,
                experience,
                config.pickupLifetime,
                config.pickupForwardSpeed,
                config.basePickupMagnetRange,
                config.pickupMagnetSpeed);

            float healthChance = Mathf.Clamp01(config.healthPickupChance * (1f + (stats != null ? stats.Luck : 0f)));
            if (Random.value < healthChance)
            {
                SurvivorPickup.SpawnHealth(
                    transform.position,
                    stats,
                    config.healthPickupAmount,
                    config.pickupLifetime,
                    config.pickupForwardSpeed,
                    config.basePickupMagnetRange,
                    config.pickupMagnetSpeed);
            }

            if (isElite)
                SpecialWeaponPickup.Spawn(
                    transform.position,
                    specialWeaponDuration,
                    specialWeaponPickupLifetime,
                    specialWeaponPickupSpeed);
        }

        private void Update()
        {
            if (!initialized || deathReported)
                return;

            bool isDead;
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                if (LegacyReflection.TryGetBool(component, "isDead", out isDead) && isDead)
                {
                    ReportKilled();
                    return;
                }

                float health;
                if (LegacyReflection.TryGetNumber(component, "EnemyHealth", out health) && health <= 0f)
                {
                    ReportKilled();
                    return;
                }
            }
        }

        private void LateUpdate()
        {
            if (!initialized)
                return;

            Vector3 position = transform.position;
            if (position.y < minimumHeight)
            {
                position.y = minimumHeight;
                transform.position = position;
            }
        }

        private void OnDestroy()
        {
            if (initialized && !deathReported)
                CheckDeathOneLastTime();
            if (director != null)
                director.NotifyEnemyRemoved(this);
        }

        private void CheckDeathOneLastTime()
        {
            if (components == null)
                return;
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                bool isDead;
                float health;
                if ((LegacyReflection.TryGetBool(component, "isDead", out isDead) && isDead) ||
                    (LegacyReflection.TryGetNumber(component, "EnemyHealth", out health) && health <= 0f))
                {
                    ReportKilled();
                    return;
                }
            }
        }

        private void MakeElite(Color tint)
        {
            transform.localScale *= 1.25f;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                target.GetPropertyBlock(block);
                block.SetColor(ColorId, tint);
                target.SetPropertyBlock(block);
            }
        }
    }
}
