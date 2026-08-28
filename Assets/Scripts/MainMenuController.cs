using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Runtime-built placeholder menu. Keeping it in code lets the team iterate on the
// game without waiting for final UI art; the artist can later replace these panels
// with designed prefabs while retaining the public button methods.
public class MainMenuController : MonoBehaviour
{
    private static bool skipMenuOnce;

    private const float PanelWidth = 520f;
    private const float ButtonHeight = 54f;

    private Font font;
    private GameObject root;
    private GameObject mainPanel;
    private GameObject howToPlayPanel;
    private GameObject settingsPanel;
    private GameObject creditsPanel;
    private Slider musicSlider;
    private Slider effectsSlider;
    private Text musicValueText;
    private Text effectsValueText;

    public static void EnsureExists()
    {
        if (FindFirstObjectByType<MainMenuController>() != null) return;
        new GameObject("Main Menu").AddComponent<MainMenuController>();
    }

    public static void SkipNextMenuOnce()
    {
        skipMenuOnce = true;
    }

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        CreateMenu();

        if (skipMenuOnce)
        {
            skipMenuOnce = false;
            StartRun();
        }
        else
        {
            Time.timeScale = 0f;
            ShowMain();
        }
    }

    public void StartRun()
    {
        root.SetActive(false);
        Time.timeScale = 1f;
    }

    public void ShowMain()
    {
        root.SetActive(true);
        SetOnlyPanelActive(mainPanel);
    }

    public void ShowHowToPlay() => SetOnlyPanelActive(howToPlayPanel);
    public void ShowSettings() => SetOnlyPanelActive(settingsPanel);
    public void ShowCredits() => SetOnlyPanelActive(creditsPanel);

    public void SetMusicVolume(float value)
    {
        GameAudioSettings.SetMusicVolume(value);
        musicValueText.text = Percentage(value);
    }

    public void SetEffectsVolume(float value)
    {
        GameAudioSettings.SetEffectsVolume(value);
        effectsValueText.text = Percentage(value);
    }

    private void CreateMenu()
    {
        EnsureEventSystem();

        root = new GameObject("Start Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image background = AddImage(root.transform, "Background", new Color(0.04f, 0.09f, 0.1f, 0.98f));
        Stretch(background.rectTransform);

        mainPanel = CreatePanel("Main Panel");
        CreateTitle(mainPanel.transform, "RUN CRAB RUN", 64, new Color(0.94f, 0.82f, 0.55f));
        CreateSubtitle(mainPanel.transform, "Trust no one. Survive as long as you can.");
        CreateButton(mainPanel.transform, "Start", StartRun);
        CreateButton(mainPanel.transform, "How to Play", ShowHowToPlay);
        CreateButton(mainPanel.transform, "Settings", ShowSettings);
        CreateButton(mainPanel.transform, "Credits", ShowCredits);

        howToPlayPanel = CreatePanel("How To Play Panel");
        CreateTitle(howToPlayPanel.transform, "HOW TO PLAY", 38, Color.white);
        CreateBodyText(howToPlayPanel.transform,
            "Move with WASD or Arrow Keys\nSprint with Left Shift\n\nFind berries to restore hunger and stand in wells to restore thirst.\nHide in bushes to break a ground predator's chase.\n\nDeer and sheep look harmless, but any one of them could be an Imposter.\nYou cannot fight—run, hide, and survive.");
        CreateButton(howToPlayPanel.transform, "Back", ShowMain);

        settingsPanel = CreatePanel("Settings Panel");
        CreateTitle(settingsPanel.transform, "SETTINGS", 38, Color.white);
        CreateBodyText(settingsPanel.transform, "CONTROLS\nMove: WASD / Arrow Keys\nSprint: Left Shift");
        musicSlider = CreateSlider(settingsPanel.transform, "Music Volume", GameAudioSettings.MusicVolume, out musicValueText, SetMusicVolume);
        effectsSlider = CreateSlider(settingsPanel.transform, "Effects Volume", GameAudioSettings.EffectsVolume, out effectsValueText, SetEffectsVolume);
        CreateButton(settingsPanel.transform, "Back", ShowMain);

        creditsPanel = CreatePanel("Credits Panel");
        CreateTitle(creditsPanel.transform, "CREDITS", 38, Color.white);
        CreateBodyText(creditsPanel.transform,
            "RUN CRAB RUN\n\nDesign & Development Team\n\nArt: Annabelle\nUI & Level Design: Emily\nCreature AI: Rener\nSystems & Polish: Caleb\n\nMade for Brackeys Game Jam 2026");
        CreateButton(creditsPanel.transform, "Back", ShowMain);
    }

    private GameObject CreatePanel(string name)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(root.transform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(PanelWidth, 0f);

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.1f, 0.18f, 0.19f, 0.98f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(36, 36, 30, 30);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return panel;
    }

    private void CreateTitle(Transform parent, string value, int size, Color color)
    {
        Text text = CreateText(parent, "Title", value, size, TextAnchor.MiddleCenter, color);
        SetLayoutHeight(text.gameObject, size + 34f);
    }

    private void CreateSubtitle(Transform parent, string value)
    {
        Text text = CreateText(parent, "Subtitle", value, 20, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.83f));
        SetLayoutHeight(text.gameObject, 46f);
    }

    private void CreateBodyText(Transform parent, string value)
    {
        Text text = CreateText(parent, "Body", value, 20, TextAnchor.MiddleCenter, new Color(0.9f, 0.93f, 0.91f));
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        SetLayoutHeight(text.gameObject, 250f);
    }

    private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = new Color(0.22f, 0.43f, 0.42f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        button.colors = colors;
        button.onClick.AddListener(action);

        SetLayoutHeight(buttonObject, ButtonHeight);
        Text text = CreateText(buttonObject.transform, "Label", label, 24, TextAnchor.MiddleCenter, Color.white);
        Stretch(text.rectTransform);
    }

    private Slider CreateSlider(Transform parent, string label, float value, out Text valueText, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject group = new GameObject(label, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        group.transform.SetParent(parent, false);
        SetLayoutHeight(group, 90f);

        VerticalLayoutGroup layout = group.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        Text labelText = CreateText(group.transform, "Label", label, 20, TextAnchor.MiddleLeft, Color.white);
        SetLayoutHeight(labelText.gameObject, 28f);

        Slider slider = DefaultControls.CreateSlider(new DefaultControls.Resources()).GetComponent<Slider>();
        slider.name = label + " Slider";
        slider.transform.SetParent(group.transform, false);
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        slider.onValueChanged.AddListener(onChanged);
        SetLayoutHeight(slider.gameObject, 28f);

        valueText = CreateText(group.transform, "Value", Percentage(value), 16, TextAnchor.MiddleRight, new Color(0.78f, 0.84f, 0.83f));
        SetLayoutHeight(valueText.gameObject, 20f);
        return slider;
    }

    private Text CreateText(Transform parent, string name, string value, int size, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        return text;
    }

    private static Image AddImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private void SetOnlyPanelActive(GameObject activePanel)
    {
        mainPanel.SetActive(activePanel == mainPanel);
        howToPlayPanel.SetActive(activePanel == howToPlayPanel);
        settingsPanel.SetActive(activePanel == settingsPanel);
        creditsPanel.SetActive(activePanel == creditsPanel);
    }

    private static void SetLayoutHeight(GameObject gameObject, float height)
    {
        LayoutElement element = gameObject.GetComponent<LayoutElement>();
        if (element == null) element = gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static string Percentage(float value) => Mathf.RoundToInt(value * 100f) + "%";

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
