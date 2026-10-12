using System.Collections.Generic;
using ParallaxVisions;
using UnityEngine;

namespace KittyGirl.Survivors
{
    [DisallowMultipleComponent]
    public sealed class SpecialWeaponPickup : MonoBehaviour
    {
        private readonly List<Material> runtimeMaterials = new List<Material>();
        private float weaponDuration;
        private float forwardSpeed;
        private float expiresAt;
        private float baseHeight;
        private bool resolved;

        public static SpecialWeaponPickup Spawn(
            Vector3 position,
            float weaponDuration,
            float pickupLifetime,
            float pickupSpeed = 6f)
        {
            GameObject pickupObject = new GameObject("Elite Arc Reactor Weapon Pickup");
            pickupObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            pickupObject.transform.position = position + Vector3.up * 0.8f;

            SphereCollider trigger = pickupObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.15f;

            Rigidbody body = pickupObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            SpecialWeaponPickup pickup = pickupObject.AddComponent<SpecialWeaponPickup>();
            pickup.Initialize(weaponDuration, pickupLifetime, pickupSpeed);
            return pickup;
        }

        private void Initialize(float duration, float lifetime, float speed)
        {
            weaponDuration = Mathf.Max(1f, duration);
            forwardSpeed = Mathf.Max(0.1f, speed);
            expiresAt = Time.time + Mathf.Max(1f, lifetime);
            baseHeight = transform.position.y;

            CreateVisual(PrimitiveType.Cylinder, "Beam Core", new Vector3(0.22f, 0.85f, 0.22f),
                Quaternion.Euler(90f, 0f, 0f), new Color(0.2f, 1f, 1f, 1f));
            CreateVisual(PrimitiveType.Cube, "Energy Cross A", new Vector3(1.25f, 0.12f, 0.12f),
                Quaternion.Euler(0f, 0f, 45f), new Color(1f, 0.15f, 0.85f, 1f));
            CreateVisual(PrimitiveType.Cube, "Energy Cross B", new Vector3(0.12f, 1.25f, 0.12f),
                Quaternion.Euler(0f, 0f, 45f), new Color(0.45f, 0.2f, 1f, 1f));

            Light glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.3f, 0.65f, 1f);
            glow.range = 4f;
            glow.intensity = 2.2f;
        }

        private void Update()
        {
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.09f;
            transform.localScale = Vector3.one * pulse;
            transform.Rotate(37f * Time.deltaTime, 93f * Time.deltaTime, 61f * Time.deltaTime, Space.Self);
            Vector3 position = transform.position;
            position.y = baseHeight + Mathf.Sin(Time.time * 3.5f) * 0.32f;
            position += Vector3.back * (forwardSpeed * Time.deltaTime);
            transform.position = position;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (resolved)
                return;

            if (other.CompareTag("Gutter"))
            {
                resolved = true;
                Destroy(gameObject);
                return;
            }

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null)
                return;

            resolved = true;
            player.ActivateArcReactorWeapon(weaponDuration);
            Destroy(gameObject);
        }

        private void CreateVisual(
            PrimitiveType primitive,
            string visualName,
            Vector3 scale,
            Quaternion rotation,
            Color color)
        {
            GameObject visual = GameObject.CreatePrimitive(primitive);
            visual.name = visualName;
            visual.layer = gameObject.layer;
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = scale;
            visual.transform.localRotation = rotation;

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
                material.SetColor("_EmissionColor", color * 1.8f);
            }
            visual.GetComponent<Renderer>().material = material;
            runtimeMaterials.Add(material);
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
