using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class LevelProgression : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI clockText;
    public TextMeshProUGUI messageText;
    public Button nextNightButton;
    public Button retryButton;
    public Button saveAndMenuButton; // new: Save & Return to Menu

    [Header("Fade")]
    public ScreenFadeIn screenFade;

    [Header("Start SFX")]
    [Tooltip("Optional AudioSource to play a 'Night X' or start SFX when the start scene shows.")]
    public AudioSource startSfxSource;
    [Tooltip("Optional clip to play for the start SFX. If null, startSfxSource.clip will be used.")]
    public AudioClip startSfxClip;
    [Tooltip("If true the global audio listener will be unpaused before playing the start SFX.")]
    public bool unpauseAudioOnStart = true;
    [Tooltip("Play the start SFX when the 'Night X' message appears (only on the configured start scene).")]
    public bool playStartSfxOnShow = true;

    [Header("Level Settings")]
    public int maxLevels = 5;
    public float firstLevelDuration = 360f;
    public float levelIncrement = 25f;

    [Header("Save Settings")]
    [Tooltip("Scene name to return to when saving and returning to menu. This is also treated as the 'start scene' name.")]
    public string startSceneName = "Start";
    [Tooltip("PlayerPrefs key used to store the saved night.")]
    public string saveKey = "SavedNight";

    // Static variables to persist across scene reloads
    private static int s_currentLevel = 1;
    private static bool s_playerDied = false;

    // Public accessor so other systems (StormyAction, etc.) can read the current level
    public static int CurrentLevel => s_currentLevel;

    private float timer = 0f;
    private float currentLevelDuration;
    private bool levelActive = false;

    // Track audio sources we explicitly paused because they ignore AudioListener.pause
    private List<AudioSource> pausedAudioSources = new List<AudioSource>();

    // Keep previous canvas states so we can restore them after a forced-black
    private Canvas fadeCanvasPrev = null;
    private bool fadeCanvasPrevOverrideSorting = false;
    private int fadeCanvasPrevOrder = 0;

    private Canvas messageCanvasPrev = null;
    private bool messageCanvasPrevOverrideSorting = false;
    private int messageCanvasPrevOrder = 0;

    void Start()
    {
        // Load saved progress if any
        s_currentLevel = PlayerPrefs.GetInt(saveKey, s_currentLevel);

        // Hide buttons at start
        if (nextNightButton != null) nextNightButton.gameObject.SetActive(false);
        if (retryButton != null) retryButton.gameObject.SetActive(false);
        if (saveAndMenuButton != null) saveAndMenuButton.gameObject.SetActive(false);

        // Start with a black screen and "Night X"
        if (screenFade != null)
            screenFade.SetAlpha(1f);

        StartCoroutine(GameStartSequence());
    }

    void OnDestroy()
    {
        // Clean up UI listeners to avoid duplicates if object recreated
        if (nextNightButton != null) nextNightButton.onClick.RemoveAllListeners();
        if (retryButton != null) retryButton.onClick.RemoveAllListeners();
        if (saveAndMenuButton != null) saveAndMenuButton.onClick.RemoveAllListeners();
    }

    void Update()
    {
        if (!levelActive) return;

        timer += Time.deltaTime;
        UpdateClockDisplay();

        if (timer >= currentLevelDuration)
        {
            StartCoroutine(LevelPassedRoutine());
        }
    }

    IEnumerator GameStartSequence()
    {
        // Ensure black screen at start
        if (screenFade != null)
            screenFade.SetAlpha(1f);

        // If player died, show "You Died" and Retry button immediately (avoid showing Night message)
        if (s_playerDied)
        {
            // Guarantee the screen is black behind the death UI
            if (screenFade != null)
                screenFade.SetAlpha(1f);

            // Ensure audio is silent on the death screen
            CutToBlackAndPauseAudio();

            messageText.text = "You Died";
            messageText.gameObject.SetActive(true);
            SetMessageAlpha(1f);

            if (retryButton != null)
            {
                retryButton.gameObject.SetActive(true);
                retryButton.onClick.RemoveAllListeners();
                retryButton.onClick.AddListener(() => StartCoroutine(RetryLevelSequence()));
            }

            if (saveAndMenuButton != null)
            {
                saveAndMenuButton.gameObject.SetActive(true);
                saveAndMenuButton.onClick.RemoveAllListeners();
                saveAndMenuButton.onClick.AddListener(SaveAndReturnToMenu);
            }

            yield break;
        }

        // Show "Night X"
        messageText.text = $"Night {s_currentLevel}";
        messageText.gameObject.SetActive(true);
        SetMessageAlpha(1f);

        // Play start SFX only if this scene is the configured start/menu scene
        if (playStartSfxOnShow && SceneManager.GetActiveScene().name == startSceneName)
        {
            if (unpauseAudioOnStart)
                AudioListener.pause = false; // ensure audio will be audible

            PlayStartSfx();
        }

        // Fade in screen and message together
        float fadeTime = screenFade != null ? screenFade.fadeDuration : 1f;
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            if (screenFade != null) screenFade.SetAlpha(alpha);
            SetMessageAlpha(alpha);
            yield return null;
        }
        if (screenFade != null) screenFade.SetAlpha(0f);
        SetMessageAlpha(0f);
        messageText.gameObject.SetActive(false);

        StartLevel(s_currentLevel);
    }

    void StartLevel(int level)
    {
        timer = 0f;
        currentLevelDuration = firstLevelDuration + (level - 1) * levelIncrement;
        levelActive = true;
        UpdateClockDisplay();
        s_playerDied = false;

        // Unpause global audio now that the level actually begins
        AudioListener.pause = false;

        // Restore any AudioSources we paused because they ignored listener pause
        if (pausedAudioSources != null && pausedAudioSources.Count > 0)
        {
            foreach (var src in pausedAudioSources)
            {
                if (src == null) continue;
                try { src.UnPause(); } catch { }
            }
            pausedAudioSources.Clear();
        }

        // Restore any canvas sorting changes we made while forcing black so UI appears normally.
        if (fadeCanvasPrev != null)
        {
            try
            {
                fadeCanvasPrev.overrideSorting = fadeCanvasPrevOverrideSorting;
                fadeCanvasPrev.sortingOrder = fadeCanvasPrevOrder;
            }
            catch { }
            fadeCanvasPrev = null;
        }

        if (messageCanvasPrev != null)
        {
            try
            {
                messageCanvasPrev.overrideSorting = messageCanvasPrevOverrideSorting;
                messageCanvasPrev.sortingOrder = messageCanvasPrevOrder;
            }
            catch { }
            messageCanvasPrev = null;
        }

        // Release forced black state if we set it (fade back in)
        if (screenFade != null)
            screenFade.ReleaseForcedBlack(true);

        // Notify Stormy, Lilly, Leia and Toby that the level has started so they can begin movement
        var stormy = FindObjectOfType<StormyAction>();
        if (stormy != null)
            stormy.OnLevelStart();

        var lilly = FindObjectOfType<LillyAction>();
        if (lilly != null)
            lilly.OnLevelStart();

        var leia = FindObjectOfType<LeiaAction>();
        if (leia != null)
            leia.OnLevelStart();

        var toby = FindObjectOfType<TobyAction>();
        if (toby != null)
            toby.OnLevelStart();

        // call when the level/night actually starts
        foreach (var agent in FindObjectsOfType<MovementAgentBase>())
            agent.OnLevelStart();

        // Re-enable DogWalking spots for this level
        var spots = FindObjectsOfType<DogWalking>();
        if (spots != null && spots.Length > 0)
        {
            foreach (var s in spots)
                s.OnLevelStart();
        }
    }

    void UpdateClockDisplay()
    {
        float progress = Mathf.Clamp01(timer / currentLevelDuration);
        int hour = Mathf.FloorToInt(12 + 6 * progress); // 12 AM to 6 AM
        int minute = Mathf.FloorToInt(60 * (6 * progress - Mathf.Floor(6 * progress)));
        string ampm = hour >= 12 && hour < 24 ? "AM" : "PM";
        hour = hour > 12 ? hour - 12 : hour;
        clockText.text = $"{hour:00}:{minute:00} {ampm}";
    }

    IEnumerator LevelPassedRoutine()
    {
        levelActive = false;

        // Reset and disable Stormy, Lilly, Leia, and Toby immediately so they won't move during end sequence / UI
        var stormy = FindObjectOfType<StormyAction>();
        if (stormy != null)
            stormy.ResetAndDisableMovement();

        var lilly = FindObjectOfType<LillyAction>();
        if (lilly != null)
            lilly.ResetAndDisableMovement();

        var leia = FindObjectOfType<LeiaAction>();
        if (leia != null)
            leia.ResetAndDisableMovement();

        var toby = FindObjectOfType<TobyAction>();
        if (toby != null)
            toby.ResetAndDisableMovement();

        // call when the level ends / player dies
        foreach (var agent in FindObjectsOfType<MovementAgentBase>())
            agent.ResetAndDisableMovement();

        // Reset DogWalking spots so they won't play during end sequence / UI
        var spots = FindObjectsOfType<DogWalking>();
        if (spots != null && spots.Length > 0)
        {
            foreach (var s in spots)
                s.ResetAndDisablePlayback();
        }

        // Fade to black
        if (screenFade != null)
            yield return StartCoroutine(screenFade.FadeOutCoroutine());

        // Pause all audio now that the end UI will show
        CutToBlackAndPauseAudio();

        // Show "Night Survived"
        messageText.text = "Night Survived";
        messageText.gameObject.SetActive(true);
        SetMessageAlpha(1f);

        yield return new WaitForSeconds(screenFade != null ? screenFade.fadeDuration : 1f);

        // Advance to next night and persist progress
        s_currentLevel++;
        SaveProgress();

        if (s_currentLevel > maxLevels)
        {
            messageText.text = "GAME OVER";

            // Show Save & Return button on game over
            if (saveAndMenuButton != null)
            {
                saveAndMenuButton.gameObject.SetActive(true);
                saveAndMenuButton.onClick.RemoveAllListeners();
                saveAndMenuButton.onClick.AddListener(SaveAndReturnToMenu);
            }
        }
        else
        {
            // Show "Next Night" button
            if (nextNightButton != null)
            {
                nextNightButton.gameObject.SetActive(true);
                nextNightButton.onClick.RemoveAllListeners();
                nextNightButton.onClick.AddListener(() => StartCoroutine(ResetAndWaitForFadeIn()));
            }

            // Also offer Save & Return to menu on level passed
            if (saveAndMenuButton != null)
            {
                saveAndMenuButton.gameObject.SetActive(true);
                saveAndMenuButton.onClick.RemoveAllListeners();
                saveAndMenuButton.onClick.AddListener(SaveAndReturnToMenu);
            }
        }
    }

    IEnumerator ResetAndWaitForFadeIn()
    {
        if (nextNightButton != null) nextNightButton.gameObject.SetActive(false);

        // Ensure black screen before reload
        if (screenFade != null)
            screenFade.SetAlpha(1f);

        // Reload the scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        yield break;
    }

    // Call this method when the player dies
    public void OnPlayerDeath()
    {
        s_playerDied = true;

        // Cut to black and pause all audio immediately
        CutToBlackAndPauseAudio();

        // immediately stop/reset Stormy, Lilly, Leia and Toby so they won't move during the death/fade sequence
        var stormy = FindObjectOfType<StormyAction>();
        if (stormy != null)
            stormy.ResetAndDisableMovement();

        var lilly = FindObjectOfType<LillyAction>();
        if (lilly != null)
            lilly.ResetAndDisableMovement();

        var leia = FindObjectOfType<LeiaAction>();
        if (leia != null)
            leia.ResetAndDisableMovement();

        var toby = FindObjectOfType<TobyAction>();
        if (toby != null)
            toby.ResetAndDisableMovement();

        // stop DogWalking spots
        var spots = FindObjectsOfType<DogWalking>();
        if (spots != null && spots.Length > 0)
        {
            foreach (var s in spots)
                s.ResetAndDisablePlayback();
        }

        // Show the death UI and keep screen black until player chooses Retry or Save & Return
        if (messageText != null)
        {
            messageText.text = "You Died";
            messageText.gameObject.SetActive(true);
            SetMessageAlpha(1f);
        }

        // Show Retry button
        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(true);
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(() => StartCoroutine(RetryLevelSequence()));
        }

        // Show Save & Return button
        if (saveAndMenuButton != null)
        {
            saveAndMenuButton.gameObject.SetActive(true);
            saveAndMenuButton.onClick.RemoveAllListeners();
            saveAndMenuButton.onClick.AddListener(SaveAndReturnToMenu);
        }

        // Ensure Next Night button is hidden
        if (nextNightButton != null)
            nextNightButton.gameObject.SetActive(false);

        // Stop gameplay updates
        levelActive = false;

        Debug.Log("[LevelProgression] Player died: death UI shown and screen remains black until retry.");
    }

    IEnumerator RetryLevelSequence()
    {
        if (retryButton != null) retryButton.gameObject.SetActive(false);

        // Ensure black screen before reload
        if (screenFade != null)
            screenFade.SetAlpha(1f);

        // Clear the death flag so the next load starts the level normally
        s_playerDied = false;

        // Reload the scene (will restart current level)
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        yield break;
    }

    // Save current progress to persistent storage
    public void SaveProgress()
    {
        PlayerPrefs.SetInt(saveKey, s_currentLevel);
        PlayerPrefs.Save();
        Debug.Log($"[LevelProgression] Saved night {s_currentLevel} to PlayerPrefs key '{saveKey}'");
    }

    // Called by the Save & Return button
    public void SaveAndReturnToMenu()
    {
        SaveProgress();

        // Before returning to menu, unpause audio and play the start/menu music persistently
        AudioListener.pause = false;
        PlayStartSfxPersistent();

        if (string.IsNullOrEmpty(startSceneName))
        {
            // If no start scene specified, fallback to first scene in build settings (index 0)
            SceneManager.LoadScene(0);
            return;
        }

        SceneManager.LoadScene(startSceneName);
    }

    // Helper to set message alpha for fade
    void SetMessageAlpha(float alpha)
    {
        if (messageText != null)
        {
            var c = messageText.color;
            c.a = alpha;
            messageText.color = c;
        }
    }

    // Helper: immediately cut to black and pause all audio (called on death)
    private void CutToBlackAndPauseAudio()
    {
        // Prefer using the ScreenFade helper to force-black the fade image and top-sort it.
        if (screenFade != null && screenFade.fadeImage != null)
        {
            var img = screenFade.fadeImage;
            var canvas = img.canvas;
            if (canvas != null)
            {
                // remember previous canvas settings so we can restore them
                fadeCanvasPrev = canvas;
                fadeCanvasPrevOverrideSorting = canvas.overrideSorting;
                fadeCanvasPrevOrder = canvas.sortingOrder;
            }

            // Force the fade image black and on top
            screenFade.ForceBlackImmediate(10000);

            // Ensure the message UI renders above the black overlay
            if (messageText != null)
            {
                var msgCanvas = messageText.GetComponentInParent<Canvas>();
                if (msgCanvas != null)
                {
                    messageCanvasPrev = msgCanvas;
                    messageCanvasPrevOverrideSorting = msgCanvas.overrideSorting;
                    messageCanvasPrevOrder = msgCanvas.sortingOrder;

                    msgCanvas.overrideSorting = true;
                    msgCanvas.sortingOrder = 10001;
                }
            }
        }
        else
        {
            // Fallback: if no fade image present, try to make cameras render a solid black background
            foreach (var cam in Camera.allCameras)
            {
                try
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                    // Optionally hide all layers to be safe:
                    cam.cullingMask = 0;
                }
                catch { }
            }
        }

        // Pause global audio so everything mutes immediately
        AudioListener.pause = true;

        // Some AudioSources may ignore listener pause; pause them explicitly and remember to unpause later.
        pausedAudioSources.Clear();
        foreach (var src in FindObjectsOfType<AudioSource>())
        {
            if (src == null) continue;
            if (src.ignoreListenerPause)
            {
                try
                {
                    if (src.isPlaying)
                    {
                        src.Pause();
                        pausedAudioSources.Add(src);
                    }
                }
                catch { }
            }
        }

        Debug.Log("[LevelProgression] Player died: screen forced to black and audio paused.");
    }

    // Play the configured start SFX (called when Night X message appears on the start scene)
    private void PlayStartSfx()
    {
        if (startSfxSource != null)
        {
            AudioClip clip = startSfxClip != null ? startSfxClip : startSfxSource.clip;
            if (clip != null)
            {
                startSfxSource.PlayOneShot(clip);
                Debug.Log("[LevelProgression] Played start SFX via startSfxSource.");
                return;
            }
        }

        if (startSfxClip != null)
        {
            Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(startSfxClip, pos);
            Debug.Log("[LevelProgression] Played start SFX via PlayClipAtPoint.");
            return;
        }

        Debug.Log("[LevelProgression] No start SFX assigned.");
    }

    // Play start/menu SFX and keep it across the scene load (used when returning to menu)
    private void PlayStartSfxPersistent()
    {
        AudioClip clip = startSfxClip != null ? startSfxClip : (startSfxSource != null ? startSfxSource.clip : null);
        if (clip == null)
        {
            Debug.Log("[LevelProgression] No start SFX assigned for persistent playback.");
            return;
        }

        GameObject go = new GameObject("PersistentStartSfx");
        DontDestroyOnLoad(go);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D
        src.loop = false;
        src.volume = startSfxSource != null ? startSfxSource.volume : 1f;
        try { src.outputAudioMixerGroup = startSfxSource != null ? startSfxSource.outputAudioMixerGroup : null; } catch { }
        src.Play();
        Destroy(go, clip.length + 0.5f);
        Debug.Log("[LevelProgression] Started persistent start SFX (will survive scene load).");
    }
}
