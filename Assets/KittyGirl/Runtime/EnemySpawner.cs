using System.Collections;
using ParallaxVisions.Models;
using UnityEngine;

namespace ParallaxVisions {
    public class EnemySpawner : MonoBehaviour {
        [SerializeField]
        Vector3 Pointer;
        Vector3 startLocation;
        int GroupCount = 0;
        int rowcount = 0;
        public GroupSO TestThing;

        [Header("Fly-In Settings")]
        [SerializeField] private float offscreenDistance = 15f;
        [SerializeField] private float flyInSpeed = 5f;

        void Start() {
            startLocation = gameObject.transform.position;
        }

        public void SpawnWave(Wave enemies) {
            ReInitPointer();
            GroupCount = 0;
            ProcessGroup(enemies.Group1);
            ProcessGroup(enemies.Group2);
            ProcessGroup(enemies.Group3);
        }

        public void ProcessGroup(Group _g) {
            rowcount = 0;
            ReInitPointer();
            Pointer.z += _g.Distance;
            ProcessRow(_g.Row1);
            ProcessRow(_g.Row2);
            ProcessRow(_g.Row3);
        }

        public void ProcessRow(Row _r) {
            switch (rowcount) {
                case 0:
                    Pointer.y = 11;
                    Pointer.x = -10;
                    if (_r.Spot1 != null) {
                        SpawnWithFlyIn(_r.Spot1, Pointer);
                    }
                    Pointer.x = 0.5f;
                    if (_r.Spot2 != null) {
                        SpawnWithFlyIn(_r.Spot2, Pointer);
                    }
                    Pointer.x = 11;
                    if (_r.Spot3 != null) {
                        SpawnWithFlyIn(_r.Spot3, Pointer);
                    }
                    rowcount++;
                    break;
                case 1:
                    Pointer.y = 6;
                    Pointer.x = -10;
                    if (_r.Spot1 != null) {
                        SpawnWithFlyIn(_r.Spot1, Pointer);
                    }
                    Pointer.x = 0.5f;
                    if (_r.Spot2 != null) {
                        SpawnWithFlyIn(_r.Spot2, Pointer);
                    }
                    Pointer.x = 11;
                    if (_r.Spot3 != null) {
                        SpawnWithFlyIn(_r.Spot3, Pointer);
                    }
                    rowcount++;
                    break;
                case 2:
                    Pointer.y = 1.8f;
                    Pointer.x = -10;
                    if (_r.Spot1 != null) {
                        SpawnWithFlyIn(_r.Spot1, Pointer);
                    }
                    Pointer.x = 0.5f;
                    if (_r.Spot2 != null) {
                        SpawnWithFlyIn(_r.Spot2, Pointer);
                    }
                    Pointer.x = 11;
                    if (_r.Spot3 != null) {
                        SpawnWithFlyIn(_r.Spot3, Pointer);
                    }
                    rowcount++;
                    break;
            }
        }

        private void SpawnWithFlyIn(GameObject enemyPrefab, Vector3 targetPosition) {
            // Randomly choose entry side: 0=Top, 1=Left, 2=Right
            int entrySide = Random.Range(0, 3);
            Vector3 spawnPosition = targetPosition;

            switch (entrySide) {
                case 0: // Top
                    spawnPosition.y += offscreenDistance;
                    break;
                case 1: // Left
                    spawnPosition.x -= offscreenDistance;
                    break;
                case 2: // Right
                    spawnPosition.x += offscreenDistance;
                    break;
            }

            GameObject enemy = GameObject.Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

            // Add the FlyIn component to handle movement
            EnemyFlyIn flyIn = enemy.AddComponent<EnemyFlyIn>();
            flyIn.Initialize(targetPosition, flyInSpeed);
        }

        private void ReInitPointer() {
            Pointer = startLocation;
        }
    }
}
