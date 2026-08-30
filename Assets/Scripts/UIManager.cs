using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    private sealed class DeathOption
    {
        public string normalText;
        public string selectedText;
        public Text label;
        public RectTransform visualTransform;
        public Button button;
        public UnityEngine.Events.UnityAction action;
    }

    public static UIManager Instance { get; private set; }

    [Header("HUD")]
    public Slider hungerBar;
    public Slider thirstBar;
    public Slider healthBar;
    public PlayerSurvival playerSurvival;

    [Header("Legacy Death UI")]
    [Tooltip("The former scene-authored death panel. It is hidden while the runtime death transition UI is used.")]
    public GameObject deathScreenPanel;
    public Text survivalTimeText;
    public Text bestTimeText;

    [Header("Death Transition")]
    [SerializeField, Min(1f)] private float deathZoomMultiplier = 1.4f;
    [SerializeField, Min(0.1f)] private float deathTransitionDuration = 1.3f;
    [SerializeField, Min(0f)] private float deathMenuFadeDelay = 0.85f;
    [SerializeField, Min(0.1f)] private float deathMenuFadeDuration = 0.45f;
    [SerializeField, Range(0f, 0.9f)] private float darkOverlayAlpha = 0.9f;

    private readonly List<DeathOption> deathOptions = new List<DeathOption>();
    private readonly Color accentColor = new Color(0.86f, 0.31f, 0.22f, 1f);
    private readonly Color primaryTextColor = new Color(1f, 0.95f, 0.84f, 1f);
    private readonly Color mutedTextColor = new Color(0.86f, 0.78f, 0.67f, 1f);
    private readonly Color overlayColor = new Color(0.07f, 0.05f, 0.05f, 1f);

    private Font deathFont;
    private Image darkOverlay;
    private CanvasGroup deathMenuGroup;
    private Text scoreValueText;
    private Text bestScoreValueText;
    private Camera gameplayCamera;
    private float normalCameraSize;
    private bool deathSequenceRunning;
    private bool deathMenuInteractive;
    private int selectedDeathOption;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (deathScreenPanel != null) deathScreenPanel.SetActive(false);

        deathFont = LoadComicSans();
        BuildDeathTransitionUi();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (playerSurvival != null)
        {
            if (hungerBar != null) hungerBar.value = Normalized(playerSurvival.hunger, playerSurvival.maxHunger);
            if (thirstBar != null) thirstBar.value = Normalized(playerSurvival.thirst, playerSurvival.maxThirst);
            if (healthBar != null) healthBar.value = Normalized(playerSurvival.health, playerSurvival.maxHealth);
        }

        if (!deathMenuInteractive) return;

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            SetDeathSelection((selectedDeathOption + 1) % deathOptions.Count);
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            SetDeathSelection((selectedDeathOption - 1 + deathOptions.Count) % deathOptions.Count);
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            deathOptions[selectedDeathOption].action.Invoke();

        AnimateDeathOptionSelection();
    }

    // Called by GameManager.OnPlayerDeath after it has calculated the current and best scores.
    public void ShowDeathScreen(float survivalTime, float bestTime)
    {
        if (deathSequenceRunning) return;

        deathSequenceRunning = true;
        deathMenuInteractive = false;

        if (deathScreenPanel != null) deathScreenPanel.SetActive(false);
        if (survivalTimeText != null) survivalTimeText.text = "SCORE: " + ScoreText(survivalTime);
        if (bestTimeText != null) bestTimeText.text = "BEST SCORE: " + ScoreText(bestTime);

        scoreValueText.text = ScoreText(survivalTime);
        bestScoreValueText.text = ScoreText(bestTime);
        darkOverlay.color = WithAlpha(overlayColor, 0f);
        deathMenuGroup.alpha = 0f;
        deathMenuGroup.blocksRaycasts = false;
        deathMenuGroup.interactable = false;

        gameplayCamera = Camera.main;
        if (gameplayCamera != null && gameplayCamera.orthographic)
            normalCameraSize = gameplayCamera.orthographicSize;

        SetDeathSelection(0);
        StartCoroutine(PlayDeathSequence());
    }

    // Kept for the existing scene-authored Restart button, if it is still wired.
    public void OnRestartButton() => RestartRun();

    public void OnMainMenuButton() => ReturnToMainMenu();

    private IEnumerator PlayDeathSequence()
    {
        float elapsed = 0f;

        while (elapsed < deathTransitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float transitionT = Mathf.Clamp01(elapsed / deathTransitionDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, transitionT);

            if (gameplayCamera != null && gameplayCamera.orthographic)
                gameplayCamera.orthographicSize = Mathf.Lerp(normalCameraSize, normalCameraSize * deathZoomMultiplier, easedT);

            darkOverlay.color = WithAlpha(overlayColor, Mathf.Lerp(0f, darkOverlayAlpha, easedT));

            float menuT = Mathf.Clamp01((elapsed - deathMenuFadeDelay) / deathMenuFadeDuration);
            deathMenuGroup.alpha = Mathf.SmoothStep(0f, 1f, menuT);
            yield return null;
        }

        if (gameplayCamera != null && gameplayCamera.orthographic)
            gameplayCamera.orthographicSize = normalCameraSize * deathZoomMultiplier;

        darkOverlay.color = WithAlpha(overlayColor, darkOverlayAlpha);
        deathMenuGroup.alpha = 1f;
        deathMenuGroup.blocksRaycasts = true;
        deathMenuGroup.interactable = true;
        deathMenuInteractive = true;
    }

    private void RestartRun()
    {
        deathMenuInteractive = false;
        GameManager.Instance?.Restart();
    }

    private void ReturnToMainMenu()
    {
        deathMenuInteractive = false;
        GameManager.Instance?.ReturnToMainMenu();
    }

    private void BuildDeathTransitionUi()
    {
        GameObject root = new GameObject("Death UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        darkOverlay = CreateImage(root.transform, "Dark Overlay", WithAlpha(overlayColor, 0f));
        darkOverlay.raycastTarget = false;
        Stretch(darkOverlay.rectTransform);

        GameObject menu = new GameObject("Death Menu", typeof(RectTransform), typeof(CanvasGroup), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        menu.transform.SetParent(root.transform, false);
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        menuRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.pivot = new Vector2(0.5f, 0.5f);
        menuRect.sizeDelta = new Vector2(680f, 0f);

        deathMenuGroup = menu.GetComponent<CanvasGroup>();
        deathMenuGroup.alpha = 0f;
        deathMenuGroup.blocksRaycasts = false;
        deathMenuGroup.interactable = false;

        VerticalLayoutGroup menuLayout = menu.GetComponent<VerticalLayoutGroup>();
        menuLayout.childAlignment = TextAnchor.MiddleCenter;
        menuLayout.childControlWidth = true;
        menuLayout.childControlHeight = false;
        menuLayout.childForceExpandWidth = true;
        menuLayout.childForceExpandHeight = false;
        menuLayout.spacing = 12f;
        menuLayout.padding = new RectOffset(18, 18, 18, 18);
        menu.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Text title = CreateText(menu.transform, "You Died", "YOU DIED", 92, TextAnchor.MiddleCenter, accentColor);
        SetLayoutHeight(title.gameObject, 112f);
        AddSpacer(menu.transform, 6f);

        scoreValueText = CreateScoreGroup(menu.transform, "Score Group", "SCORE");
        bestScoreValueText = CreateScoreGroup(menu.transform, "Best Score Group", "BEST SCORE");
        AddSpacer(menu.transform, 10f);

        GameObject options = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        options.transform.SetParent(menu.transform, false);
        VerticalLayoutGroup optionsLayout = options.GetComponent<VerticalLayoutGroup>();
        optionsLayout.childAlignment = TextAnchor.MiddleCenter;
        optionsLayout.childControlWidth = true;
        optionsLayout.childControlHeight = false;
        optionsLayout.childForceExpandWidth = true;
        optionsLayout.childForceExpandHeight = false;
        optionsLayout.spacing = 4f;
        options.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AddDeathOption(options.transform, "RESTART", RestartRun);
        AddDeathOption(options.transform, "MAIN MENU", ReturnToMainMenu);
    }

    private Text CreateScoreGroup(Transform parent, string name, string label)
    {
        GameObject group = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        group.transform.SetParent(parent, false);
        VerticalLayoutGroup layout = group.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 2f;
        group.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Text labelText = CreateText(group.transform, "Label", label, 25, TextAnchor.MiddleCenter, mutedTextColor);
        SetLayoutHeight(labelText.gameObject, 30f);
        Text valueText = CreateText(group.transform, "Value", "0", 55, TextAnchor.MiddleCenter, primaryTextColor);
        SetLayoutHeight(valueText.gameObject, 62f);
        return valueText;
    }

    private void AddDeathOption(Transform parent, string caption, UnityEngine.Events.UnityAction action)
    {
        GameObject optionObject = new GameObject(caption + " Option", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(EventTrigger));
        optionObject.transform.SetParent(parent, false);
        Image hitArea = optionObject.GetComponent<Image>();
        hitArea.color = Color.clear;

        Button button = optionObject.GetComponent<Button>();
        button.targetGraphic = hitArea;
        button.onClick.AddListener(action);
        SetLayoutHeight(optionObject, 48f);

        Text label = CreateText(optionObject.transform, "Label", caption, 31, TextAnchor.MiddleCenter, primaryTextColor);
        Stretch(label.rectTransform);

        DeathOption option = new DeathOption
        {
            normalText = "  " + caption,
            selectedText = "> " + caption,
            label = label,
            visualTransform = label.rectTransform,
            button = button,
            action = action
        };
        int index = deathOptions.Count;
        deathOptions.Add(option);

        EventTrigger trigger = optionObject.GetComponent<EventTrigger>();
        AddPointerEvent(trigger, EventTriggerType.PointerEnter, () => SetDeathSelection(index));
        AddPointerEvent(trigger, EventTriggerType.PointerClick, action);
    }

    private void SetDeathSelection(int index)
    {
        if (deathOptions.Count == 0) return;
        selectedDeathOption = Mathf.Clamp(index, 0, deathOptions.Count - 1);
        EventSystem.current?.SetSelectedGameObject(deathOptions[selectedDeathOption].button.gameObject);
    }

    private void AnimateDeathOptionSelection()
    {
        float blend = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
        for (int i = 0; i < deathOptions.Count; i++)
        {
            DeathOption option = deathOptions[i];
            bool selected = i == selectedDeathOption;
            float scale = selected ? 1.13f : 1f;
            Color targetColor = selected ? accentColor : primaryTextColor;

            option.visualTransform.localScale = Vector3.Lerp(option.visualTransform.localScale, Vector3.one * scale, blend);
            option.label.color = Color.Lerp(option.label.color, targetColor, blend);
            string targetText = selected ? option.selectedText : option.normalText;
            if (option.label.text != targetText) option.label.text = targetText;
        }
    }

    private Text CreateText(Transform parent, string name, string value, int size, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = deathFont;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = false;
        return text;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void AddSpacer(Transform parent, float height)
    {
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        SetLayoutHeight(spacer, height);
    }

    private static void AddPointerEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction callback)
    {
        if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => callback.Invoke());
        trigger.triggers.Add(entry);
    }

    private static void SetLayoutHeight(GameObject gameObject, float height)
    {
        LayoutElement element = gameObject.GetComponent<LayoutElement>();
        if (element == null) element = gameObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Font LoadComicSans()
    {
        foreach (string installedFont in Font.GetOSInstalledFontNames())
        {
            if (!string.Equals(installedFont, "Comic Sans MS", StringComparison.OrdinalIgnoreCase) &&
                installedFont.IndexOf("Comic Sans", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Font comicSans = Font.CreateDynamicFontFromOSFont(installedFont, 16);
            if (comicSans != null) return comicSans;
        }

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static string ScoreText(float score) => Mathf.Max(0, Mathf.FloorToInt(score)).ToString();

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static float Normalized(float value, float maximum)
    {
        return maximum > 0f ? Mathf.Clamp01(value / maximum) : 0f;
    }
}
