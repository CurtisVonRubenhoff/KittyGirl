using UnityEngine;

namespace ParallaxVisions
{
    /// <summary>Moves a newly spawned enemy from an off-screen entry point into formation.</summary>
    public sealed class EnemyFlyIn : MonoBehaviour
    {
        private Vector3 targetPosition;
        private Vector3 targetScale;
        private float initialDistance;
        private float speed;
        private float minimumHeight;
        private bool initialized;
        private Enemy enemyMovement;

        public void Initialize(Vector3 target, float flyInSpeed, float minimumY = 1.8f)
        {
            targetPosition = target;
            minimumHeight = minimumY;
            targetPosition.y = Mathf.Max(minimumHeight, targetPosition.y);
            speed = Mathf.Max(0.01f, flyInSpeed);
            targetScale = transform.localScale;
            initialDistance = Mathf.Max(0.01f, Vector3.Distance(transform.position, targetPosition));
            transform.localScale = targetScale * 0.04f;
            enemyMovement = GetComponent<Enemy>();
            if (enemyMovement != null)
                enemyMovement.enabled = false;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized)
                return;

            Vector3 current = transform.position;
            current.y = Mathf.Max(minimumHeight, current.y);
            transform.position = Vector3.MoveTowards(
                current,
                targetPosition,
                speed * Time.deltaTime);

            float remaining = Vector3.Distance(transform.position, targetPosition);
            float progress = 1f - Mathf.Clamp01(remaining / initialDistance);
            float scaleProgress = Mathf.SmoothStep(0.04f, 1f, progress);
            transform.localScale = targetScale * scaleProgress;

            if ((transform.position - targetPosition).sqrMagnitude <= 0.0001f)
            {
                transform.position = targetPosition;
                transform.localScale = targetScale;
                if (enemyMovement != null)
                    enemyMovement.enabled = true;
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            if (enemyMovement != null)
                enemyMovement.enabled = true;
        }
    }
}
