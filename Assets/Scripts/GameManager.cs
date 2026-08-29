using UnityEngine;
using UnityEngine.SceneManagement;

// Design doc section 17-18: score = seconds survived, death screen shows time + best,
// one-button restart. Simple singleton so any script can call GameManager.Instance.
//
// UNITY SETUP:
//   Create an empty GameObject called "GameManager" in your main scene, add this script.
//   Nothing else to wire — it finds/creates everything it needs on its own.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public float SurvivalTime { get; private set; }
    public bool IsGameOver { get; private set; }

    private const string HighScoreKey = "RunCrabRun_HighScore";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (!IsGameOver) SurvivalTime += Time.deltaTime;
    }

    public void OnPlayerDeath()
    {
        if (IsGameOver) return;
        IsGameOver = true;

        float best = PlayerPrefs.GetFloat(HighScoreKey, 0f);
        if (SurvivalTime > best)
        {
            best = SurvivalTime;
            PlayerPrefs.SetFloat(HighScoreKey, best);
        }

        UIManager.Instance?.ShowDeathScreen(SurvivalTime, best);
        Time.timeScale = 0f; // freeze the world for the death screen
    }

    // Wired to the death screen's Restart button (see UIManager.OnRestartButton).
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Used by the death screen's Main Menu button. Keeping navigation here prevents
    // the gameplay scene from needing any knowledge of menu UI implementation.
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
