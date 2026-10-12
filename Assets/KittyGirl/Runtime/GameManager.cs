using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using ParallaxVisions.Models;
using VacuumShaders.CurvedWorld;
using System;
using Com.LuisPedroFonseca.ProCamera2D;

namespace ParallaxVisions
{
    [RequireComponent(typeof(MusicController), typeof(SoundFxManager))]
    public class GameManager : MonoBehaviour
    {
        #region "Variables"
        public static GameManager Instance;
        public EnemySpawner ES;

        public GameObject RetryMenu;

        public int CurrentWave = 0;
        public int EnemiesShot = 0;
        public int ThisWaveTotal = 0;
        public int CurrentLevel = 0;

        public int PlayerLives = 3;

        public float PlayerScore = 0;

        public float gameTime = 99;
        public float thisWaveTime = 0;

        [SerializeField, Min(0f)]
        private float hitTimePenalty = 5f;

        Runner_SceneManager RSM;
        [SerializeField]
        PlayerController PC;

        [SerializeField]
        KaleidoscopeEffect kal;

        [SerializeField]
        private List<SpriteRenderer> UI_KalSprites = new List<SpriteRenderer>();

        private int UI_CurrentKalIndex = 0;

        public bool GameISRunning = false;
        public bool IsAttractMode { get; private set; }
        private bool proceduralWavesEnabled = false;
        private bool endOfRunHandled;
        private ArcadeHighScoreSystem arcadeSystem;
        private Coroutine gameOverTransition;
        private const float GameOverTransitionDuration = 3f;

        public event Action ProceduralRunStarted;

        private MusicController musicController;
        private SoundFxManager soundFxManager;
        private bool audioMuted;

        public MusicController Music => musicController;
        public SoundFxManager SoundFx => soundFxManager;


        [SerializeField]
        public List<Wave> waveList = new List<Wave>();



        [SerializeField]
        private TMP_Text UI_ScoreText;

        public Canvas MainCanvas
        {
            get { return UI_ScoreText != null ? UI_ScoreText.GetComponentInParent<Canvas>() : null; }
        }

        [SerializeField]
        private List<string> BonusTextList = new List<string>();

        [SerializeField]
        private TMP_Text UI_TimeBonusText;

        [SerializeField]
        private List<string> ScoreList= new List<string>();

        [SerializeField]
        private TMP_Text UI_PointRewardText;

        [SerializeField]
        private List<string> TimeList = new List<string>();

        [SerializeField]
        private TMP_Text UI_TimeRewardText;


        [SerializeField]
        private TMP_Text UI_TimeLeft;

        [SerializeField]
        private List<Image> UI_LivesRemaining = new List<Image>();

        [SerializeField]
        private TMP_Text UI_ScreenText;

        private CanvasGroup screenTextGroup;
        private Coroutine screenTextTransition;
        private Vector3 screenTextScale = Vector3.one;

        bool fadingIn = false;
        bool fadingOut = false;
        private Coroutine waveThemeTransition;
        private const float WaveThemeFadeDuration = 1.75f;


        #endregion

        // Use this for initialization
        void Awake()
        {
            if (GameManager.Instance == null) Instance = this;
            musicController = GetComponent<MusicController>();
            if (musicController == null)
                musicController = gameObject.AddComponent<MusicController>();
            musicController.Configure(kal);
            musicController.PlayMenu();

            soundFxManager = GetComponent<SoundFxManager>();
            if (soundFxManager == null)
                soundFxManager = gameObject.AddComponent<SoundFxManager>();
            if (UI_ScreenText != null)
            {
                screenTextGroup = UI_ScreenText.GetComponent<CanvasGroup>();
                if (screenTextGroup == null)
                    screenTextGroup = UI_ScreenText.gameObject.AddComponent<CanvasGroup>();
                screenTextScale = UI_ScreenText.rectTransform.localScale;
            }
            RSM = GetComponent<Runner_SceneManager>();
            arcadeSystem = GetComponent<ArcadeHighScoreSystem>();
            if (arcadeSystem == null)
                arcadeSystem = gameObject.AddComponent<ArcadeHighScoreSystem>();
            arcadeSystem.Initialize(this, PC);
            SetTimerVisible(false);
            //StartCoroutine (SetupGame ());

        }

