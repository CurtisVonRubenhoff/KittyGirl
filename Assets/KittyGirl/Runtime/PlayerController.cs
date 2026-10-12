using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using VacuumShaders.CurvedWorld;
using ParallaxVisions.Models;
using ArcReactor.Demo;
using Kvant;
using KittyGirl.Survivors;

namespace ParallaxVisions
{

    [System.Serializable]
    [DefaultExecutionOrder(-10000)]
    public class PlayerController : MonoBehaviour
    {
        private const float MinimumSpecialWeaponBeamDistance = 40f;
        private const float SpecialWeaponBeamRadius = 0.9f;
        private const float SpecialWeaponDamageInterval = 0.1f;
        private const float SpecialWeaponImageEffectIntensity = 1.44f;

        [SerializeField]
        CurvedWorld_Controller WorldControler;
        [SerializeField]
        LatticeScroller LatScroll;

        [SerializeField]
        Animator playerAnim;

        [SerializeField]
        Weapon CurrentWeapon;
        [SerializeField]
        GameObject BulletPrefab;

        [SerializeField]
        GameObject UI_LoseHealth;

        [SerializeField]
        GameManager GM;

        [SerializeField]
        ArcReactorDemoGunController LaserBeam;

        [SerializeField, Min(0f)]
        float SpecialWeaponBeamDistance = MinimumSpecialWeaponBeamDistance;

        // Kept under their original serialized names so the legacy binary player
        // prefab can migrate its existing AudioSource references into SoundFxManager.
        [SerializeField, HideInInspector]
        private List<AudioSource> AUDIO_Shoot = new List<AudioSource>();

        [SerializeField, HideInInspector]
        private List<AudioSource> AUDIO_Ouch = new List<AudioSource>();

        private SoundFxManager soundFxManager;

        public bool canMove = true;
        public bool isInv = false;
        public bool onGunDelay = true;

        public int TripTime;

        public bool isTripping = false;

        public float SpecialWeaponTimeRemaining { get; private set; }

        public ParticleSystem effect_hurt;

        private float lastx, lasty;

        public int playerHealth = 9;

        public float SurvivorDamageMultiplier { get; private set; } = 1f;
        public float SurvivorAttackSpeed { get; private set; } = 1f;
        public float SurvivorMoveSpeed { get; private set; } = 1f;
        public float SurvivorProjectileSpeed { get; private set; } = 1f;
        public float SurvivorArea { get; private set; } = 1f;
        public int SurvivorBonusHealth { get; private set; }
        public int SurvivorMaxBonusHealth { get; private set; }
        public float SurvivorRecoveryPerSecond { get; private set; }
        private float survivorRecoveryBuffer;

        public float MinimumHorizontalPosition => IsArcade ? -10.5f : -10f;
        public float MaximumHorizontalPosition => IsArcade ? 10.5f : 11f;

        [SerializeField] bool IsArcade = false;

        private bool simulatedInputEnabled;
        private Vector2 simulatedMovement;
        private bool simulatedFire;
        private bool specialLaserFiring;
        private float nextSpecialWeaponDamageAt;
        private Coroutine specialWeaponRoutine;
        private MusicSpectrumReactor musicSpectrumReactor;
        private CameraFilterPack_Blend2Camera_Blend specialWeaponBlendEffect;
        private float previousSpecialWeaponBlendFx;
        private bool restoreSpecialWeaponBlendFx;
        private Vector3 defaultLocalPosition;
        private Quaternion defaultLocalRotation;
        private Vector3 defaultLocalScale;
        private readonly HashSet<Enemy> specialWeaponHitEnemies = new HashSet<Enemy>();
        private readonly List<CameraFilterPack_Vision_Psycho> psychoEffects =
            new List<CameraFilterPack_Vision_Psycho>();


        private void Awake()
        {
            defaultLocalPosition = transform.localPosition;
            defaultLocalRotation = transform.localRotation;
            defaultLocalScale = transform.localScale;

            // Curved World 2015 did not serialize this optional reference reliably.
            // Newer Unity versions report an exception as soon as the controller
            // tries to read it, so restore the player-relative pivot before the
            // vendor component's default-order Awake/Start methods run.
            if (WorldControler != null && WorldControler.pivotPoint == null)
                WorldControler.pivotPoint = transform;

            soundFxManager = FindFirstObjectByType<SoundFxManager>();
        }

