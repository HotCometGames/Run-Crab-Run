using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    private const string HighScoreKey = "RunCrabRun_HighScore";
    private const int HeartCount = 3;
    private const float ResourceBarWidth = 280f;
    private const float ResourceBarHeight = 20f;

    private enum HeartState { Dim, HalfLit, Lit }

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
    [Tooltip("Legacy scene bars. They are hidden when the runtime HUD is built.")]
    public Slider hungerBar;
    public Slider thirstBar;
    public Slider healthBar;
    public PlayerSurvival playerSurvival;

    [Header("HUD Art (Optional)")]
    [SerializeField] private Sprite halfLitHeartSprite;
    [SerializeField] private Sprite fullHeartSprite;
    [SerializeField] private Sprite emptyHeartSprite;
    [SerializeField] private Sprite hungerIcon;
    [SerializeField] private Sprite thirstIcon;

    [Header("HUD Animation")]
    [SerializeField, Min(0.1f)] private float heartRegenFlashDuration = 0.4f;
    [SerializeField, Min(1)] private int heartRegenFlashCount = 2;
    [SerializeField, Min(0.1f)] private float resourceBarSmoothingSpeed = 10f;

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
    private readonly Color hudTextColor = new Color(0.16f, 0.13f, 0.11f, 1f);
    private readonly Color hudMutedTextColor = new Color(0.31f, 0.25f, 0.21f, 1f);
    private readonly Color hungerColor = new Color(0.87f, 0.31f, 0.19f, 1f);
    private readonly Color thirstColor = new Color(0.28f, 0.66f, 0.82f, 1f);
    private readonly Color barBackgroundColor = new Color(0.15f, 0.12f, 0.1f, 0.65f);

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

    private readonly List<Image> heartImages = new List<Image>(HeartCount);
    private readonly Coroutine[] heartRegenRoutines = new Coroutine[HeartCount];
    private RectTransform hungerFillRect;
    private RectTransform thirstFillRect;
    private Sprite hudBarSprite;
    private Text survivalTimerText;
    private Text bestTimerText;
    private int targetHeartCount = -1;
    private int displayedSecond = -1;
    private float displayedHunger;
    private float displayedThirst;
    private float bestSurvivalTime;
    private bool resourcesInitialized;

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
        LoadBundledHeartArt();
        HideLegacyHud();
        BuildGameplayHud();
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
            UpdateHealthDisplay();

            if (!IsRunOver())
                UpdateResourceDisplay();
        }

        if (!IsRunOver()) UpdateSurvivalTimer();

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

    private void HideLegacyHud()
    {
        // The scene-authored sliders are retained for backwards-compatible Inspector
        // references, but the new HUD owns their presentation.
        if (hungerBar != null) hungerBar.gameObject.SetActive(false);
        if (thirstBar != null) thirstBar.gameObject.SetActive(false);
        if (healthBar != null) healthBar.gameObject.SetActive(false);
    }

    private void LoadBundledHeartArt()
    {
        // Inspector assignments take priority. These bundled defaults let the HUD use
        // the supplied artwork without requiring a scene edit or a duplicate prefab.
        if (halfLitHeartSprite == null) halfLitHeartSprite = LoadCenteredHeartSprite("HUD/Heart_HalfLit");
        if (fullHeartSprite == null) fullHeartSprite = LoadCenteredHeartSprite("HUD/Heart_Lit");
        if (emptyHeartSprite == null) emptyHeartSprite = LoadCenteredHeartSprite("HUD/Heart_Dim");
    }

    private static Sprite LoadCenteredHeartSprite(string resourcePath)
    {
        Texture2D source = Resources.Load<Texture2D>(resourcePath);
        if (source == null) return null;

        // The supplied source art has a large transparent canvas around a centered
        // heart. Creating a sprite from its centre keeps the original pixels while
        // making it an appropriate size for the compact HUD row.
        float cropSize = Mathf.Min(256f, Mathf.Min(source.width, source.height));
        float cropX = (source.width - cropSize) * 0.5f;
        float cropY = (source.height - cropSize) * 0.5f;
        return Sprite.Create(source, new Rect(cropX, cropY, cropSize, cropSize), new Vector2(0.5f, 0.5f), cropSize);
    }

    private void BuildGameplayHud()
    {
        GameObject root = new GameObject("Gameplay HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildHealthDisplay(root.transform);
        BuildTimerDisplay(root.transform);
        BuildResourceDisplay(root.transform);

        bestSurvivalTime = PlayerPrefs.GetFloat(HighScoreKey, 0f);
        UpdateSurvivalTimer();
    }

    private void BuildHealthDisplay(Transform parent)
    {
        GameObject display = new GameObject("Health Display", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        display.transform.SetParent(parent, false);
        RectTransform rect = display.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(44f, -40f);
        rect.sizeDelta = new Vector2(180f, 56f);

        HorizontalLayoutGroup layout = display.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 9f;

        for (int i = 0; i < HeartCount; i++)
        {
            Image heart = CreateImage(display.transform, "Heart " + (i + 1), Color.white);
            heart.preserveAspect = true;
            heart.raycastTarget = false;
            SetLayoutSize(heart.gameObject, 50f, 50f);
            heartImages.Add(heart);
            SetHeartSprite(i, false);
        }
    }

    private void BuildTimerDisplay(Transform parent)
    {
        GameObject display = new GameObject("Timer Display", typeof(RectTransform));
        display.transform.SetParent(parent, false);
        RectTransform rect = display.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -24f);
        rect.sizeDelta = new Vector2(420f, 140f);

        survivalTimerText = CreateHudText(display.transform, "Current Time", "00:00", 96, TextAnchor.MiddleCenter);
        survivalTimerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        survivalTimerText.verticalOverflow = VerticalWrapMode.Overflow;
        ConfigureTimerTextRect(survivalTimerText.rectTransform, 0f, 104f);
        bestTimerText = CreateHudText(display.transform, "Best Time", "★ 00:00", 27, TextAnchor.MiddleCenter);
        bestTimerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        bestTimerText.verticalOverflow = VerticalWrapMode.Overflow;
        ConfigureTimerTextRect(bestTimerText.rectTransform, -108f, 32f);
    }

    private static void ConfigureTimerTextRect(RectTransform rect, float topOffset, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, topOffset);
        rect.sizeDelta = new Vector2(420f, height);
    }

    private void BuildResourceDisplay(Transform parent)
    {
        GameObject display = new GameObject("Bottom Resources", typeof(RectTransform));
        display.transform.SetParent(parent, false);
        RectTransform rect = display.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 34f);
        rect.sizeDelta = new Vector2(650f, 96f);

        hungerFillRect = CreateResourceGroup(display.transform, "Hunger Display", "HUNGER", hungerIcon, hungerColor, -165f);
        thirstFillRect = CreateResourceGroup(display.transform, "Thirst Display", "THIRST", thirstIcon, thirstColor, 165f);
    }

    private RectTransform CreateResourceGroup(Transform parent, string name, string label, Sprite iconSprite, Color fillColor, float xPosition)
    {
        GameObject group = new GameObject(name, typeof(RectTransform));
        group.transform.SetParent(parent, false);
        RectTransform groupRect = group.GetComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0.5f, 0f);
        groupRect.anchorMax = new Vector2(0.5f, 0f);
        groupRect.pivot = new Vector2(0.5f, 0f);
        groupRect.anchoredPosition = new Vector2(xPosition, 0f);
        groupRect.sizeDelta = new Vector2(ResourceBarWidth, 96f);

        Text title = CreateHudText(group.transform, "Label", label, 20, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(ResourceBarWidth, 26f);

        Image icon = CreateImage(group.transform, "Icon", Color.white);
        icon.sprite = iconSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = iconSprite != null;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -28f);
        iconRect.sizeDelta = iconSprite != null ? new Vector2(26f, 26f) : Vector2.zero;

        GameObject track = new GameObject("Track", typeof(RectTransform), typeof(Image));
        track.transform.SetParent(group.transform, false);
        RectTransform trackRect = track.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0.5f, 1f);
        trackRect.anchorMax = new Vector2(0.5f, 1f);
        trackRect.pivot = new Vector2(0.5f, 1f);
        trackRect.anchoredPosition = new Vector2(0f, iconSprite != null ? -66f : -38f);
        trackRect.sizeDelta = new Vector2(ResourceBarWidth, ResourceBarHeight);

        Image background = track.GetComponent<Image>();
        background.sprite = GetHudBarSprite();
        background.color = barBackgroundColor;
        background.raycastTarget = false;

        Image fill = CreateImage(track.transform, "Fill", fillColor);
        fill.sprite = background.sprite;
        fill.raycastTarget = false;
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(ResourceBarWidth, 0f);
        return fillRect;
    }

    private void UpdateHealthDisplay()
    {
        int fullHearts = Mathf.Clamp(Mathf.FloorToInt(playerSurvival.health + 0.0001f), 0, HeartCount);
        if (fullHearts == targetHeartCount) return;

        if (targetHeartCount < 0 || fullHearts < targetHeartCount)
        {
            StopHeartAnimations();
            for (int i = 0; i < HeartCount; i++) SetHeartSprite(i, i < fullHearts);
        }
        else
        {
            for (int i = targetHeartCount; i < fullHearts; i++)
            {
                StopHeartAnimation(i);
                heartRegenRoutines[i] = StartCoroutine(FlashHeartBeforeRestore(i));
            }
        }

        targetHeartCount = fullHearts;
    }

    private IEnumerator FlashHeartBeforeRestore(int heartIndex)
    {
        SetHeartSprite(heartIndex, false);
        float stepDuration = heartRegenFlashDuration / (heartRegenFlashCount * 2f + 1f);

        for (int flash = 0; flash < heartRegenFlashCount; flash++)
        {
            heartImages[heartIndex].enabled = false;
            yield return new WaitForSeconds(stepDuration);
            SetHeartSprite(heartIndex, HeartState.HalfLit);
            yield return new WaitForSeconds(stepDuration);
        }

        SetHeartSprite(heartIndex, HeartState.Lit);
        heartImages[heartIndex].rectTransform.localScale = Vector3.one * 1.12f;
        yield return new WaitForSeconds(stepDuration);
        heartImages[heartIndex].rectTransform.localScale = Vector3.one;
        heartRegenRoutines[heartIndex] = null;
    }

    private void StopHeartAnimations()
    {
        for (int i = 0; i < HeartCount; i++) StopHeartAnimation(i);
    }

    private void StopHeartAnimation(int heartIndex)
    {
        if (heartRegenRoutines[heartIndex] != null)
        {
            StopCoroutine(heartRegenRoutines[heartIndex]);
            heartRegenRoutines[heartIndex] = null;
        }
    }

    private void SetHeartSprite(int heartIndex, bool full)
    {
        SetHeartSprite(heartIndex, full ? HeartState.Lit : HeartState.Dim);
    }

    private void SetHeartSprite(int heartIndex, HeartState state)
    {
        if (heartIndex < 0 || heartIndex >= heartImages.Count) return;
        Image heart = heartImages[heartIndex];
        Sprite sprite = state == HeartState.Lit
            ? fullHeartSprite
            : state == HeartState.HalfLit
                ? halfLitHeartSprite
                : emptyHeartSprite;
        heart.sprite = sprite;
        heart.enabled = sprite != null;
        heart.color = Color.white;
        heart.rectTransform.localScale = Vector3.one;
    }

    private void UpdateResourceDisplay()
    {
        float hunger = Normalized(playerSurvival.hunger, playerSurvival.maxHunger);
        float thirst = Normalized(playerSurvival.thirst, playerSurvival.maxThirst);

        if (!resourcesInitialized)
        {
            displayedHunger = hunger;
            displayedThirst = thirst;
            resourcesInitialized = true;
        }
        else
        {
            float blend = 1f - Mathf.Exp(-resourceBarSmoothingSpeed * Time.deltaTime);
            displayedHunger = Mathf.Lerp(displayedHunger, hunger, blend);
            displayedThirst = Mathf.Lerp(displayedThirst, thirst, blend);
        }

        SetResourceFillWidth(hungerFillRect, displayedHunger);
        SetResourceFillWidth(thirstFillRect, displayedThirst);
    }

    private void UpdateSurvivalTimer()
    {
        GameManager manager = GameManager.Instance;
        float survivalTime = manager != null ? manager.SurvivalTime : 0f;
        int elapsedSeconds = Mathf.Max(0, Mathf.FloorToInt(survivalTime));
        if (elapsedSeconds == displayedSecond) return;

        displayedSecond = elapsedSeconds;
        if (survivalTimerText != null) survivalTimerText.text = ClockText(elapsedSeconds);
        if (bestTimerText != null) bestTimerText.text = "★ " + ClockText(bestSurvivalTime);
    }

    private static void SetResourceFillWidth(RectTransform fillRect, float normalizedValue)
    {
        if (fillRect == null) return;
        fillRect.sizeDelta = new Vector2(ResourceBarWidth * Mathf.Clamp01(normalizedValue), 0f);
    }

    private static bool IsRunOver()
    {
        return GameManager.Instance != null && GameManager.Instance.IsGameOver;
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

    private Text CreateHudText(Transform parent, string name, string value, int size, TextAnchor alignment)
    {
        Text text = CreateText(parent, name, value, size, alignment, Color.white);
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
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

    private Sprite GetHudBarSprite()
    {
        if (hudBarSprite != null) return hudBarSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "Runtime HUD Bar Texture";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);
        texture.hideFlags = HideFlags.HideAndDontSave;

        hudBarSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        hudBarSprite.name = "Runtime HUD Bar Sprite";
        hudBarSprite.hideFlags = HideFlags.HideAndDontSave;
        return hudBarSprite;
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

    private static void SetLayoutSize(GameObject gameObject, float width, float height)
    {
        LayoutElement element = gameObject.GetComponent<LayoutElement>();
        if (element == null) element = gameObject.AddComponent<LayoutElement>();
        element.minWidth = width;
        element.preferredWidth = width;
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

    private static string ClockText(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int remainingSeconds = totalSeconds % 60;
        return hours > 0
            ? string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, remainingSeconds)
            : string.Format("{0:00}:{1:00}", minutes, remainingSeconds);
    }

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