        // Update is called once per frame
        void Update()
        {
            if (GameISRunning) {
                if (gameTime <= 0)
                {
                    LoseGame();
                    return;
                }
                kal.angle += .25f;
                //AddScore(.5f);
                gameTime -= Time.deltaTime;
                thisWaveTime += Time.deltaTime;
                UpdateTimerDisplay();
                if (!proceduralWavesEnabled && EnemiesShot == ThisWaveTotal)
                {
                    StartCoroutine(EndWave());

                }
                if (PlayerLives < 1)
                {
                    LoseGame();
                }
            }


            if (fadingIn) {
                var cur = UI_KalSprites[UI_CurrentKalIndex].color;

                UI_KalSprites[UI_CurrentKalIndex].color = Color.Lerp(cur, new Color(cur.r, cur.g, cur.b, .39f), Time.deltaTime);
            }
            if (fadingOut)
            {
                var cur = UI_KalSprites[UI_CurrentKalIndex].color;
                UI_KalSprites[UI_CurrentKalIndex].color = Color.Lerp(cur, new Color(cur.r, cur.g, cur.b, 0), Time.deltaTime);
            }

        }


        private void DestroyShit()
        {
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            var eb = GameObject.FindGameObjectsWithTag("EnemyBullet");
            var pb = GameObject.FindGameObjectsWithTag("PlayerBullet");

            if (enemies.Length > 0)
            {
                foreach (GameObject enemy in enemies)
                {
                    Destroy(enemy);
                }
            }
            if (eb.Length > 0)
            {
                foreach (GameObject b in eb)
                {
                    Destroy(b);
                }
            }
            if (pb.Length > 0)
            {
                foreach (GameObject b in pb)
                {
                    Destroy(b);
                }
            }
        }

        //<summary>
        //These are functions that control the game itself. When the play button is pressed, this is what will start the game up
        //
        //</summary
        #region "Game Control"

        public IEnumerator SetupNormal()
        {
            SetupGame(true);
            yield return new WaitForSeconds(2);
            StartGame();
        }

        private void SetupGame(bool switchToLevelMusic)
        {
            if (gameOverTransition != null)
            {
                StopCoroutine(gameOverTransition);
                gameOverTransition = null;
            }
            endOfRunHandled = false;
            EnableHearts();
            if (switchToLevelMusic)
            {
                musicController.PlayGameplay();
            }
            DestroyShit();
            PC.ResetToDefaultState();
            PC.canMove = true;
            PC.isInv = false;
            SetScreenMessage("READY?");
            gameTime = 99;
            SetTimerVisible(true);
            UpdateTimerDisplay();
            UI_LivesRemaining[0].transform.parent.gameObject.SetActive(true);
            UI_ScoreText.text = "000000000";
            UI_ScoreText.gameObject.SetActive(true);
            PlayerScore = 0;
            PlayerLives = 9;
            CurrentLevel = 0;
            CurrentWave = 0;
        }

        void EnableHearts()
        {
            foreach (Image h in UI_LivesRemaining)
            {
                h.enabled = true;
            }
        }

        void StartGame()
        {
            if (proceduralWavesEnabled)
            {
                StartCoroutine(StartWave());
            }
            else
            {
                SetupWave(CurrentWave);
            }
        }

        public void EnableProceduralWaves()
        {
            proceduralWavesEnabled = true;
            if (ES != null)
                ES.enabled = false;
        }

        public void SetProceduralWaveNumber(int wave)
        {
            CurrentWave = Mathf.Max(0, wave - 1);
        }

