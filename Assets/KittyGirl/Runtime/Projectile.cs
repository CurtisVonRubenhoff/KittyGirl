using UnityEngine;
using System.Collections;

namespace ParallaxVisions {
    public class Projectile : MonoBehaviour {
        public int rotationspeed = 0;
        public int bulletDamage = 0;
        public int myHealth = 0;
        public int value;
        public GameObject explosion;
        public MeshRenderer renderer;
        public MeshRenderer otherrender;

        bool isDead = false;


        // Use this for initialization
        void Start() {

        }

        // Update is called once per frame
        void Update() {
            transform.Rotate(Vector3.forward * Time.deltaTime * rotationspeed);
            //if (myHealth < 1) StartCoroutine(Die());

        }

        private IEnumerator Die()
        {
            Debug.Log("Dead");
            GetComponent<Collider>().enabled = false;
            isDead = true;
            GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
             renderer.enabled = false;
            if (otherrender != null) otherrender.enabled = false;
            explosion.SetActive(true);
            GameManager.Instance.AddScore(value);
             yield return new WaitForSeconds(1.8f);
            Debug.Log("Destroyed");
            Destroy(gameObject);
        }


        public void ArcReactorHit(ArcReactorHitInfo hit)
        {
            Debug.Log("I'm Hit");
            if (!isDead)
            {
                StartCoroutine(Die());
            }
        }


        void OnTriggerEnter(Collider col)
        {
            if (!isDead)
            {
                if (col.tag == "PlayerBullet")
                {
                    myHealth--;
                    if (myHealth < 1) StartCoroutine(Die());
                }
                if (col.tag == "Gutter" || (col.tag == "Player" && !(gameObject.tag == "PlayerBullet"))) Destroy(gameObject);

                if (gameObject.tag == "PlayerBullet" && (col.tag == "EnemyBullet" || col.tag == "Enemy")) Destroy(gameObject);

                /*if (gameObject.tag == "PlayerBullet" && (col.tag == "EnemyBullet" || col.tag == "Enemy" ))
                {
                    Destroy(gameObject);
                }*/
            }
        }
    }
}