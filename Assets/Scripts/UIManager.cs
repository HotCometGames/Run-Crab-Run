using UnityEngine;
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
    public PlayerSurvival playerSurvival;

    [Header("Death Screen")]
    public GameObject deathScreenPanel;
    public Text survivalTimeText;
    public Text bestTimeText;

    private void Awake()
    {
        Instance = this;
        if (deathScreenPanel != null) deathScreenPanel.SetActive(false);
    }

    private void Update()
    {
        if (playerSurvival == null) return;
        if (hungerBar != null) hungerBar.value = playerSurvival.hunger / playerSurvival.maxHunger;
        if (thirstBar != null) thirstBar.value = playerSurvival.thirst / playerSurvival.maxThirst;
    }

    // Called by GameManager.OnPlayerDeath().
    public void ShowDeathScreen(float survivalTime, float bestTime)
    {
        if (deathScreenPanel != null) deathScreenPanel.SetActive(true);
        if (survivalTimeText != null) survivalTimeText.text = FormatTime(survivalTime);
        if (bestTimeText != null) bestTimeText.text = "BEST: " + FormatTime(bestTime);
    }

    // Wired to the Restart button's OnClick() in the Inspector.
    public void OnRestartButton()
    {
        GameManager.Instance.Restart();
    }

    private string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m:00}:{s:00}";
    }
}
