using System.Collections.Generic;
using ParallaxVisions;
using UnityEngine;

namespace KittyGirl.Survivors
{
    [DisallowMultipleComponent]
    public sealed class SurvivorPickup : MonoBehaviour
    {
        private enum PickupKind
        {
            Experience,
            Health
        }

        private readonly List<Material> runtimeMaterials = new List<Material>();
        private PickupKind kind;
        private SurvivorProgression progression;
        private SurvivorStats stats;
        private Transform player;
        private int amount;
        private float expiresAt;
        private float forwardSpeed;
        private float baseMagnetRange;
        private float magnetSpeed;
        private float bobOffset;
        private bool collected;

        public static void ClearAll()
        {
            SurvivorPickup[] pickups = FindObjectsByType<SurvivorPickup>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < pickups.Length; i++)
            {
                if (pickups[i] != null)
                    Destroy(pickups[i].gameObject);
            }
        }

        public static SurvivorPickup SpawnExperience(
            Vector3 position,
            SurvivorProgression progression,
            SurvivorStats stats,
            int amount,
            float lifetime,
            float forwardSpeed,
            float baseMagnetRange,
            float magnetSpeed)
        {
            return Spawn(PickupKind.Experience, position, progression, stats, amount, lifetime,
                forwardSpeed, baseMagnetRange, magnetSpeed);
        }

        public static SurvivorPickup SpawnHealth(
            Vector3 position,
            SurvivorStats stats,
            int amount,
            float lifetime,
            float forwardSpeed,
            float baseMagnetRange,
            float magnetSpeed)
        {
            return Spawn(PickupKind.Health, position, null, stats, amount, lifetime,
                forwardSpeed, baseMagnetRange, magnetSpeed);
        }

        private static SurvivorPickup Spawn(
            PickupKind kind,
            Vector3 position,
            SurvivorProgression progression,
            SurvivorStats stats,
            int amount,
            float lifetime,
            float forwardSpeed,
            float baseMagnetRange,
            float magnetSpeed)
        {
            GameObject pickupObject = new GameObject(
                kind == PickupKind.Experience ? "Experience Pickup" : "Health Pickup");
            pickupObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            pickupObject.transform.position = position + Vector3.up * 0.5f;

            SphereCollider trigger = pickupObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.65f;

            Rigidbody body = pickupObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            SurvivorPickup pickup = pickupObject.AddComponent<SurvivorPickup>();
            pickup.Initialize(
                kind,
                progression,
                stats,
                amount,
                lifetime,
                forwardSpeed,
                baseMagnetRange,
                magnetSpeed);
            return pickup;
        }

        private void Initialize(
            PickupKind pickupKind,
            SurvivorProgression runProgression,
            SurvivorStats survivorStats,
            int pickupAmount,
            float lifetime,
            float laneSpeed,
            float range,
            float speed)
        {
            kind = pickupKind;
            progression = runProgression;
            stats = survivorStats;
            player = survivorStats != null ? survivorStats.transform : null;
            amount = Mathf.Max(1, pickupAmount);
            expiresAt = Time.time + Mathf.Max(1f, lifetime);
            forwardSpeed = Mathf.Max(0.1f, laneSpeed);
            baseMagnetRange = Mathf.Max(0f, range);
            magnetSpeed = Mathf.Max(0.1f, speed);
            bobOffset = Random.value * Mathf.PI * 2f;

            Color color = kind == PickupKind.Experience
                ? new Color(0.15f, 0.85f, 1f, 1f)
                : new Color(1f, 0.18f, 0.32f, 1f);
            CreateVisual(color);
        }

        private void Update()
        {
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            transform.Rotate(0f, 120f * Time.deltaTime, 55f * Time.deltaTime, Space.Self);
            float pulse = 1f + Mathf.Sin(Time.time * 5f + bobOffset) * 0.08f;
            transform.localScale = Vector3.one * pulse;

            // Gameplay advances toward negative Z, so carry every drop down the
            // same lane as its defeated enemy until magnetism takes over.
            transform.position += Vector3.back * (forwardSpeed * Time.deltaTime);

            if (player == null)
                return;

            float magnetRange = baseMagnetRange + (stats != null ? stats.PickupMagnetRangeBonus : 0f);
            Vector3 offset = player.position - transform.position;
            if (offset.sqrMagnitude <= magnetRange * magnetRange)
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    player.position,
                    magnetSpeed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected)
                return;

            if (other.CompareTag("Gutter"))
            {
                collected = true;
                Destroy(gameObject);
                return;
            }

            PlayerController controller = other.GetComponentInParent<PlayerController>();
            if (controller == null)
                return;

            collected = true;
            if (kind == PickupKind.Experience)
            {
                if (progression != null)
                    progression.AwardExperience(amount);
            }
            else
            {
                controller.RestoreSurvivorHealth(amount);
            }
            if (GameManager.Instance != null && GameManager.Instance.SoundFx != null)
                GameManager.Instance.SoundFx.Play(SoundFxEvent.PickupCollected);
            Destroy(gameObject);
        }

        private void CreateVisual(Color color)
        {
            GameObject visual = GameObject.CreatePrimitive(
                kind == PickupKind.Experience ? PrimitiveType.Sphere : PrimitiveType.Cube);
            visual.name = kind == PickupKind.Experience ? "XP Orb" : "Health Crystal";
            visual.layer = gameObject.layer;
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = kind == PickupKind.Experience
                ? Vector3.one * 0.48f
                : new Vector3(0.55f, 0.55f, 0.22f);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                visualCollider.enabled = false;
                Destroy(visualCollider);
            }

            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return;

            Material material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }
            visual.GetComponent<Renderer>().material = material;
            runtimeMaterials.Add(material);

            Light glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = color;
            glow.range = 2.5f;
            glow.intensity = 1.2f;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < runtimeMaterials.Count; i++)
            {
                if (runtimeMaterials[i] != null)
                    Destroy(runtimeMaterials[i]);
            }
        }
    }
}
