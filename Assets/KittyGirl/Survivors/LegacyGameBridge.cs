using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ParallaxVisions;

namespace KittyGirl.Survivors
{
    internal static class LegacyReflection
    {
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static Component FindComponent(string typeName, bool includeInactive = true)
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.GetType().Name == typeName)
                    return behaviour;
            }
            return null;
        }

        public static bool TryGetNumber(object target, string fieldName, out float value)
        {
            value = 0f;
            if (target == null)
                return false;

            FieldInfo field = target.GetType().GetField(fieldName, Flags);
            if (field == null)
                return false;

            object raw = field.GetValue(target);
            if (raw == null || !(raw is IConvertible))
                return false;

            try
            {
                value = Convert.ToSingle(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TrySetNumber(object target, string fieldName, float value)
        {
            if (target == null)
                return false;

            FieldInfo field = target.GetType().GetField(fieldName, Flags);
            if (field == null)
                return false;

            try
            {
                Type type = field.FieldType;
                if (type == typeof(int)) field.SetValue(target, Mathf.RoundToInt(value));
                else if (type == typeof(float)) field.SetValue(target, value);
                else if (type == typeof(double)) field.SetValue(target, (double)value);
                else return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetBool(object target, string fieldName, out bool value)
        {
            value = false;
            if (target == null)
                return false;
            FieldInfo field = target.GetType().GetField(fieldName, Flags);
            if (field == null || field.FieldType != typeof(bool))
                return false;
            value = (bool)field.GetValue(target);
            return true;
        }

        public static void ScaleNumber(Component[] components, string fieldName, float multiplier)
        {
            for (int i = 0; i < components.Length; i++)
            {
                float value;
                if (TryGetNumber(components[i], fieldName, out value))
                    TrySetNumber(components[i], fieldName, value * multiplier);
            }
        }

        public static GameObject FindPlayer()
        {
            try
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                    return tagged;
            }
            catch (UnityException) { }

            Component controller = FindComponent("PlayerController");
            if (controller != null)
                return controller.gameObject;

            GameObject named = GameObject.Find("Player");
            return named;
        }
    }

    [DisallowMultipleComponent]
    public sealed class LegacyGameBridge : MonoBehaviour
    {
        private sealed class Baseline
        {
            public object target;
            public string field;
            public float value;
            public bool inverse;
        }

        private static readonly string[] DamageFields =
            { "bulletDamage", "damage", "Damage", "PowerBullet" };
        private static readonly string[] CooldownFields =
            { "fireDelay", "shotDelay", "cooldown", "rechargeTime", "Delay" };

        private readonly List<Baseline> playerBaselines = new List<Baseline>();
        private readonly HashSet<int> adjustedProjectiles = new HashSet<int>();
        private SurvivorStats stats;
        private float recoveryBuffer;
        private int baseHealth;
        private int appliedBonusHealth;
        private Component healthOwner;
        private Vector3 previousPosition;
        private PlayerController directPlayer;

        public void Initialize(SurvivorStats survivorStats)
        {
            stats = survivorStats;
            directPlayer = GetComponent<PlayerController>();
            CachePlayerFields();
            previousPosition = transform.position;
            stats.Changed += ApplyPlayerStats;
            ApplyPlayerStats();
        }

        private void OnDestroy()
        {
            if (stats != null)
                stats.Changed -= ApplyPlayerStats;
        }

        private void Update()
        {
            if (stats == null)
                return;

            if (directPlayer == null)
                ApplyToNewProjectiles();
            ApplyRecovery();
        }

        private void LateUpdate()
        {
            if (stats == null || directPlayer != null)
                return;

            Vector3 current = transform.position;
            Vector3 movement = current - previousPosition;
            if (stats.MoveSpeedMultiplier > 1f && movement.sqrMagnitude > 0.000001f && movement.sqrMagnitude < 9f)
            {
                transform.position += movement * (stats.MoveSpeedMultiplier - 1f);
                current = transform.position;
            }
            previousPosition = current;
        }

        private void CachePlayerFields()
        {
            Component[] components = GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                for (int n = 0; n < DamageFields.Length; n++)
                    AddBaseline(component, DamageFields[n], false);
                for (int n = 0; n < CooldownFields.Length; n++)
                    AddBaseline(component, CooldownFields[n], true);
                CacheNestedCooldowns(component);

                float health;
                if (healthOwner == null && LegacyReflection.TryGetNumber(component, "playerHealth", out health))
                {
                    healthOwner = component;
                    baseHealth = Mathf.Max(1, Mathf.RoundToInt(health));
                }
            }
        }

        private void AddBaseline(Component component, string fieldName, bool inverse)
        {
            float value;
            if (!LegacyReflection.TryGetNumber(component, fieldName, out value))
                return;
            playerBaselines.Add(new Baseline
            {
                target = component,
                field = fieldName,
                value = value,
                inverse = inverse
            });
        }

        private void CacheNestedCooldowns(Component component)
        {
            FieldInfo[] fields = component.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                object value;
                try { value = fields[i].GetValue(component); }
                catch { continue; }
                IEnumerable collection = value as IEnumerable;
                if (collection == null || value is string)
                    continue;

                foreach (object item in collection)
                {
                    if (item == null)
                        continue;
                    for (int n = 0; n < CooldownFields.Length; n++)
                    {
                        float baseline;
                        if (LegacyReflection.TryGetNumber(item, CooldownFields[n], out baseline))
                        {
                            playerBaselines.Add(new Baseline
                            {
                                target = item,
                                field = CooldownFields[n],
                                value = baseline,
                                inverse = true
                            });
                        }
                    }
                }
            }
        }

        private void ApplyPlayerStats()
        {
            if (directPlayer != null)
            {
                directPlayer.ApplySurvivorStats(
                    stats.DamageMultiplier,
                    stats.AttackSpeedMultiplier,
                    stats.MoveSpeedMultiplier,
                    stats.ProjectileSpeedMultiplier,
                    stats.AreaMultiplier,
                    stats.BonusMaxHealth,
                    stats.RecoveryPerSecond);
            }

            for (int i = 0; i < playerBaselines.Count; i++)
            {
                Baseline baseline = playerBaselines[i];
                if (baseline.target == null)
                    continue;
                float multiplier = baseline.inverse
                    ? 1f / stats.AttackSpeedMultiplier
                    : stats.DamageMultiplier;
                LegacyReflection.TrySetNumber(
                    baseline.target,
                    baseline.field,
                    baseline.value * multiplier);
            }

            if (directPlayer == null && healthOwner != null)
            {
                float current;
                if (LegacyReflection.TryGetNumber(healthOwner, "playerHealth", out current))
                {
                    int newMaximum = baseHealth + stats.BonusMaxHealth;
                    int newlyAddedHealth = Mathf.Max(0, stats.BonusMaxHealth - appliedBonusHealth);
                    int result = Mathf.Min(newMaximum, Mathf.RoundToInt(current) + newlyAddedHealth);
                    LegacyReflection.TrySetNumber(healthOwner, "playerHealth", result);
                    appliedBonusHealth = stats.BonusMaxHealth;
                }
            }
        }

        private void ApplyRecovery()
        {
            if (directPlayer != null || healthOwner == null || stats.RecoveryPerSecond <= 0f)
                return;

            float current;
            if (!LegacyReflection.TryGetNumber(healthOwner, "playerHealth", out current))
                return;

            int maximum = baseHealth + stats.BonusMaxHealth;
            if (current >= maximum)
                return;

            recoveryBuffer += stats.RecoveryPerSecond * Time.deltaTime;
            if (recoveryBuffer < 1f)
                return;

            int healing = Mathf.FloorToInt(recoveryBuffer);
            recoveryBuffer -= healing;
            int result = Mathf.Min(maximum, Mathf.RoundToInt(current) + healing);
            LegacyReflection.TrySetNumber(healthOwner, "playerHealth", result);
        }

        private void ApplyToNewProjectiles()
        {
            GameObject[] projectiles;
            try
            {
                projectiles = GameObject.FindGameObjectsWithTag("PlayerBullet");
            }
            catch (UnityException)
            {
                return;
            }

            for (int i = 0; i < projectiles.Length; i++)
            {
                GameObject projectile = projectiles[i];
                int id = projectile.GetInstanceID();
                if (!adjustedProjectiles.Add(id))
                    continue;

                Component[] components = projectile.GetComponents<Component>();
                for (int n = 0; n < DamageFields.Length; n++)
                    LegacyReflection.ScaleNumber(components, DamageFields[n], stats.DamageMultiplier);

                projectile.transform.localScale *= stats.AreaMultiplier;
                Rigidbody body = projectile.GetComponent<Rigidbody>();
                if (body != null)
                    body.linearVelocity *= stats.ProjectileSpeedMultiplier;
                Rigidbody2D body2D = projectile.GetComponent<Rigidbody2D>();
                if (body2D != null)
                    body2D.linearVelocity *= stats.ProjectileSpeedMultiplier;
            }
        }

        public void ResetForNewRun()
        {
            recoveryBuffer = 0f;
            appliedBonusHealth = 0;
            if (directPlayer != null)
            {
                directPlayer.ResetSurvivorRun();
                return;
            }
            if (healthOwner != null)
                LegacyReflection.TrySetNumber(healthOwner, "playerHealth", baseHealth);
        }
    }
}
