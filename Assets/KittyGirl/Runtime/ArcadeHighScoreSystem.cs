using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ParallaxVisions
{
    [DisallowMultipleComponent]
    public sealed class ArcadeHighScoreSystem : MonoBehaviour
    {
        [Serializable]
        private sealed class HighScoreEntry
        {
            public string initials;
            public int score;
        }

        [Serializable]
        private sealed class HighScoreData
        {
            public List<HighScoreEntry> entries = new List<HighScoreEntry>();
        }

        private enum DisplayState
        {
            Hidden,
            Initials,
            Attract
        }

        private const string SaveKey = "KittyGirl.HighScores.v1";
        private const float IdleSeconds = 15f;
        private const float AttractFireInterval = 0.25f;
        private const int MaximumScores = 10;

        private GameManager gameManager;
        private PlayerController player;
        private MenuScript titleMenu;
        private GameObject canvasObject;
        private GameObject panelObject;
        private CanvasGroup canvasGroup;
        private Coroutine overlayTransition;
        private TMP_Text headingText;
        private TMP_Text contentText;
        private TMP_Text helpText;
        private HighScoreData highScores;
        private DisplayState state;
        private readonly char[] initials = { 'A', 'A', 'A' };
        private int initialsIndex;
        private int pendingScore;
        private float titleIdleTime;
        private float menuInputBlockedUntil;
        private bool initialsInputArmed;
        private bool horizontalAxisReady = true;
        private bool verticalAxisReady = true;
        private float nextTargetSearch;
        private float nextAttractFire;
        private Transform botTarget;

        public bool CanStartFromMenu
        {
            get
            {
                return state == DisplayState.Hidden &&
                    Time.unscaledTime >= menuInputBlockedUntil;
            }
        }

        public void Initialize(GameManager manager, PlayerController playerController)
        {
            gameManager = manager;
            player = playerController;
            LoadScores();
            BuildInterface();
            HideOverlay();
        }

        public void RegisterTitleMenu(MenuScript menu)
        {
            if (menu == null || menu.gameObject.name != "Title")
                return;
            titleMenu = menu;
            titleIdleTime = 0f;
        }

        public void BeginInitialsEntry(int score)
        {
            pendingScore = score;
            initials[0] = initials[1] = initials[2] = 'A';
            initialsIndex = 0;
            initialsInputArmed = false;
            horizontalAxisReady = true;
            verticalAxisReady = true;
            state = DisplayState.Initials;
            ShowOverlay(false);
            string heading = IsNewHighScore(score) ? "NEW HIGH SCORE" : "NEW SCORE";
            headingText.text = heading + "  " + score.ToString("000000000");
            helpText.text = "W / S CHANGES LETTER  •  A / D MOVES CURSOR\nFIRE SELECTS LETTER";
            RefreshInitialsText();
        }

        private bool IsNewHighScore(int score)
        {
            return highScores.entries.Count == 0 || score > highScores.entries[0].score;
        }

        public void HideOverlay()
        {
            state = DisplayState.Hidden;
            if (canvasObject != null && canvasObject.activeSelf)
            {
                if (overlayTransition != null)
                    StopCoroutine(overlayTransition);
                overlayTransition = StartCoroutine(HideOverlayAnimated());
            }
        }

        public void ReturnToTitle()
        {
            if (gameManager != null)
                gameManager.ReturnToMainMenu();
            if (player != null)
                player.SetSimulatedInput(false, Vector2.zero, false);
            state = DisplayState.Hidden;
            HideOverlay();
            if (titleMenu != null)
                titleMenu.gameObject.SetActive(true);
            titleIdleTime = 0f;
            menuInputBlockedUntil = Time.unscaledTime + 0.3f;
        }

        private void Update()
        {
            if (titleMenu == null)
                DiscoverTitleMenu();

            if (state == DisplayState.Initials)
            {
                UpdateInitialsEntry();
                return;
            }

            if (state == DisplayState.Attract)
            {
                if (PlayerPressedAnything())
                {
                    ReturnToTitle();
                    return;
                }
                UpdateAttractBot();
                return;
            }

            if (titleMenu == null || !titleMenu.gameObject.activeInHierarchy ||
                gameManager == null || gameManager.GameISRunning)
            {
                titleIdleTime = 0f;
                return;
            }

            if (PlayerPressedAnything())
                titleIdleTime = 0f;
            else
                titleIdleTime += Time.unscaledDeltaTime;

            if (titleIdleTime >= IdleSeconds)
                BeginAttractMode();
        }

        private void DiscoverTitleMenu()
        {
            MenuScript[] menus = FindObjectsByType<MenuScript>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < menus.Length; i++)
            {
                if (menus[i] != null && menus[i].gameObject.name == "Title")
                {
                    RegisterTitleMenu(menus[i]);
                    return;
                }
            }
        }

        private void BeginAttractMode()
        {
            state = DisplayState.Attract;
            titleIdleTime = 0f;
            nextAttractFire = Time.unscaledTime;
            if (titleMenu != null)
                titleMenu.gameObject.SetActive(false);
            ShowOverlay(true);
            headingText.text = "HIGH SCORES";
            contentText.text = FormatLeaderboard();
            helpText.text = "ATTRACT MODE  •  PRESS ANY CONTROL TO RETURN";
            if (gameManager != null)
                gameManager.PlayAttractMode();
        }

        private void UpdateInitialsEntry()
        {
            float horizontal = ReadHorizontal();
            float vertical = ReadVertical();

            // Death can happen while fire is being held or rapidly pressed. Do
            // not let that input spill into initials entry: require the cabinet
            // controls to return to neutral before accepting a selection.
            if (!initialsInputArmed)
            {
                if (!IsFireHeld() && Mathf.Abs(horizontal) < 0.25f && Mathf.Abs(vertical) < 0.25f)
                    initialsInputArmed = true;
                return;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                initialsIndex = Mathf.Max(0, initialsIndex - 1);
                RefreshInitialsText();
                return;
            }

            if (horizontalAxisReady && Mathf.Abs(horizontal) > 0.55f)
            {
                initialsIndex = Mathf.Clamp(initialsIndex + (horizontal > 0f ? 1 : -1), 0, 2);
                horizontalAxisReady = false;
                RefreshInitialsText();
            }
            else if (Mathf.Abs(horizontal) < 0.25f)
            {
                horizontalAxisReady = true;
            }

            if (verticalAxisReady && Mathf.Abs(vertical) > 0.55f)
            {
                int change = vertical > 0f ? 1 : -1;
                int letter = initials[initialsIndex] - 'A';
                letter = (letter + change + 26) % 26;
                initials[initialsIndex] = (char)('A' + letter);
                verticalAxisReady = false;
                RefreshInitialsText();
            }
            else if (Mathf.Abs(vertical) < 0.25f)
            {
                verticalAxisReady = true;
            }

            bool confirm = Input.GetButtonDown("Fire") || Input.GetKeyDown(KeyCode.J) ||
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (!confirm)
                return;

            if (initialsIndex < 2)
            {
                initialsIndex++;
                RefreshInitialsText();
            }
            else
            {
                SubmitScore();
            }
        }

        private void SubmitScore()
        {
            highScores.entries.Add(new HighScoreEntry
            {
                initials = new string(initials),
                score = pendingScore
            });
            SortAndTrimScores();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(highScores));
            PlayerPrefs.Save();

            state = DisplayState.Hidden;
            HideOverlay();
            ReturnToTitle();
        }

        private void UpdateAttractBot()
        {
            if (player == null)
                return;

            if (Time.unscaledTime >= nextTargetSearch || botTarget == null)
            {
                nextTargetSearch = Time.unscaledTime + 0.2f;
                botTarget = FindClosestEnemy();
            }

            Vector2 movement;
            if (botTarget != null)
            {
                Vector3 difference = botTarget.position - player.transform.position;
                movement = new Vector2(difference.x, difference.y);
                if (Mathf.Abs(movement.x) < 0.45f) movement.x = 0f;
                if (Mathf.Abs(movement.y) < 0.45f) movement.y = 0f;
                movement = Vector2.ClampMagnitude(movement, 1f);
            }
            else
            {
                movement = new Vector2(
                    Mathf.Sin(Time.unscaledTime * 0.73f),
                    Mathf.Cos(Time.unscaledTime * 0.47f)) * 0.65f;
            }
            bool fire = Time.unscaledTime >= nextAttractFire;
            if (fire)
                nextAttractFire = Time.unscaledTime + AttractFireInterval;
            player.SetSimulatedInput(true, movement, fire);
        }

        private Transform FindClosestEnemy()
        {
            Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Transform closest = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] == null || enemies[i].isDead)
                    continue;
                float distance = (enemies[i].transform.position - player.transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closest = enemies[i].transform;
                }
            }
            return closest;
        }

        private void RefreshInitialsText()
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < initials.Length; i++)
            {
                if (i == initialsIndex)
                    result.Append("<color=#FF69D4>[ ").Append(initials[i]).Append(" ]</color>");
                else
                    result.Append("  ").Append(initials[i]).Append("  ");
                if (i < initials.Length - 1)
                    result.Append("   ");
            }
            contentText.text = "ENTER YOUR INITIALS\n\n" + result;
        }

        private string FormatLeaderboard()
        {
            if (highScores.entries.Count == 0)
                return "\nNO SCORES YET\n\nBE THE FIRST!";

            StringBuilder result = new StringBuilder();
            result.AppendLine();
            for (int i = 0; i < highScores.entries.Count; i++)
            {
                HighScoreEntry entry = highScores.entries[i];
                result.Append((i + 1).ToString("00"))
                    .Append("   ").Append(entry.initials.PadRight(3))
                    .Append("   ").Append(entry.score.ToString("000000000"));
                if (i < highScores.entries.Count - 1)
                    result.AppendLine();
            }
            return result.ToString();
        }

        private void LoadScores()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { highScores = JsonUtility.FromJson<HighScoreData>(json); }
                catch (Exception) { highScores = null; }
            }
            if (highScores == null || highScores.entries == null)
                highScores = new HighScoreData();
            SortAndTrimScores();
        }

        private void SortAndTrimScores()
        {
            highScores.entries.RemoveAll(entry => entry == null);
            highScores.entries.Sort((left, right) => right.score.CompareTo(left.score));
            if (highScores.entries.Count > MaximumScores)
                highScores.entries.RemoveRange(MaximumScores, highScores.entries.Count - MaximumScores);
        }

        private static bool PlayerPressedAnything()
        {
            return Input.anyKeyDown || Mathf.Abs(ReadHorizontal()) > 0.45f ||
                Mathf.Abs(ReadVertical()) > 0.45f;
        }

        private static float ReadHorizontal()
        {
            float value = Input.GetAxisRaw("Horizontal");
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) value = -1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) value = 1f;
            return value;
        }

        private static float ReadVertical()
        {
            float value = Input.GetAxisRaw("Vertical");
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) value = -1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) value = 1f;
            return value;
        }

        private static bool IsFireHeld()
        {
            return Input.GetButton("Fire") || Input.GetKey(KeyCode.J) ||
                Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
        }

        private void ShowOverlay(bool compact)
        {
            if (canvasObject == null)
                BuildInterface();
            if (overlayTransition != null)
                StopCoroutine(overlayTransition);
            canvasObject.SetActive(true);
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = compact ? new Vector2(0.62f, 0.12f) : new Vector2(0.22f, 0.2f);
            panel.anchorMax = compact ? new Vector2(0.97f, 0.91f) : new Vector2(0.78f, 0.8f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            overlayTransition = StartCoroutine(ShowOverlayAnimated());
        }

        private void BuildInterface()
        {
            if (canvasObject != null)
                return;

            canvasObject = new GameObject("Arcade High Scores", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;
            canvasGroup = canvasObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            panelObject.transform.SetParent(canvasObject.transform, false);
            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.025f, 0.015f, 0.08f, 0.9f);
            panelImage.raycastTarget = false;
            Outline panelOutline = panelObject.GetComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 0.2f, 0.75f, 0.9f);
            panelOutline.effectDistance = new Vector2(4f, -4f);

            TMP_FontAsset font = GameUIFont.Arcade;
            headingText = CreateText("Heading", panelObject.transform, font, 52, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.98f));
            headingText.color = new Color(1f, 0.35f, 0.82f);
            contentText = CreateText("Scores", panelObject.transform, font, 35, TextAlignmentOptions.Top,
                new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.8f));
            contentText.richText = true;
            helpText = CreateText("Help", panelObject.transform, font, 24, TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.18f));
            helpText.color = new Color(0.72f, 0.9f, 1f);
        }

        private IEnumerator ShowOverlayAnimated()
        {
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            canvasGroup.alpha = 0f;
            panel.localScale = Vector3.one * 0.92f;
            float elapsed = 0f;
            const float duration = 0.32f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = t * t * (3f - 2f * t);
                canvasGroup.alpha = smooth;
                panel.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, smooth);
                yield return null;
            }
            canvasGroup.alpha = 1f;
            panel.localScale = Vector3.one;
            overlayTransition = null;
        }

        private IEnumerator HideOverlayAnimated()
        {
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            float startAlpha = canvasGroup.alpha;
            Vector3 startScale = panel.localScale;
            float elapsed = 0f;
            const float duration = 0.2f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t * t);
                panel.localScale = Vector3.Lerp(startScale, Vector3.one * 0.97f, t);
                yield return null;
            }
            canvasObject.SetActive(false);
            overlayTransition = null;
        }

        private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, int size,
            TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16;
            text.fontSizeMax = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
