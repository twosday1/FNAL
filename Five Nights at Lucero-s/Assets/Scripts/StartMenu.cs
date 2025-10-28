using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class StartMenu : MonoBehaviour
{
    [Header("Scene names")]
    [Tooltip("Name of the main game scene that contains LevelProgression")]
    public string gameSceneName = "Main"; // set to your gameplay scene name
    [Tooltip("Name of the start/menu scene to load when returning from game (optional)")]
    public string startSceneName = "Start";

    [Header("Save")]
    [Tooltip("PlayerPrefs key used to store the saved night (must match LevelProgression.saveKey)")]
    public string saveKey = "SavedNight";

    [Header("UI")]
    public Button newGameButton;
    public Button loadSaveButton;
    public Button quitButton;
    public TextMeshProUGUI saveInfoText; // shows "No Save" or "Saved Night X"

    // cap save shown/loaded at this night
    private const int SaveMaxNight = 6;

    void Start()
    {
        // Wire UI (if not wired in inspector)
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveAllListeners();
            newGameButton.onClick.AddListener(OnNewGame);
        }

        if (loadSaveButton != null)
        {
            loadSaveButton.onClick.RemoveAllListeners();
            loadSaveButton.onClick.AddListener(OnLoadSave);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuit);
        }

        RefreshSaveUI();
    }

    // Refresh UI state for load/save button and info text
    public void RefreshSaveUI()
    {
        int savedNight = PlayerPrefs.GetInt(saveKey, 0);
        if (savedNight > SaveMaxNight) savedNight = SaveMaxNight;

        bool hasSave = savedNight > 0;

        if (saveInfoText != null)
        {
            saveInfoText.text = hasSave ? $"Saved Night: {savedNight}" : "No Save";
        }

        if (loadSaveButton != null)
        {
            loadSaveButton.interactable = hasSave;
            var btnText = loadSaveButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
                btnText.text = hasSave ? $"Load Night {savedNight}" : "Load Save";
        }
    }

    // Starts a fresh game and overwrites any existing save with Night 1
    public void OnNewGame()
    {
        PlayerPrefs.SetInt(saveKey, 1);
        PlayerPrefs.Save();
        // Immediately load the game scene; LevelProgression reads PlayerPrefs on start
        if (!string.IsNullOrEmpty(gameSceneName))
            SceneManager.LoadScene(gameSceneName);
        else
            SceneManager.LoadScene(1); // fallback to build index 1
    }

    // Loads the saved night (LevelProgression will pick it up from PlayerPrefs)
    public void OnLoadSave()
    {
        int savedNight = PlayerPrefs.GetInt(saveKey, 0);
        if (savedNight > SaveMaxNight) savedNight = SaveMaxNight;

        if (savedNight <= 0)
        {
            Debug.LogWarning("[StartMenu] No saved night to load.");
            RefreshSaveUI();
            return;
        }

        if (!string.IsNullOrEmpty(gameSceneName))
            SceneManager.LoadScene(gameSceneName);
        else
            SceneManager.LoadScene(1);
    }

    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Optional helper: call to clear the save (for debugging)
    public void ClearSave()
    {
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();
        RefreshSaveUI();
    }
}