        public void SetWaveTheme(int wave)
        {
            Color[] palette =
            {
                new Color(0.83f, 0.18f, 0.72f, 1f),
                new Color(0.18f, 0.55f, 0.95f, 1f),
                new Color(0.12f, 0.82f, 0.68f, 1f),
                new Color(0.96f, 0.42f, 0.18f, 1f),
                new Color(0.55f, 0.28f, 0.96f, 1f)
            };

            int themeIndex = Mathf.Abs(wave - 1) % palette.Length;
            Color theme = palette[themeIndex];
            Color darkTheme = Color.Lerp(Color.black, theme, 0.24f);

            FitWaveBackgroundsToTheirCamera();

            if (waveThemeTransition != null)
                StopCoroutine(waveThemeTransition);
            waveThemeTransition = StartCoroutine(FadeWaveTheme(themeIndex, theme, darkTheme));

            if (kal != null)
            {
                kal.effectType = (KaleidoscopeEffect.EffectType)(themeIndex % 3);
                kal.number = 12 + themeIndex * 2;
                kal.scale = 0.62f + themeIndex * 0.07f;
                kal.angle = themeIndex * 19f;
            }
        }

        private void FitWaveBackgroundsToTheirCamera()
        {
            for (int i = 0; i < UI_KalSprites.Count; i++)
            {
                SpriteRenderer background = UI_KalSprites[i];
                if (background == null || background.sprite == null)
                    continue;

                Camera backgroundCamera = background.GetComponentInParent<Camera>();
                if (backgroundCamera == null)
                    continue;

                float viewHeight;
                if (backgroundCamera.orthographic)
                {
                    viewHeight = backgroundCamera.orthographicSize * 2f;
                }
                else
                {
                    float distance = Mathf.Abs(Vector3.Dot(
                        background.transform.position - backgroundCamera.transform.position,
                        backgroundCamera.transform.forward));
                    viewHeight = 2f * distance * Mathf.Tan(backgroundCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                }

                float viewWidth = viewHeight * backgroundCamera.aspect;
                Vector2 spriteSize = background.sprite.bounds.size;
                if (spriteSize.x <= 0f || spriteSize.y <= 0f)
                    continue;

                // Slight overscan prevents thin borders from appearing at unusual
                // cabinet resolutions or while the kaleidoscope effect is moving.
                const float overscan = 1.12f;
                float worldScale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y) * overscan;
                Vector3 parentScale = background.transform.parent != null
                    ? background.transform.parent.lossyScale
                    : Vector3.one;
                Vector3 localScale = background.transform.localScale;
                localScale.x = Mathf.Sign(localScale.x == 0f ? 1f : localScale.x) *
                    worldScale / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f);
                localScale.y = Mathf.Sign(localScale.y == 0f ? 1f : localScale.y) *
                    worldScale / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f);
                background.transform.localScale = localScale;
            }
        }

        private IEnumerator FadeWaveTheme(int themeIndex, Color theme, Color darkTheme)
        {
            int targetSpriteIndex = UI_KalSprites.Count > 0
                ? themeIndex % UI_KalSprites.Count
                : 0;
            Color[] startingSpriteColors = new Color[UI_KalSprites.Count];
            Color[] targetSpriteColors = new Color[UI_KalSprites.Count];

            for (int i = 0; i < UI_KalSprites.Count; i++)
            {
                if (UI_KalSprites[i] == null)
                    continue;
                startingSpriteColors[i] = UI_KalSprites[i].color;
                Color target = Color.Lerp(theme, Color.white, i * 0.055f);
                target.a = i == targetSpriteIndex ? 0.39f : 0f;
                targetSpriteColors[i] = target;
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Color[] startingCameraColors = new Color[cameras.Length];
            for (int i = 0; i < cameras.Length; i++)
                startingCameraColors[i] = cameras[i].backgroundColor;
            Color startingAmbient = RenderSettings.ambientLight;
            Color targetAmbient = Color.Lerp(Color.white, theme, 0.3f);

            float elapsed = 0f;
            while (elapsed < WaveThemeFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / WaveThemeFadeDuration));

                for (int i = 0; i < UI_KalSprites.Count; i++)
                {
                    if (UI_KalSprites[i] != null)
                        UI_KalSprites[i].color = Color.Lerp(startingSpriteColors[i], targetSpriteColors[i], t);
                }
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i] != null)
                        cameras[i].backgroundColor = Color.Lerp(startingCameraColors[i], darkTheme, t);
                }
                RenderSettings.ambientLight = Color.Lerp(startingAmbient, targetAmbient, t);
                yield return null;
            }

            UI_CurrentKalIndex = targetSpriteIndex;
            for (int i = 0; i < UI_KalSprites.Count; i++)
            {
                if (UI_KalSprites[i] != null)
                    UI_KalSprites[i].color = targetSpriteColors[i];
            }
            waveThemeTransition = null;
        }

        public void PlayGame()
        {
            IsAttractMode = false;
            soundFxManager.SetAttractMode(false);
            soundFxManager.Play(SoundFxEvent.GameStarted);
            if (arcadeSystem != null)
                arcadeSystem.HideOverlay();
            PC.SetSimulatedInput(false, Vector2.zero, false);
            RetryMenu.SetActive(false);
            if (proceduralWavesEnabled && ProceduralRunStarted != null)
                ProceduralRunStarted();
            StartCoroutine(SetupNormal());
        }

        public void PlayAttractMode()
        {
            if (GameISRunning || IsAttractMode)
                return;

            IsAttractMode = true;
            musicController.PlayAttractMode();
            soundFxManager.SetAttractMode(true);
            RetryMenu.SetActive(false);
            if (proceduralWavesEnabled && ProceduralRunStarted != null)
                ProceduralRunStarted();
            StartCoroutine(SetupAttract());
        }

        private IEnumerator SetupAttract()
        {
            // Keep the title music playing during the unattended demo. Music
            // changes only after a person explicitly starts a run.
            SetupGame(false);
            PC.isInv = true;
            PC.SetSimulatedInput(true, Vector2.zero, false);
            yield return new WaitForSeconds(1f);
            StartGame();
        }

        public void StopAttractMode()
        {
            if (!IsAttractMode)
                return;

            ReturnToMainMenu();
        }

        public void ReturnToMainMenu()
        {
            IsAttractMode = false;
            GameISRunning = false;
            StopAllCoroutines();
            fadingIn = false;
            fadingOut = false;
            waveThemeTransition = null;
            DestroyShit();
            PC.ResetToDefaultState();
            if (RSM != null)
                RSM.speed = 0;
            RetryMenu.SetActive(false);
            SetScreenMessage("");
            UI_ScoreText.gameObject.SetActive(false);
            SetTimerVisible(false);
            if (UI_LivesRemaining.Count > 0)
                UI_LivesRemaining[0].transform.parent.gameObject.SetActive(false);
            soundFxManager.SetAttractMode(false);
            musicController.PlayMenu();
        }

        public bool ToggleAudioMuted()
        {
            audioMuted = !audioMuted;
            musicController.SetMuted(audioMuted);
            soundFxManager.SetMuted(audioMuted);
            return audioMuted;
        }

        public void RegisterTitleMenu(MenuScript menu)
        {
            if (arcadeSystem != null)
                arcadeSystem.RegisterTitleMenu(menu);
        }

        public bool CanStartFromMenu()
        {
            return arcadeSystem == null || arcadeSystem.CanStartFromMenu;
        }

        public void PlayEndless()
        {
            PlayGame();
        }

        private void WinGame()
        {
            if (endOfRunHandled)
                return;
            endOfRunHandled = true;
            GameISRunning = false;
            PC.canMove = false;
            SetTimerVisible(false);
            UI_TimeBonusText.text = "";
            UI_PointRewardText.text = "";
            UI_TimeRewardText.text = "";
            SetScreenMessage("CONGRATULATION");
            soundFxManager.Play(SoundFxEvent.GameWon);
            CountScore();
            if (IsAttractMode)
            {
                if (arcadeSystem != null)
                    arcadeSystem.ReturnToTitle();
            }
            else
            {
                gameOverTransition = StartCoroutine(ShowEndOfRunTransition());
            }

        }

        private void CountScore()
        {
            Debug.Log("Player score is : " + PlayerScore.ToString() + " x " + PlayerLives.ToString() + "Lives");
            PlayerScore = PlayerScore * PlayerLives;
            UI_ScoreText.text = PlayerScore.ToString("000000000");

        }

        private void LoseGame()
        {
            if (endOfRunHandled)
                return;
            endOfRunHandled = true;
            GameISRunning = false;
            PC.canMove = false;
            PC.onGunDelay = true;
            PC.ClearPowerUpEffects();
            SetTimerVisible(false);
            SetScreenMessage("GAME OVER");
            soundFxManager.Play(SoundFxEvent.GameOver);
            if (IsAttractMode)
            {
                if (arcadeSystem != null)
                    arcadeSystem.ReturnToTitle();
                return;
            }

            RetryMenu.SetActive(false);
            gameOverTransition = StartCoroutine(ShowEndOfRunTransition());

        }

        private IEnumerator ShowEndOfRunTransition()
        {
            // Keep the end-of-run message unobstructed long enough to register,
            // then move on to initials entry. Realtime keeps this reliable if a
            // future pause or slow-motion effect changes Time.timeScale.
            yield return new WaitForSecondsRealtime(GameOverTransitionDuration);

            SetScreenMessage("");
            gameOverTransition = null;
            if (arcadeSystem != null)
                arcadeSystem.BeginInitialsEntry(Mathf.Max(0, Mathf.RoundToInt(PlayerScore)));
            else
                ReturnToMainMenu();
        }

        #endregion

        #region "Wave Control"

        void SetupWave(int wave)
        {
            if (wave >= waveList.Count)
            {
                WinGame();
                return;
            }
            ES.SpawnWave(waveList[wave]);
            kal.angle = 0;
            ThisWaveTotal = waveList[wave].WaveCount;
            EnemiesShot = 0;
            thisWaveTime = 0;
            StartCoroutine(StartWave());
        }

        IEnumerator StartWave()
        {
            UI_TimeBonusText.text = "";
            UI_PointRewardText.text = "";
            UI_TimeRewardText.text = "";
            SetScreenMessage("WAVE " + (CurrentWave + 1).ToString());
            soundFxManager.Play(SoundFxEvent.WaveStarted);
            yield return new WaitForSeconds(1.8f);
            SetScreenMessage("GO!!");
            //PC.canMove = true;
            PC.onGunDelay = false;
            GameISRunning = true;
            RSM.speed = 80;
            fadingIn = true;
            yield return new WaitForSeconds(2);
            fadingIn = false;
            SetScreenMessage("");
        }

        public IEnumerator EndWave()
        {
            fadingOut = true;
            GameISRunning = false;
            SetScreenMessage("GREAT JOB");
            soundFxManager.Play(SoundFxEvent.WaveCompleted);
            Debug.Log(thisWaveTime.ToString());
            CheckTimeBonus(thisWaveTime);
            gameTime += 10;
            gameTime = (gameTime > 99) ? 99 : gameTime;
            UpdateTimerDisplay();
            DestroyShit();
            //PC.canMove = false;
            PC.onGunDelay = true;
            CurrentWave++;
            yield return new WaitForSeconds(3);
            fadingOut = false;
            UI_CurrentKalIndex++;
            if (UI_CurrentKalIndex > UI_KalSprites.Count - 1) UI_CurrentKalIndex = 0;
            SetupWave(CurrentWave);
        }

        #endregion

        #region "Public Methods"

        public void ShowWaveClearMessage(int wave)
        {
            SetScreenMessage("WAVE " + wave + " CLEAR");
            soundFxManager.Play(SoundFxEvent.WaveCompleted);
        }

        public void HideWaveClearMessage()
        {
            SetScreenMessage("");
        }

        private void SetScreenMessage(string message)
        {
            if (UI_ScreenText == null)
                return;
            if (screenTextTransition != null)
                StopCoroutine(screenTextTransition);
            screenTextTransition = StartCoroutine(AnimateScreenMessage(message));
        }

        private IEnumerator AnimateScreenMessage(string message)
        {
            bool showing = !string.IsNullOrEmpty(message);
            if (showing)
            {
                UI_ScreenText.text = message;
                screenTextGroup.alpha = 0f;
                UI_ScreenText.rectTransform.localScale = screenTextScale * 0.82f;
            }

            float startAlpha = screenTextGroup.alpha;
            Vector3 startScale = UI_ScreenText.rectTransform.localScale;
            float elapsed = 0f;
            float duration = showing ? 0.26f : 0.16f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = t * t * (3f - 2f * t);
                screenTextGroup.alpha = Mathf.Lerp(startAlpha, showing ? 1f : 0f, smooth);
                UI_ScreenText.rectTransform.localScale = Vector3.Lerp(
                    startScale, showing ? screenTextScale : screenTextScale * 1.06f, smooth);
                yield return null;
            }

            screenTextGroup.alpha = showing ? 1f : 0f;
            UI_ScreenText.rectTransform.localScale = screenTextScale;
            if (!showing)
                UI_ScreenText.text = string.Empty;
            screenTextTransition = null;
        }

        public void AddScore(float points)
        {
            PlayerScore += points;
            UI_ScoreText.text = PlayerScore.ToString("000000000");

        }

        public void AddTime(float times)
        {
            gameTime = Mathf.Min(99f, gameTime + Mathf.Max(0f, times));
            UpdateTimerDisplay();
        }

        public void ApplyHitTimePenalty()
        {
            gameTime = Mathf.Max(0f, gameTime - hitTimePenalty);
            UpdateTimerDisplay();
        }

        private void SetTimerVisible(bool visible)
        {
            if (UI_TimeLeft != null)
                UI_TimeLeft.gameObject.SetActive(visible);
        }

        private void UpdateTimerDisplay()
        {
            if (UI_TimeLeft != null)
                UI_TimeLeft.text = Mathf.CeilToInt(Mathf.Max(0f, gameTime)).ToString();
        }

        public void CheckTimeBonus(float time)
        {
            if (time < 25 && time > 15)
            {
                UI_TimeBonusText.text = BonusTextList[0];
                UI_PointRewardText.text = ScoreList[0];
                UI_TimeRewardText.text = TimeList[0];

                Debug.Log("Bronze Time Bonus +5 000");
                AddScore(5000);
                //AddTime(2);
                return;
            }
            if (time < 15 && time > 10) {
               Debug.Log("Silver Time Bonus +10 000");
                UI_TimeBonusText.text = BonusTextList[1];
                UI_PointRewardText.text = ScoreList[1];
                UI_TimeRewardText.text = TimeList[1];
                AddScore(10000);
               AddTime(5);
               return;
            }
            if (time < 10) {
                UI_TimeBonusText.text = BonusTextList[2];
                UI_PointRewardText.text = ScoreList[2];
                UI_TimeRewardText.text = TimeList[2];
                Debug.Log("Gold Time Bonus +50 000");
                AddScore(50000);
                AddTime(10);
                return;
            }
        }

        public void LostLife(int damage)
        {
            ProCamera2DShake.Instance.StopShaking();
            ProCamera2DShake.Instance.ShakeUsingPreset(0);
            int heartIndex = 9 - PlayerLives;
            if (heartIndex >= 0 && heartIndex < UI_LivesRemaining.Count)
                UI_LivesRemaining[heartIndex].enabled = false;
            PlayerLives = Mathf.Max(0, PlayerLives - damage);

            if (PlayerLives == 0)
            {
                LoseGame();
            }

        }

        public void RestoreLife(int amount)
        {
            for (int i = 0; i < amount && PlayerLives < 9; i++)
            {
                int heartIndex = 9 - PlayerLives - 1;
                if (heartIndex >= 0 && heartIndex < UI_LivesRemaining.Count)
                    UI_LivesRemaining[heartIndex].enabled = true;
                PlayerLives++;
            }
        }

        public void KilledOne()
        {
            EnemiesShot++;
            soundFxManager.Play(SoundFxEvent.EnemyDefeated);
        }

        #endregion


        #region "VFX"

        private void FadeOutKalSprite()
        {

        }

        private void FadeInKalSprite()
        {


        }

        #endregion
    }
}
