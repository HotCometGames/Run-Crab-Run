using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// UNITY SETUP:
//   1. Create a Canvas (UI -> Canvas). Add an EventSystem if Unity didn't create one.
//   2. Under the Canvas, add two Sliders (UI -> Slider) named "HungerBar" and "ThirstBar".
//      Set their Min Value = 0, Max Value = 1, and remove/hide the default handle if you
//      want a plain fill bar.
//   3. Under the Canvas, add a Panel named "DeathScreenPanel" containing:
//        - a Text (or TextMeshPro Text) for survival time
//        - a Text for the best time
//        - a Button labeled "Restart"
//      Set the Panel inactive by default (this script also force-disables it in Awake).
//   4. Create an empty GameObject "UIManager", add this script, and drag in:
//        - Hunger Bar / Thirst Bar -> the two Sliders
//        - Player Survival -> your Player GameObject's PlayerSurvival component
//        - Death Screen Panel -> the Panel from step 3
//        - Survival Time Text / Best Time Text -> the two Text objects
//   5. Select the Restart Button -> On Click () -> drag the "UIManager" GameObject in,
//      and choose UIManager -> OnRestartButton() from the function dropdown.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("HUD")]
    public Slider hungerBar;
    public Slider thirstBar;
    public Slider healthBar;
    public PlayerSurvival playerSurvival;

    [Header("Death Screen")]
    public GameObject deathScreenPanel;
    public Text survivalTimeText;
    public Text bestTimeText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (deathScreenPanel != null) deathScreenPanel.SetActive(false);
        CreateMainMenuButton();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (playerSurvival == null) return;
        if (hungerBar != null) hungerBar.value = Normalized(playerSurvival.hunger, playerSurvival.maxHunger);
        if (thirstBar != null) thirstBar.value = Normalized(playerSurvival.thirst, playerSurvival.maxThirst);
        if (healthBar != null) healthBar.value = Normalized(playerSurvival.health, playerSurvival.maxHealth);
    }

    // Called by GameManager.OnPlayerDeath().
    public void ShowDeathScreen(float survivalTime, float bestTime)
    {
        if (deathScreenPanel != null) deathScreenPanel.SetActive(true);
        if (survivalTimeText != null) survivalTimeText.text = "SURVIVED: " + FormatTime(survivalTime);
        if (bestTimeText != null) bestTimeText.text = "BEST: " + FormatTime(bestTime);

        if (deathScreenPanel != null && EventSystem.current != null)
        {
            Button restart = deathScreenPanel.transform.Find("Restart")?.GetComponent<Button>();
            if (restart != null) EventSystem.current.SetSelectedGameObject(restart.gameObject);
        }
    }

    // Wired to the Restart button's OnClick() in the Inspector.
    public void OnRestartButton()
    {
        GameManager.Instance?.Restart();
    }

    public void OnMainMenuButton()
    {
        GameManager.Instance?.ReturnToMainMenu();
    }

    // The current death panel predates the dedicated menu scene, so this adds a
    // temporary default button without touching the artist-owned scene layout.
    private void CreateMainMenuButton()
    {
        if (deathScreenPanel == null || deathScreenPanel.transform.Find("Main Menu") != null) return;

        GameObject buttonObject = DefaultControls.CreateButton(new DefaultControls.Resources());
        buttonObject.name = "Main Menu";
        buttonObject.transform.SetParent(deathScreenPanel.transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -165f);
        rect.sizeDelta = new Vector2(200f, 38f);

        buttonObject.GetComponent<Button>().onClick.AddListener(OnMainMenuButton);
        Text label = buttonObject.GetComponentInChildren<Text>();
        if (label != null) label.text = "MAIN MENU";
    }

    private string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }

    private static float Normalized(float value, float maximum)
    {
        return maximum > 0f ? Mathf.Clamp01(value / maximum) : 0f;
    }
}
