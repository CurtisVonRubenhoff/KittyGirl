using System.Collections.Generic;
using System.Collections;
using ParallaxVisions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KittyGirl.Survivors
{
    [DisallowMultipleComponent]
    public sealed class SurvivorRunUI : MonoBehaviour
    {
        private const float NavigationRepeatDelay = 0.22f;
        private const float HoldToSelectDuration = 0.85f;
        private const float LevelUpHoldDuration = 0.62f;
        private const float TransitionDuration = 0.28f;
        private static readonly Color SelectedColor = new Color(0.72f, 0.16f, 0.62f, 0.98f);
        private static readonly Color CardColor = new Color(0.12f, 0.06f, 0.18f, 0.96f);

        private readonly List<SurvivorUpgradeDefinition> choices = new List<SurvivorUpgradeDefinition>();
        private readonly List<Image> cardBackgrounds = new List<Image>();
        private readonly List<TMP_Text> cardLabels = new List<TMP_Text>();
        private readonly List<CanvasGroup> cardGroups = new List<CanvasGroup>();

        private SurvivorProgression progression;
        private ProceduralWaveDirector director;
        private GameManager gameManager;
        private PlayerController player;
        private MenuScript[] runBlockingMenus;
        private GameObject canvasObject;
        private GameObject hudPanel;
        private GameObject choiceOverlay;
        private CanvasGroup hudGroup;
        private CanvasGroup choiceGroup;
        private CanvasGroup levelUpGroup;
        private CanvasGroup instructionsGroup;
        private CanvasGroup progressTrackGroup;
        private TMP_Text levelUpText;
        private TMP_Text headingText;
        private TMP_Text hudText;
        private TMP_Text instructionsText;
        private Image holdProgress;
        private int selectedChoice;
        private float nextNavigationAt;
        private float holdStartedAt;
        private bool axisWasPressed;
        private bool navigationArmed;
        private bool confirmationArmed;
        private bool isHolding;
        private bool wasShowingRunUi;
        private bool choicesInteractive;
        private bool uiTornDown;
        private Coroutine choiceTransition;
        private Coroutine hudTransition;

        public void Initialize(SurvivorProgression runProgression, ProceduralWaveDirector waveDirector)
        {
            progression = runProgression;
            director = waveDirector;
            gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindFirstObjectByType<GameManager>();
            player = FindFirstObjectByType<PlayerController>();
            runBlockingMenus = FindObjectsByType<MenuScript>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            progression.ChoicesOffered += ShowChoices;
            progression.ChoiceClosed += HideChoices;
            BuildCanvas();
        }

        private void OnDestroy()
        {
            TearDownUi();
            if (progression != null)
            {
                progression.ChoicesOffered -= ShowChoices;
                progression.ChoiceClosed -= HideChoices;
            }
        }

        private void OnDisable()
        {
            // Deactivate the complete overlay synchronously. This is important
            // when the editor stops on a paused upgrade frame, where Update no
            // longer gets a chance to hide the UI.
            TearDownUi();
        }

        private void TearDownUi()
        {
            if (uiTornDown)
                return;

            uiTornDown = true;
            StopAllCoroutines();
            choiceTransition = null;
            hudTransition = null;
            choices.Clear();
            choicesInteractive = false;
            isHolding = false;

            if (choiceOverlay != null)
                choiceOverlay.SetActive(false);
            if (hudPanel != null)
                hudPanel.SetActive(false);
            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        private void Update()
        {
            bool showRunUi = ShouldShowRunUi();
            bool becameVisible = showRunUi && !wasShowingRunUi;
            if (!showRunUi && wasShowingRunUi)
                HideChoices();
            wasShowingRunUi = showRunUi;
            if (canvasObject != null && canvasObject.activeSelf != showRunUi)
                canvasObject.SetActive(showRunUi);
            if (hudPanel != null && hudPanel.activeSelf != showRunUi)
                hudPanel.SetActive(showRunUi);
            if (becameVisible)
                AnimateHudIn();
            if (!showRunUi)
            {
                CancelHold();
                return;
            }

            UpdateHud();
            if (choices.Count == 0)
                return;

            UpdateCardMotion();
            if (!choicesInteractive)
                return;

            int numberedChoice = ReadNumberedChoice();
            if (numberedChoice >= 0)
            {
                Choose(numberedChoice);
                return;
            }

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) horizontal = -1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) horizontal = 1f;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) vertical = 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) vertical = -1f;

            // Horizontal follows the card layout. Up/down are supported too for
            // cabinets whose joystick is wired as a vertical menu navigator.
            float navigation = Mathf.Abs(horizontal) >= Mathf.Abs(vertical) ? horizontal : -vertical;

            bool axisPressed = Mathf.Abs(navigation) > 0.5f;
            if (!navigationArmed)
            {
                navigationArmed = !axisPressed;
                axisWasPressed = axisPressed;
            }
            else if (axisPressed && (!axisWasPressed || Time.unscaledTime >= nextNavigationAt))
            {
                selectedChoice = Wrap(selectedChoice + (navigation > 0f ? 1 : -1), choices.Count);
                nextNavigationAt = Time.unscaledTime + NavigationRepeatDelay;
                CancelHold();
                RefreshCards();
            }
            axisWasPressed = axisPressed;

            UpdateHoldToSelect();
        }

        private void BuildCanvas()
        {
            TMP_FontAsset font = GameUIFont.Arcade;
            canvasObject = new GameObject("Survivor Overlay Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // Keep every runtime-created UI object under the runtime canvas so
            // destroying the Survivor Run host cannot orphan HUD elements on a
            // scene-owned canvas.
            hudPanel = CreateImage("Survivor Stats HUD", canvasObject.transform, new Color(0f, 0f, 0f, 0.72f));
            hudGroup = hudPanel.AddComponent<CanvasGroup>();
            SetRect(hudPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero,
                new Vector2(25f, 25f), new Vector2(510f, 82f), Vector2.zero);
            hudText = CreateText("HUD Text", hudPanel.transform, font, 26, TextAlignmentOptions.MidlineLeft);
            Stretch(hudText.rectTransform, 16f);

            choiceOverlay = CreateImage("Upgrade Choices", canvasObject.transform, new Color(0f, 0f, 0f, 0.88f));
            choiceGroup = choiceOverlay.AddComponent<CanvasGroup>();
            Stretch(choiceOverlay.GetComponent<RectTransform>(), 0f);

            headingText = CreateText("Heading", choiceOverlay.transform, font, 54, TextAlignmentOptions.Center);
            SetRect(headingText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -105f), new Vector2(1000f, 80f), new Vector2(0.5f, 0.5f));
            headingText.text = "CHOOSE AN UPGRADE";

            levelUpText = CreateText("Level Up Reveal", choiceOverlay.transform, font, 112, TextAlignmentOptions.Center);
            SetRect(levelUpText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1300f, 180f), new Vector2(0.5f, 0.5f));
            levelUpText.text = "LEVEL UP!";
            levelUpText.color = new Color(1f, 0.32f, 0.82f);
            levelUpText.outlineColor = new Color(0.12f, 0.02f, 0.18f, 1f);
            levelUpText.outlineWidth = 0.22f;
            levelUpGroup = levelUpText.gameObject.AddComponent<CanvasGroup>();

            instructionsText = CreateText("Instructions", choiceOverlay.transform, font, 28, TextAlignmentOptions.Center);
            instructionsGroup = instructionsText.gameObject.AddComponent<CanvasGroup>();
            SetRect(instructionsText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 92f), new Vector2(1300f, 55f), new Vector2(0.5f, 0.5f));
            instructionsText.text = "MOVE TO CHOOSE     HOLD FIRE TO SELECT";

            GameObject progressTrack = CreateImage("Hold Track", choiceOverlay.transform, new Color(1f, 1f, 1f, 0.22f));
            progressTrackGroup = progressTrack.AddComponent<CanvasGroup>();
            SetRect(progressTrack.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 52f), new Vector2(620f, 22f), new Vector2(0.5f, 0.5f));
            GameObject progressFill = CreateImage("Hold Progress", progressTrack.transform, new Color(1f, 0.22f, 0.78f, 1f));
            Stretch(progressFill.GetComponent<RectTransform>(), 2f);
            holdProgress = progressFill.GetComponent<Image>();
            holdProgress.type = Image.Type.Filled;
            holdProgress.fillMethod = Image.FillMethod.Horizontal;
            holdProgress.fillOrigin = 0;
            holdProgress.fillAmount = 0f;

            for (int i = 0; i < 4; i++)
            {
                GameObject card = CreateImage("Upgrade " + (i + 1), choiceOverlay.transform, CardColor);
                cardBackgrounds.Add(card.GetComponent<Image>());
                cardGroups.Add(card.AddComponent<CanvasGroup>());
                TMP_Text label = CreateText("Upgrade Text", card.transform, font, 30, TextAlignmentOptions.Center);
                label.enableAutoSizing = true;
                label.fontSizeMin = 19;
                label.fontSizeMax = 30;
                Stretch(label.rectTransform, 24f);
                cardLabels.Add(label);
            }

            choiceOverlay.SetActive(false);
            bool showRunUi = ShouldShowRunUi();
            canvasObject.SetActive(showRunUi);
            hudPanel.SetActive(showRunUi);
            UpdateHud();
        }

        private bool ShouldShowRunUi()
        {
            if (gameManager == null || !gameManager.GameISRunning || gameManager.IsAttractMode)
                return false;

            // The legacy title and retry screens each own a MenuScript. Their
            // visible state is authoritative: stale run data must never draw or
            // accept upgrade input over either menu, even if the legacy running
            // flag has not caught up yet.
            if (runBlockingMenus != null)
            {
                for (int i = 0; i < runBlockingMenus.Length; i++)
                {
                    MenuScript menu = runBlockingMenus[i];
                    if (menu != null && menu.gameObject.activeInHierarchy)
                        return false;
                }
            }

            return true;
        }

        private void UpdateHud()
        {
            if (hudPanel == null || hudText == null || progression == null || director == null)
                return;

            string waveText = director.IsResting
                ? "WAVE " + director.CurrentWave + " CLEAR"
                : "WAVE " + director.CurrentWave + "   ENEMIES " + director.AliveCount;
            hudText.text = "LEVEL " + progression.Level + "   XP " + progression.Experience + "/" +
                progression.ExperienceToNextLevel + "\n" + waveText +
                (player != null && player.SpecialWeaponTimeRemaining > 0f
                    ? "   ARC BEAM " + Mathf.CeilToInt(player.SpecialWeaponTimeRemaining) + "s"
                    : string.Empty);
        }

        private void ShowChoices(IReadOnlyList<SurvivorUpgradeDefinition> offered)
        {
            if (gameManager != null && gameManager.IsAttractMode)
            {
                HideChoices();
                return;
            }

            choices.Clear();
            for (int i = 0; i < offered.Count; i++)
                choices.Add(offered[i]);

            selectedChoice = 0;
            axisWasPressed = false;
            navigationArmed = false;
            confirmationArmed = false;
            isHolding = false;
            choicesInteractive = false;
            if (holdProgress != null)
                holdProgress.fillAmount = 0f;
            nextNavigationAt = Time.unscaledTime + NavigationRepeatDelay;
            choiceOverlay.SetActive(choices.Count > 0);
            RefreshCards();
            if (choiceTransition != null)
                StopCoroutine(choiceTransition);
            if (choices.Count > 0)
                choiceTransition = StartCoroutine(PresentChoices());
        }

        private void HideChoices()
        {
            choices.Clear();
            choicesInteractive = false;
            CancelHold();
            if (choiceTransition != null)
                StopCoroutine(choiceTransition);
            if (choiceOverlay != null && choiceOverlay.activeSelf)
                choiceTransition = StartCoroutine(HideChoicesAnimated());
        }

        private void UpdateHoldToSelect()
        {
            bool fireHeld = Input.GetButton("Fire") ||
                Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);

            // A shot that was already held when the upgrade appeared must be
            // released before it can start a selection hold.
            if (!confirmationArmed)
            {
                if (!fireHeld)
                    confirmationArmed = true;
                return;
            }

            if (!fireHeld)
            {
                CancelHold();
                return;
            }

            if (!isHolding)
            {
                isHolding = true;
                holdStartedAt = Time.unscaledTime;
            }

            float progress = Mathf.Clamp01((Time.unscaledTime - holdStartedAt) / HoldToSelectDuration);
            if (holdProgress != null)
                holdProgress.fillAmount = progress;
            if (instructionsText != null)
                instructionsText.text = "KEEP HOLDING FIRE  " + Mathf.RoundToInt(progress * 100f) + "%";

            if (progress >= 1f)
                Choose(selectedChoice);
        }

        private void CancelHold()
        {
            isHolding = false;
            holdStartedAt = 0f;
            if (holdProgress != null)
                holdProgress.fillAmount = 0f;
            if (instructionsText != null)
                instructionsText.text = "MOVE TO CHOOSE     HOLD FIRE TO SELECT";
        }

        private void RefreshCards()
        {
            const float cardWidth = 420f;
            const float cardHeight = 430f;
            const float gap = 32f;
            float totalWidth = choices.Count * cardWidth + Mathf.Max(0, choices.Count - 1) * gap;

            for (int i = 0; i < cardBackgrounds.Count; i++)
            {
                bool visible = i < choices.Count;
                cardBackgrounds[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;

                RectTransform rect = cardBackgrounds[i].rectTransform;
                SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-totalWidth * 0.5f + cardWidth * 0.5f + i * (cardWidth + gap), -5f),
                    new Vector2(cardWidth, cardHeight), new Vector2(0.5f, 0.5f));

                SurvivorUpgradeDefinition choice = choices[i];
                int nextLevel = progression.Stats.GetLevel(choice.Id) + 1;
                cardLabels[i].text = (i == selectedChoice ? ">  " : "") + choice.Name.ToUpperInvariant() +
                    (i == selectedChoice ? "  <" : "") + "\n\n" + choice.Description +
                    "\n\nLEVEL " + nextLevel + "/" + choice.MaxLevel;
            }
        }

        private IEnumerator PresentChoices()
        {
            choiceGroup.alpha = 0f;
            levelUpGroup.alpha = 0f;
            levelUpText.rectTransform.localScale = Vector3.one * 0.55f;
            headingText.alpha = 0f;
            instructionsGroup.alpha = 0f;
            progressTrackGroup.alpha = 0f;
            headingText.rectTransform.anchoredPosition = new Vector2(0f, -128f);
            for (int i = 0; i < cardGroups.Count; i++)
            {
                cardGroups[i].alpha = 0f;
                cardGroups[i].transform.localScale = Vector3.one * 0.82f;
            }

            float elapsed = 0f;
            while (elapsed < TransitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseOutBack(Mathf.Clamp01(elapsed / TransitionDuration));
                choiceGroup.alpha = Mathf.Clamp01(elapsed / TransitionDuration);
                levelUpGroup.alpha = Mathf.Clamp01(elapsed / (TransitionDuration * 0.65f));
                levelUpText.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.55f, 1f, t);
                yield return null;
            }

            choiceGroup.alpha = 1f;
            levelUpGroup.alpha = 1f;
            levelUpText.rectTransform.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(LevelUpHoldDuration);

            elapsed = 0f;
            while (elapsed < TransitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Smooth(Mathf.Clamp01(elapsed / TransitionDuration));
                levelUpGroup.alpha = 1f - t;
                levelUpText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.16f, t);
                headingText.alpha = t;
                instructionsGroup.alpha = t;
                progressTrackGroup.alpha = t;
                headingText.rectTransform.anchoredPosition = Vector2.Lerp(
                    new Vector2(0f, -128f), new Vector2(0f, -105f), t);

                for (int i = 0; i < choices.Count; i++)
                {
                    float cardT = EaseOutBack(Mathf.Clamp01((t - i * 0.1f) / 0.7f));
                    cardGroups[i].alpha = Mathf.Clamp01(cardT);
                    cardGroups[i].transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.82f, 1f, cardT);
                }
                yield return null;
            }

            levelUpGroup.alpha = 0f;
            headingText.alpha = 1f;
            instructionsGroup.alpha = 1f;
            progressTrackGroup.alpha = 1f;
            for (int i = 0; i < choices.Count; i++)
            {
                cardGroups[i].alpha = 1f;
                cardGroups[i].transform.localScale = Vector3.one;
            }
            choicesInteractive = true;
            choiceTransition = null;
        }

        private IEnumerator HideChoicesAnimated()
        {
            float startAlpha = choiceGroup.alpha;
            float elapsed = 0f;
            while (elapsed < TransitionDuration * 0.7f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Smooth(Mathf.Clamp01(elapsed / (TransitionDuration * 0.7f)));
                choiceGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }
            choiceGroup.alpha = 0f;
            choiceOverlay.SetActive(false);
            choiceTransition = null;
        }

        private void UpdateCardMotion()
        {
            float t = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            for (int i = 0; i < choices.Count && i < cardBackgrounds.Count; i++)
            {
                bool selected = i == selectedChoice;
                cardBackgrounds[i].color = Color.Lerp(
                    cardBackgrounds[i].color, selected ? SelectedColor : CardColor, t);
                if (choicesInteractive)
                {
                    float scale = selected ? 1.045f : 1f;
                    cardBackgrounds[i].transform.localScale = Vector3.Lerp(
                        cardBackgrounds[i].transform.localScale, Vector3.one * scale, t);
                }
            }
        }

        private void AnimateHudIn()
        {
            if (hudGroup == null)
                return;
            if (hudTransition != null)
                StopCoroutine(hudTransition);
            hudTransition = StartCoroutine(AnimateHudInRoutine());
        }

        private IEnumerator AnimateHudInRoutine()
        {
            RectTransform rect = hudPanel.GetComponent<RectTransform>();
            Vector2 destination = new Vector2(25f, 25f);
            Vector2 origin = destination + new Vector2(-60f, 0f);
            hudGroup.alpha = 0f;
            rect.anchoredPosition = origin;
            float elapsed = 0f;
            while (elapsed < 0.38f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Smooth(Mathf.Clamp01(elapsed / 0.38f));
                hudGroup.alpha = t;
                rect.anchoredPosition = Vector2.Lerp(origin, destination, t);
                yield return null;
            }
            hudGroup.alpha = 1f;
            rect.anchoredPosition = destination;
            hudTransition = null;
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float EaseOutBack(float value)
        {
            const float overshoot = 1.70158f;
            float shifted = value - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted +
                overshoot * shifted * shifted;
        }

        private void Choose(int index)
        {
            if (index < 0 || index >= choices.Count)
                return;
            progression.Choose(index);
        }

        private static int ReadNumberedChoice()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) return 3;
            return -1;
        }

        private static int Wrap(int value, int count)
        {
            if (count <= 0)
                return 0;
            return (value % count + count) % count;
        }

        private static GameObject CreateImage(string name, Transform parent, Color color)
        {
            GameObject result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            result.transform.SetParent(parent, false);
            Image image = result.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return result;
        }

        private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, int size,
            TextAlignmentOptions alignment)
        {
            GameObject result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            result.transform.SetParent(parent, false);
            TextMeshProUGUI text = result.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