        public void ResetToDefaultState()
        {
            StopAllCoroutines();
            specialWeaponRoutine = null;
            StopTripping();

            simulatedInputEnabled = false;
            simulatedMovement = Vector2.zero;
            simulatedFire = false;
            canMove = false;
            isInv = false;
            onGunDelay = true;

            transform.localPosition = defaultLocalPosition;
            transform.localRotation = defaultLocalRotation;
            transform.localScale = defaultLocalScale;

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (UI_LoseHealth != null)
                UI_LoseHealth.SetActive(false);
            if (effect_hurt != null)
                effect_hurt.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (playerAnim != null)
            {
                playerAnim.Rebind();
                playerAnim.Update(0f);
                playerAnim.SetFloat("MovementX", 0f);
                playerAnim.SetFloat("MovementY", 0f);
                playerAnim.SetBool("hurt", false);
            }
        }

        // Use this for initialization
        void Start()
        {
            CurrentWeapon = new Weapon(.0125f, 80, BulletPrefab);
            lastx = lasty = 0;
            ResolveSoundFxManager();
            if (soundFxManager != null)
            {
                soundFxManager.RegisterSources(SoundFxEvent.PlayerShot, AUDIO_Shoot);
                soundFxManager.RegisterSources(SoundFxEvent.PlayerHurt, AUDIO_Ouch);
            }
            //GM = GameManager.Instance;

        }

        // Update is called once per frame
        void Update()
        {
            RecoverSurvivorHealth();

            bool fireHeld = simulatedInputEnabled ? simulatedFire : Input.GetButton("Fire");
            if (isTripping)
            {
                if (fireHeld)
                    StartSpecialLaser();
                else
                    StopSpecialLaser();
            }
            else
            {
                StopSpecialLaser();
            }

            if (specialLaserFiring)
                DamageEnemiesInSpecialBeam();


            if (canMove)
            {
                Vector2 movementInput = simulatedInputEnabled ? simulatedMovement : ReadMovementInput();
                float inputx = movementInput.x;
                float inputy = movementInput.y;
                //WorldControler._V_CW_Bend_X = inputy * -4f;
                //WorldControler._V_CW_Bend_Y = inputx *13f;
                if (LatScroll != null)
                    LatScroll.yawAngle = -90f + inputx * 45f;

                if (IsArcade)
                {
                    var currentPosition = transform.position;

                    Vector2 direction = movementInput;
                    if (direction.sqrMagnitude > 0.01f)
                    {
                        direction = direction * (2.5f * SurvivorMoveSpeed * Time.deltaTime);
                        transform.position = new Vector3(
                            Mathf.Clamp(
                                currentPosition.x + (direction.x * 10.5f),
                                MinimumHorizontalPosition,
                                MaximumHorizontalPosition),
                            Mathf.Clamp(currentPosition.y + (direction.y * ((direction.y > 0) ? 5 : 4.2f)), 1, 10f),
                                8f);

                    }
                    else
                    {
                        var directionToCenter = new Vector2(0.5f - currentPosition.x, 5 - currentPosition.y);
                        transform.position = new Vector3(
                            currentPosition.x + (directionToCenter.x * Time.deltaTime * 2),
                            currentPosition.y + (directionToCenter.y * Time.deltaTime * 2),
                            8f);
                    }

                    currentPosition = transform.position;

                    var angle = Mathf.Sin(Time.time);

                    if (WorldControler != null)
                    {
                        WorldControler._V_CW_Bend_X = (currentPosition.x / 10.5f) + Mathf.Sin(Time.time) * -4f;
                        WorldControler._V_CW_Bend_Y = (currentPosition.y / 10f) + Mathf.Cos(Time.time) * 13f;
                    }
                }
                else
                {
                    transform.position = new Vector3(0.5f + (inputx * 10.5f), 6 + (inputy * ((inputy > 0) ? 5 : 4.2f)), 8f);
                }

                if (playerAnim != null)
                {
                    playerAnim.SetFloat("MovementX", inputx);
                    playerAnim.SetFloat("MovementY", inputy);
                }




                if (!isTripping && simulatedInputEnabled && simulatedFire)
                {
                    if (!onGunDelay)
                        StartCoroutine("ShootBullet");
                }
                else if (!isTripping && !simulatedInputEnabled && Input.GetButtonDown("Fire"))
                {
                    if (!onGunDelay)
                    {
                        StartCoroutine("ShootBullet");
                    }
                }


            }


        }

