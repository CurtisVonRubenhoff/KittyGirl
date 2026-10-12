using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ParallaxVisions.Models;
using Klak.Motion;
using KittyGirl.Survivors;

namespace ParallaxVisions
{
    public class Enemy : MonoBehaviour
    {
        public float Delay;
        public bool needsAmmo = false;
        public int RemainingBullets;
        public int PowerBullet;
        public int moveSpeed = 1;
        public GameObject bullet;

        public int EnemyHealth;

        [SerializeField]
        private BrownianMotion MyBrown;

        [Header("Rest Movement")]
        [SerializeField, Min(0f)]
        private float restTrackingSpeed = 1.5f;

        [SerializeField, Tooltip("Aiming enemies hold this world-space height while tracking the player.")]
        private float homingPreferredHeight = 8f;

        public int value;

        public bool isDead = false;
        public bool takeAim = false;
        public ParticleSystem Explosion;

        GameManager GM;

        Weapon CurrentWeapon;
        bool onGunDelay;
        bool isResting;
        Transform playerTarget;
        PlayerController playerController;

        private void Awake()
        {
            ResolveGameManager();
            ResolveRestTargets();
        }

        // Use this for initialization
        protected void Start()
        {
            CurrentWeapon = new Weapon(Delay, PowerBullet, bullet);
            ResolveGameManager();

        }

        // Update is called once per frame
        protected void Update()
        {
            if (GM == null)
                ResolveGameManager();

            if (GM != null && GM.GameISRunning)
            {
                if (!isDead)
                {
                    if (isResting && MyBrown != null && MyBrown.enabled)
                        TrackPlayerDuringRest();
                    else
                        transform.Translate(-Vector3.forward * Time.deltaTime * moveSpeed);
                    if (!onGunDelay) StartCoroutine("ShootBullet");
                }
            }
        }
        protected IEnumerator ShootBullet()
        {
            if (!onGunDelay)
            {
                onGunDelay = true;
                if (CurrentWeapon.needsAmmo)
                {
                    if (CurrentWeapon._RemainingBullets <= 0)
                    {
                        StopCoroutine("ShootBullet");
                    }
                }
                GameObject bullet = Instantiate(CurrentWeapon._PrefabBullet, transform.position, transform.rotation) as GameObject;
                var playerpos = GameObject.FindGameObjectWithTag("Player").transform.position;
                var mypos = transform.position;
                var targetshot = new Vector3((playerpos.x - mypos.x) / 8, (playerpos.y - mypos.y) / 8, (playerpos.z - mypos.z) / 8);
                targetshot = targetshot.normalized;
                bullet.GetComponent<Rigidbody>().linearVelocity = (takeAim) ? targetshot * CurrentWeapon._PowerBullet : -Vector3.forward * CurrentWeapon._PowerBullet;
                Destroy(bullet, 6);
                yield return new WaitForSeconds(CurrentWeapon._Delay);
                onGunDelay = false;

            }

        }

        protected IEnumerator KillSelf(bool defeatedByPlayer)
        {
            isDead = true;
            EnemyRunModifier runModifier = GetComponent<EnemyRunModifier>();
            if (runModifier != null)
                runModifier.ReportKilled(defeatedByPlayer);
            CollectPoints();
            if (Explosion != null)
                Explosion.Play();
            if (GM != null)
                GM.KilledOne();
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
            yield return new WaitForSeconds(1);
            Destroy(gameObject);
        }

        public void ArcReactorHit(ArcReactorHitInfo hit)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            TakeSpecialWeaponDamage(player != null ? player.RollSurvivorDamage() * 2 : 2);
        }

        public void TakeSpecialWeaponDamage(int damage)
        {
            if (isDead)
                return;

            CollectPoints();
            GetHit(damage);
        }

        private void CollectPoints() {
            if (GM == null)
                ResolveGameManager();
            if (GM != null)
                GM.AddScore(value);
        }

        private void ResolveGameManager()
        {
            if (GM == null)
                GM = FindFirstObjectByType<GameManager>();
        }

        private void ResolveRestTargets()
        {
            if (MyBrown == null)
                MyBrown = GetComponentInChildren<BrownianMotion>(true);

            if (playerTarget == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerTarget = player.transform;
            }

            if (playerController == null && playerTarget != null)
                playerController = playerTarget.GetComponent<PlayerController>();
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();
        }

        private void TrackPlayerDuringRest()
        {
            if (playerTarget == null)
                ResolveRestTargets();
            if (playerTarget == null)
                return;

            Vector3 target = playerTarget.position;
            if (playerController != null)
            {
                target.x = Mathf.Clamp(
                    target.x,
                    playerController.MinimumHorizontalPosition,
                    playerController.MaximumHorizontalPosition);
            }
            if (takeAim)
                target.y = Mathf.Max(target.y, homingPreferredHeight);

            MyBrown.MovePositionAnchorTowards(
                target,
                restTrackingSpeed * Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (!isResting || playerController == null)
                return;

            Vector3 position = transform.position;
            position.x = Mathf.Clamp(
                position.x,
                playerController.MinimumHorizontalPosition,
                playerController.MaximumHorizontalPosition);
            transform.position = position;
        }

        private void GetHit(int damage = 1)
        {
            EnemyHealth -= Mathf.Max(1, damage);
            if (EnemyHealth < 1) StartCoroutine(KillSelf(true));
        }

        protected void OnTriggerEnter(Collider col)
        {
            if (col.tag == "Gutter") StartCoroutine(KillSelf(false));
            if (col.tag == "PlayerBullet") {
                Debug.Log("I'm Hit");
                if (!isDead)
                {
                    CollectPoints();
                    Projectile projectile = col.GetComponent<Projectile>();
                    GetHit(projectile != null ? projectile.bulletDamage : 1);
                }
            }
            if (col.CompareTag("Stopper")) {
                ResolveRestTargets();
                isResting = true;
                if (MyBrown != null)
                    MyBrown.enabled = true;
            }

        }
    }
}
