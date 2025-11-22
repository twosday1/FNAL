using System.Collections;
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

    [Header("Level Settings")]
    public int maxLevels = 5;
    public float firstLevelDuration = 360f;
    public float levelIncrement = 25f;

    [Header("Save Settings")]
    [Tooltip("Scene name to return to when saving and returning to menu.")]
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

        StartCoroutine(ResetAndWaitForFadeIn());
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
        if (screenFade != null)
            screenFade.SetAlpha(1f);

        // Pause global audio so everything mutes immediately
        AudioListener.pause = true;

        Debug.Log("[LevelProgression] Player died: screen cut to black and audio paused.");
    }
}