        private static Vector2 ReadMovementInput()
        {
            // Keep the configured axes for controllers, but do not make keyboard
            // movement depend on a particular Input Manager asset surviving a
            // Unity upgrade. Direct keys also work in fresh project settings.
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal = -1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal = 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical = -1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical = 1f;

            return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }

        public void SetSimulatedInput(bool enabled, Vector2 movement, bool fire)
        {
            simulatedInputEnabled = enabled;
            simulatedMovement = Vector2.ClampMagnitude(movement, 1f);
            simulatedFire = fire;
        }

        public IEnumerator ShootBullet()
        {
            onGunDelay = true;
            ResolveSoundFxManager();
            if (soundFxManager != null)
                soundFxManager.Play(SoundFxEvent.PlayerShot);
            if (CurrentWeapon.needsAmmo) {
                if (CurrentWeapon._RemainingBullets <= 0)
                {
                    StopCoroutine("ShootBullet");
                }
            }
            GameObject bullet = Instantiate(CurrentWeapon._PrefabBullet, transform.position, transform.rotation) as GameObject;
            Projectile projectile = bullet.GetComponent<Projectile>();
            if (projectile != null)
                projectile.bulletDamage = RollSurvivorDamage();
            bullet.transform.localScale *= SurvivorArea;
            bullet.GetComponent<Rigidbody>().linearVelocity = transform.forward * CurrentWeapon._PowerBullet * SurvivorProjectileSpeed;
            Destroy(bullet, .7f);
            yield return new WaitForSeconds(CurrentWeapon._Delay / SurvivorAttackSpeed);
            onGunDelay = false;
            //return null;
        }

        private IEnumerator GetHurt(int damage)
        {

            StartHurt();
            if (GM != null)
                GM.ApplyHitTimePenalty();
            ResolveSoundFxManager();
            if (soundFxManager != null)
                soundFxManager.Play(SoundFxEvent.PlayerHurt);
            int absorbedDamage = Mathf.Min(SurvivorBonusHealth, damage);
            SurvivorBonusHealth -= absorbedDamage;
            int remainingDamage = damage - absorbedDamage;
            if (remainingDamage > 0)
            {
                playerHealth -= remainingDamage;
                GM.LostLife(remainingDamage);
            }
            effect_hurt.Play();
            yield return new WaitForSeconds(.4f);
            EndHurt();
        }

		private void StartHurt()
		{
            isInv = true;
            onGunDelay = true;
           // canMove = false;
            playerAnim.SetBool("hurt", true);
            StopCoroutine(DisplayHealthLost());
            StartCoroutine(DisplayHealthLost());
        }

        private void EndHurt()
        {
            playerAnim.SetBool("hurt", false);

            isInv = false;
           // canMove = true;
            onGunDelay = false;
        }
        public void ActivateArcReactorWeapon(float duration = 30f)
        {
            if (LaserBeam == null)
            {
                Debug.LogWarning("The Arc Reactor weapon pickup was collected, but the player's LaserBeam is not configured.", this);
                return;
            }

            LaserBeam.SetMinimumBeamDistance(
                Mathf.Max(SpecialWeaponBeamDistance, MinimumSpecialWeaponBeamDistance));

            SurvivorStats stats = GetComponent<SurvivorStats>();
            if (stats != null)
                duration += stats.SpecialWeaponDurationBonus;

            if (specialWeaponRoutine != null)
                StopCoroutine(specialWeaponRoutine);
            ResolveSoundFxManager();
            if (soundFxManager != null)
                soundFxManager.Play(SoundFxEvent.SpecialWeaponActivated);
            specialWeaponRoutine = StartCoroutine(RunArcReactorWeapon(Mathf.Max(1f, duration)));
        }

        private void ResolveSoundFxManager()
        {
            if (soundFxManager == null)
                soundFxManager = GM != null ? GM.SoundFx : FindFirstObjectByType<SoundFxManager>();
        }

        public void ClearPowerUpEffects()
        {
            if (specialWeaponRoutine != null)
                StopCoroutine(specialWeaponRoutine);
            StopTripping();
        }

        private IEnumerator RunArcReactorWeapon(float duration)
        {
            StopSpecialLaser();
            isTripping = true;
            TripTime = Mathf.CeilToInt(duration);
            SpecialWeaponTimeRemaining = duration;
            EnablePsychoEffects();

            while (SpecialWeaponTimeRemaining > 0f)
            {
                SpecialWeaponTimeRemaining = Mathf.Max(0f, SpecialWeaponTimeRemaining - Time.deltaTime);
                AnimatePsychoEffects();
                yield return null;
            }

            StopTripping();
        }

