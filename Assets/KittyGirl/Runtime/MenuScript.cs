using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using ParallaxVisions.Models;

namespace ParallaxVisions {

    public class MenuScript : MonoBehaviour {

        private const string StartPrompt = "PRESS SPACE / FIRE TO START";

        [SerializeField]
        GameManager GM;

        [SerializeField]
        List<Sprite> audioSprites = new List<Sprite>();

        [SerializeField]
        Image AudioToggle;

        private CanvasGroup transitionGroup;
        private RectTransform transitionRect;
        private Vector3 restingScale = Vector3.one;
        private Coroutine transitionRoutine;
        private bool startingGame;

        void Awake() {
            EnsureTransitionVisuals();
            RepairStartPrompt();
            if (GM == null)
                GM = GameManager.Instance != null ? GameManager.Instance : FindFirstObjectByType<GameManager>();
            if (GM != null && gameObject.name == "Title")
                GM.RegisterTitleMenu(this);
        }

        private void OnEnable()
        {
            EnsureTransitionVisuals();
            startingGame = false;
            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(AnimateMenuIn());
        }

        // Update is called once per frame
        void Update() {
            if (GM != null && !GM.CanStartFromMenu())
                return;
            if (Input.GetButtonDown("Fire") ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                StartGame();
            }
        }

        private void RepairStartPrompt()
        {
            // This scene came from an older Unity UI layout. Its GameMenu ended
            // up hundreds of pixels outside the title panel after migration,
            // leaving the start button active but invisible.
            Transform menu = transform.Find("GameMenu");
            if (menu == null)
                return;

            RectTransform menuRect = menu as RectTransform;
            if (menuRect != null)
                menuRect.anchoredPosition = new Vector2(0f, -300f);

            Transform play = menu.Find("Play");
            if (play == null)
                return;

            play.gameObject.SetActive(true);
            TMP_Text prompt = play.GetComponentInChildren<TMP_Text>(true);
            if (prompt == null)
                return;

            prompt.text = StartPrompt;
            prompt.fontSize = 34;
            prompt.enableAutoSizing = true;
            prompt.fontSizeMin = 18;
            prompt.fontSizeMax = 34;
            prompt.overflowMode = TextOverflowModes.Overflow;
            prompt.color = Color.white;
            prompt.enabled = true;
        }

        public void StartGame()
        {
            if (GM == null)
                GM = GameManager.Instance != null ? GameManager.Instance : FindFirstObjectByType<GameManager>();

            if (GM == null)
            {
                Debug.LogError("The menu cannot start the game because no GameManager was found.", this);
                return;
            }

            if (!GM.CanStartFromMenu())
                return;

            if (!startingGame)
                transitionRoutine = StartCoroutine(AnimateMenuOutAndStart());
        }

        private void EnsureTransitionVisuals()
        {
            if (transitionGroup == null)
            {
                transitionGroup = GetComponent<CanvasGroup>();
                if (transitionGroup == null)
                    transitionGroup = gameObject.AddComponent<CanvasGroup>();
            }
            if (transitionRect == null)
            {
                transitionRect = transform as RectTransform;
                if (transitionRect != null)
                    restingScale = transitionRect.localScale;
            }
        }

        private IEnumerator AnimateMenuIn()
        {
            transitionGroup.alpha = 0f;
            if (transitionRect != null)
                transitionRect.localScale = restingScale * 0.96f;
            float elapsed = 0f;
            const float duration = 0.34f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                transitionGroup.alpha = t;
                if (transitionRect != null)
                    transitionRect.localScale = Vector3.Lerp(restingScale * 0.96f, restingScale, t);
                yield return null;
            }
            transitionGroup.alpha = 1f;
            if (transitionRect != null)
                transitionRect.localScale = restingScale;
            transitionRoutine = null;
        }

        private IEnumerator AnimateMenuOutAndStart()
        {
            startingGame = true;
            float startAlpha = transitionGroup.alpha;
            Vector3 startScale = transitionRect != null ? transitionRect.localScale : Vector3.one;
            float elapsed = 0f;
            const float duration = 0.22f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transitionGroup.alpha = Mathf.Lerp(startAlpha, 0f, t * t);
                if (transitionRect != null)
                    transitionRect.localScale = Vector3.Lerp(startScale, restingScale * 1.025f, t);
                yield return null;
            }
            GM.PlayGame();
            gameObject.SetActive(false);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        public void ToggleSound()
        {
            if (GM == null)
                GM = GameManager.Instance != null ? GameManager.Instance : FindFirstObjectByType<GameManager>();
            if (GM == null)
                return;

            bool muted = GM.ToggleAudioMuted();
            int spriteIndex = muted ? 0 : 1;
            if (AudioToggle != null && spriteIndex < audioSprites.Count)
                AudioToggle.sprite = audioSprites[spriteIndex];
        }

    }
}