        private void StopTripping() {
            StopSpecialLaser();
            isTripping = false;
            SpecialWeaponTimeRemaining = 0f;
            DisablePsychoEffects();
            specialWeaponRoutine = null;
        }

        private void StartSpecialLaser()
        {
            if (specialLaserFiring || LaserBeam == null)
                return;
            specialLaserFiring = LaserBeam.MouseDownShoot();
            if (specialLaserFiring)
                nextSpecialWeaponDamageAt = 0f;
        }

        private void StopSpecialLaser()
        {
            if (!specialLaserFiring)
                return;
            specialLaserFiring = false;
            if (LaserBeam != null)
                LaserBeam.MouseUpStop();
        }

        private void DamageEnemiesInSpecialBeam()
        {
            if (Time.time < nextSpecialWeaponDamageAt ||
                !LaserBeam.TryGetBeamVolume(out Transform beamOrigin, out float beamDistance))
                return;

            nextSpecialWeaponDamageAt = Time.time + SpecialWeaponDamageInterval;
            Vector3 beamEnd = beamOrigin.position + beamOrigin.forward * beamDistance;
            Collider[] hits = Physics.OverlapCapsule(
                beamOrigin.position,
                beamEnd,
                SpecialWeaponBeamRadius,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            specialWeaponHitEnemies.Clear();
            for (int i = 0; i < hits.Length; i++)
            {
                Enemy enemy = hits[i].GetComponentInParent<Enemy>();
                if (enemy != null && specialWeaponHitEnemies.Add(enemy))
                    enemy.TakeSpecialWeaponDamage(RollSurvivorDamage() * 2);
            }
        }

        private void EnablePsychoEffects()
        {
            DisablePsychoEffects();
            specialWeaponBlendEffect = FindFirstObjectByType<CameraFilterPack_Blend2Camera_Blend>();
            if (specialWeaponBlendEffect != null)
            {
                previousSpecialWeaponBlendFx = specialWeaponBlendEffect.BlendFX;
                restoreSpecialWeaponBlendFx = true;
                specialWeaponBlendEffect.BlendFX = 0.65f;
            }

            musicSpectrumReactor = FindFirstObjectByType<MusicSpectrumReactor>();
            KaleidoscopeEffect backgroundKaleidoscope = FindFirstObjectByType<KaleidoscopeEffect>();
            Camera backgroundCamera = backgroundKaleidoscope != null
                ? backgroundKaleidoscope.GetComponent<Camera>()
                : null;
            if (backgroundCamera == null || !backgroundCamera.enabled)
                return;

            CameraFilterPack_Vision_Psycho effect =
                backgroundCamera.GetComponent<CameraFilterPack_Vision_Psycho>();
            if (effect == null)
                effect = backgroundCamera.gameObject.AddComponent<CameraFilterPack_Vision_Psycho>();
            effect.HoleSize = 0.56f;
            effect.HoleSmooth = 0.12f;
            effect.Color1 = 0.08f;
            effect.Color2 = 0.55f;
            effect.enabled = true;
            psychoEffects.Add(effect);
        }

        private void AnimatePsychoEffects()
        {
            // Keep a little procedural motion between beats, but let the music's
            // bass and low mids drive most of the effect's impact.
            float mainPulse = (Mathf.Sin(Time.time * 4.6f) + 1f) * 0.5f;
            float shimmer = (Mathf.Sin(Time.time * 11.3f) + 1f) * 0.5f;
            float proceduralPulse = Mathf.SmoothStep(0f, 1f, mainPulse * 0.82f + shimmer * 0.18f);
            float pulse = proceduralPulse;
            if (musicSpectrumReactor != null && musicSpectrumReactor.HasActiveMusic)
                pulse = Mathf.Clamp01(0.1f + proceduralPulse * 0.1f + musicSpectrumReactor.PowerUpPulse * 0.65f);
            for (int i = psychoEffects.Count - 1; i >= 0; i--)
            {
                CameraFilterPack_Vision_Psycho effect = psychoEffects[i];
                if (effect == null)
                {
                    psychoEffects.RemoveAt(i);
                    continue;
                }
                effect.HoleSize = Mathf.Lerp(
                    0.62f,
                    0.62f + (0.46f - 0.62f) * SpecialWeaponImageEffectIntensity,
                    pulse);
                effect.HoleSmooth = Mathf.Lerp(
                    0.08f,
                    0.08f + (0.16f - 0.08f) * SpecialWeaponImageEffectIntensity,
                    pulse);
                effect.Color1 = Mathf.Lerp(
                    0.02f,
                    0.02f + (0.28f - 0.02f) * SpecialWeaponImageEffectIntensity,
                    pulse);
                effect.Color2 = Mathf.Lerp(
                    0.5f,
                    0.5f + (0.82f - 0.5f) * SpecialWeaponImageEffectIntensity,
                    pulse);
            }
        }

        private void DisablePsychoEffects()
        {
            if (restoreSpecialWeaponBlendFx && specialWeaponBlendEffect != null)
                specialWeaponBlendEffect.BlendFX = previousSpecialWeaponBlendFx;
            specialWeaponBlendEffect = null;
            restoreSpecialWeaponBlendFx = false;

            for (int i = 0; i < psychoEffects.Count; i++)
            {
                if (psychoEffects[i] != null)
                    psychoEffects[i].enabled = false;
            }
            psychoEffects.Clear();
        }


        private void OnTriggerEnter(Collider col)
        {
            if (col.tag == "EnemyBullet" || col.tag == "Enemy")
            {
                var eb = col.gameObject.GetComponent<Projectile>();
                if (!isInv)
                {
                    StartCoroutine(GetHurt(eb != null ? eb.bulletDamage : 1));
                }
            }

        }

        private void OnDisable()
        {
            ClearPowerUpEffects();
        }

        private IEnumerator DisplayHealthLost()
        {
            UI_LoseHealth.SetActive(true);
            yield return new WaitForSeconds(1);
            UI_LoseHealth.SetActive(false);
        }

        public void ApplySurvivorStats(
            float damageMultiplier,
            float attackSpeedMultiplier,
            float moveSpeedMultiplier,
            float projectileSpeedMultiplier,
            float areaMultiplier,
            int bonusMaxHealth,
            float recoveryPerSecond)
        {
            SurvivorDamageMultiplier = Mathf.Max(1f, damageMultiplier);
            SurvivorAttackSpeed = Mathf.Max(0.01f, attackSpeedMultiplier);
            SurvivorMoveSpeed = Mathf.Max(0.01f, moveSpeedMultiplier);
            SurvivorProjectileSpeed = Mathf.Max(0.01f, projectileSpeedMultiplier);
            SurvivorArea = Mathf.Max(0.01f, areaMultiplier);
            int addedHealth = Mathf.Max(0, bonusMaxHealth - SurvivorMaxBonusHealth);
            SurvivorMaxBonusHealth = Mathf.Max(0, bonusMaxHealth);
            SurvivorBonusHealth = Mathf.Min(SurvivorMaxBonusHealth, SurvivorBonusHealth + addedHealth);
            SurvivorRecoveryPerSecond = Mathf.Max(0f, recoveryPerSecond);
        }

        public int RollSurvivorDamage()
        {
            int guaranteedDamage = Mathf.FloorToInt(SurvivorDamageMultiplier);
            float fractionalDamage = SurvivorDamageMultiplier - guaranteedDamage;
            return guaranteedDamage + (Random.value < fractionalDamage ? 1 : 0);
        }

        public void RestoreSurvivorHealth(int amount)
        {
            if (amount <= 0)
                return;

            while (amount-- > 0)
            {
                if (SurvivorBonusHealth < SurvivorMaxBonusHealth)
                {
                    SurvivorBonusHealth++;
                }
                else if (GM != null && GM.PlayerLives < 9)
                {
                    GM.RestoreLife(1);
                    playerHealth = Mathf.Min(9, playerHealth + 1);
                }
            }
        }

        public void ResetSurvivorRun()
        {
            if (specialWeaponRoutine != null)
                StopCoroutine(specialWeaponRoutine);
            StopTripping();
            SurvivorBonusHealth = 0;
            SurvivorMaxBonusHealth = 0;
            SurvivorRecoveryPerSecond = 0f;
            survivorRecoveryBuffer = 0f;
            playerHealth = 9;
        }

        private void RecoverSurvivorHealth()
        {
            if (SurvivorRecoveryPerSecond <= 0f || GM == null || !GM.GameISRunning)
                return;

            survivorRecoveryBuffer += SurvivorRecoveryPerSecond * Time.deltaTime;
            if (survivorRecoveryBuffer < 1f)
                return;

            int healing = Mathf.FloorToInt(survivorRecoveryBuffer);
            survivorRecoveryBuffer -= healing;
            RestoreSurvivorHealth(healing);
        }

    }



}
