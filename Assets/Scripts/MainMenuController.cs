using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The menu is built at runtime so placeholder visuals remain easy to replace with the
// artist's final work. Layout groups, anchors, and scroll views keep every page usable
// across different game-view sizes without hand-positioned text blocks.
public class MainMenuController : MonoBehaviour
{
    private enum Page { Main, HowToPlay, Settings, Credits }

    private sealed class MenuOption
    {
        public string normalText;
        public string selectedText;
        public Text label;
        public Button button;
        public RectTransform visualTransform;
        public float selectedScale;
    }

    [Tooltip("The gameplay scene loaded by the Play option.")]
    public string gameplaySceneName = "SampleScene";

    private readonly List<MenuOption> mainOptions = new List<MenuOption>();

    private Font font;
    private GameObject root;
    private GameObject mainPage;
    private GameObject howToPlayPage;
    private GameObject settingsPage;
    private GameObject creditsPage;
    private Text musicValueText;
    private Text effectsValueText;
    private int selectedOption;
    private Page activePage;

    // A lightweight sketchbook palette: cream paper, charcoal pencil, and pastel ink.
    private readonly Color backgroundColor = new Color(0.98f, 0.94f, 0.84f, 1f);
    private readonly Color primaryTextColor = new Color(0.17f, 0.14f, 0.12f, 1f);
    private readonly Color mutedTextColor = new Color(0.42f, 0.35f, 0.30f, 1f);
    private readonly Color accentColor = new Color(0.78f, 0.31f, 0.25f, 1f);
    private readonly Color pastelTrackColor = new Color(0.80f, 0.71f, 0.62f, 1f);
    private readonly Color pastelFillColor = new Color(0.94f, 0.56f, 0.39f, 1f);

    private void Awake()
    {
        font = LoadComicSans();
        EnsureMenuCamera();
        EnsureEventSystem();
        BuildMenu();
        Time.timeScale = 1f;
        ShowMain();
    }

    private void Update()
    {
        AnimateMainSelection();

        if (activePage != Page.Main &&
            (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)))
        {
            ShowMain();
        }
    }

    public void StartRun()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void ShowMain()
    {
        root.SetActive(true);
        SetActivePage(Page.Main);
        SetMainSelection(0);
    }

    public void ShowHowToPlay() => SetActivePage(Page.HowToPlay);
    public void ShowSettings() => SetActivePage(Page.Settings);
    public void ShowCredits() => SetActivePage(Page.Credits);

    public void SetMusicVolume(float value)
    {
        GameAudioSettings.SetMusicVolume(value);
        if (musicValueText != null) musicValueText.text = Percentage(value);
    }

    public void SetEffectsVolume(float value)
    {
        GameAudioSettings.SetEffectsVolume(value);
        if (effectsValueText != null) effectsValueText.text = Percentage(value);
    }

    private void BuildMenu()
    {
        root = new GameObject("Start Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image background = CreateImage(root.transform, "Background", backgroundColor);
        Stretch(background.rectTransform);

        mainPage = CreateFullPage("Main Page");
        howToPlayPage = CreateFullPage("How To Play Page");
        settingsPage = CreateFullPage("Settings Page");
        creditsPage = CreateFullPage("Credits Page");

        BuildMainPage();
        BuildHowToPlayPage();
        BuildSettingsPage();
        BuildCreditsPage();
    }

    private void BuildMainPage()
    {
        GameObject column = CreateCenteredColumn(mainPage.transform, "Main Menu Column", 760f);
        VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(30, 30, 16, 16);

        Text title = CreateText(column.transform, "Title", "RUN CRAB RUN", 84, TextAnchor.MiddleCenter, accentColor);
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        title.verticalOverflow = VerticalWrapMode.Overflow;
        SetLayoutHeight(title.gameObject, 112f);

        GameObject choices = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        choices.transform.SetParent(column.transform, false);
        SetLayoutHeight(choices, 254f);
        VerticalLayoutGroup choicesLayout = choices.GetComponent<VerticalLayoutGroup>();
        choicesLayout.childAlignment = TextAnchor.MiddleLeft;
        choicesLayout.childControlWidth = true;
        choicesLayout.childControlHeight = true;
        choicesLayout.childForceExpandWidth = true;
        choicesLayout.childForceExpandHeight = false;
        choicesLayout.spacing = 4f;

        AddMainOption(choices.transform, "PLAY", 46, 1.16f, StartRun);
        AddMainOption(choices.transform, "HOW TO PLAY", 30, 1.13f, ShowHowToPlay);
        AddMainOption(choices.transform, "SETTINGS", 30, 1.13f, ShowSettings);
        AddMainOption(choices.transform, "CREDITS", 30, 1.13f, ShowCredits);
    }

    private void BuildHowToPlayPage()
    {
        CreatePageTitle(howToPlayPage.transform, "HOW TO PLAY");
        Transform content = CreateScrollableContent(howToPlayPage.transform, "How To Play Content");

        Transform controls = CreateSection(content, "Controls Section", "CONTROLS");
        AddInfoRow(controls, "Move", "WASD / Arrow Keys");
        AddInfoRow(controls, "Sprint", "Left Shift");

        Transform survival = CreateSection(content, "Survival Section", "SURVIVAL");
        AddParagraph(survival, "Find berries to restore hunger.");
        AddParagraph(survival, "Stand in the stream to restore thirst.");
        AddParagraph(survival, "Animals seek water. Following one may save you—but getting close may expose an Imposter.");
        AddParagraph(survival, "Hide in bushes to escape ground predators.");

        Transform danger = CreateSection(content, "Danger Section", "DANGER");
        AddParagraph(danger, "Deer and sheep may look harmless, but any one of them could be an Imposter. Escape for long enough and it may disguise itself again.");
        AddParagraph(danger, "You cannot fight—run, hide, and survive.");
        CreateBackChoice(howToPlayPage.transform, ShowMain);
    }

    private void BuildSettingsPage()
    {
        CreatePageTitle(settingsPage.transform, "SETTINGS");
        Transform content = CreateCenteredPageColumn(settingsPage.transform, "Settings Content", 720f, 26f);

        // The page column owns only the two major sections. Their rows and sliders
        // are contained by the corresponding nested section layout.
        Transform controls = CreateSection(content, "Controls Section", "CONTROLS");
        AddInfoRow(controls, "Move", "WASD / Arrow Keys");
        AddInfoRow(controls, "Sprint", "Left Shift");

        Transform audio = CreateSection(content, "Audio Section", "AUDIO");
        CreateVolumeControl(audio, "Music Volume", GameAudioSettings.MusicVolume, out musicValueText, SetMusicVolume);
        CreateVolumeControl(audio, "Effects Volume", GameAudioSettings.EffectsVolume, out effectsValueText, SetEffectsVolume);
        CreateBackChoice(settingsPage.transform, ShowMain);
    }

    private void BuildCreditsPage()
    {
        CreatePageTitle(creditsPage.transform, "CREDITS");
        Transform content = CreateCenteredPageColumn(creditsPage.transform, "Credits Content", 620f, 24f);

        // Every role/name pair is a single child of Credits Content. The outer
        // column therefore spaces entries without ever splitting a pair apart.
        AddCredit(content, "Art", "Annabelle");
        AddCredit(content, "UI & Level Design", "Emily");
        AddCredit(content, "Creature AI", "Rener");
        AddCredit(content, "Systems & Polish", "Caleb");
        AddCredit(content, "Heartbeat Sound", "Soundious");
        CreateBackChoice(creditsPage.transform, ShowMain);
    }

    private void AddMainOption(Transform parent, string caption, int baseSize, float selectedScale, UnityEngine.Events.UnityAction action)
    {
        GameObject optionObject = new GameObject(caption + " Option", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(EventTrigger));
        optionObject.transform.SetParent(parent, false);
        Image hitArea = optionObject.GetComponent<Image>();
        hitArea.color = Color.clear; // Pointer target only: no visible button rectangle.

        Button button = optionObject.GetComponent<Button>();
        button.targetGraphic = hitArea;
        button.onClick.AddListener(action);
        SetLayoutHeight(optionObject, caption == "PLAY" ? 82f : 58f);

        Text label = CreateText(optionObject.transform, "Label", caption, baseSize, TextAnchor.MiddleLeft, mutedTextColor);
        Stretch(label.rectTransform);
        label.rectTransform.pivot = new Vector2(0f, 0.5f);

        MenuOption option = new MenuOption
        {
            normalText = "  " + caption,
            selectedText = "> " + caption,
            label = label,
            button = button,
            visualTransform = label.rectTransform,
            selectedScale = selectedScale
        };
        int index = mainOptions.Count;
        mainOptions.Add(option);
        EventTrigger trigger = optionObject.GetComponent<EventTrigger>();
        AddPointerEnter(trigger, () => SetMainSelection(index));
        AddSelect(trigger, () => selectedOption = index);
    }

    private void SetMainSelection(int index)
    {
        if (mainOptions.Count == 0) return;
        selectedOption = Mathf.Clamp(index, 0, mainOptions.Count - 1);
        EventSystem.current?.SetSelectedGameObject(mainOptions[selectedOption].button.gameObject);
    }

    private void AnimateMainSelection()
    {
        if (activePage != Page.Main) return;

        for (int i = 0; i < mainOptions.Count; i++)
        {
            MenuOption option = mainOptions[i];
            bool selected = i == selectedOption;
            Color targetColor = selected ? primaryTextColor : mutedTextColor;
            float targetScale = selected ? option.selectedScale : 1f;
            float blend = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);

            // Text size and layout dimensions stay fixed. Scaling only the label's
            // transform avoids a full layout rebuild whenever selection changes.
            option.visualTransform.localScale = Vector3.Lerp(
                option.visualTransform.localScale,
                Vector3.one * targetScale,
                blend);
            option.label.color = Color.Lerp(option.label.color, targetColor, blend);

            string targetText = selected ? option.selectedText : option.normalText;
            if (option.label.text != targetText) option.label.text = targetText;
        }
    }

    private void SetActivePage(Page page)
    {
        activePage = page;
        mainPage.SetActive(page == Page.Main);
        howToPlayPage.SetActive(page == Page.HowToPlay);
        settingsPage.SetActive(page == Page.Settings);
        creditsPage.SetActive(page == Page.Credits);
    }

    private GameObject CreateFullPage(string name)
    {
        GameObject page = new GameObject(name, typeof(RectTransform));
        page.transform.SetParent(root.transform, false);
        Stretch(page.GetComponent<RectTransform>());
        return page;
    }

    private GameObject CreateCenteredColumn(Transform parent, string name, float width)
    {
        GameObject column = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        column.transform.SetParent(parent, false);
        RectTransform rect = column.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 0f);

        VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        column.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return column;
    }

    private Transform CreateCenteredPageColumn(Transform page, string name, float width, float spacing)
    {
        GameObject column = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        column.transform.SetParent(page, false);

        RectTransform rect = column.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -8f);
        rect.sizeDelta = new Vector2(width, 0f);

        VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = spacing;
        layout.padding = new RectOffset(0, 0, 4, 4);

        column.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return column.transform;
    }

    private Text CreatePageTitle(Transform parent, string title)
    {
        Text text = CreateText(parent, "Title", title, 56, TextAnchor.MiddleCenter, accentColor);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -30f);
        rect.sizeDelta = new Vector2(900f, 64f);
        return text;
    }

    private Transform CreateScrollableContent(Transform page, string name)
    {
        GameObject viewport = new GameObject(name + " Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(page, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(270f, 92f);
        viewportRect.offsetMax = new Vector2(-270f, -108f);
        viewport.GetComponent<Image>().color = Color.clear;

        GameObject contentObject = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 4, 16);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = viewport.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
        return contentObject.transform;
    }

    // Sections keep related controls together so their compact spacing survives
    // resolution changes and content growth.
    private Transform CreateSection(Transform parent, string name, string heading)
    {
        Transform section = CreateCompactGroup(parent, name, 6f);
        AddSectionHeader(section, heading);
        return section;
    }

    private Transform CreateCompactGroup(Transform parent, string name, float spacing)
    {
        GameObject group = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        group.transform.SetParent(parent, false);

        VerticalLayoutGroup layout = group.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = spacing;

        group.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return group.transform;
    }

    private void AddSectionHeader(Transform parent, string value)
    {
        AddDisplayText(parent, value, 30, TextAnchor.MiddleLeft, accentColor, 34f);
    }

    private void AddParagraph(Transform parent, string value)
    {
        Text text = AddDisplayText(parent, value, 22, TextAnchor.MiddleLeft, primaryTextColor, 34f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void AddInfoRow(Transform parent, string leftValue, string rightValue)
    {
        GameObject row = new GameObject(leftValue + " Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        SetLayoutHeight(row, 32f);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Text left = CreateText(row.transform, "Name", leftValue, 22, TextAnchor.MiddleLeft, primaryTextColor);
        AddLayoutWidth(left.gameObject, 0f, 1f);
        Text right = CreateText(row.transform, "Value", rightValue, 22, TextAnchor.MiddleRight, primaryTextColor);
        AddLayoutWidth(right.gameObject, 280f, 0f);
    }

    private void CreateVolumeControl(Transform parent, string label, float value, out Text valueText, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject group = new GameObject(label + " Row", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        group.transform.SetParent(parent, false);
        SetLayoutHeight(group, 68f);
        VerticalLayoutGroup layout = group.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;

        GameObject labelRow = new GameObject("Label Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        labelRow.transform.SetParent(group.transform, false);
        SetLayoutHeight(labelRow, 28f);
        HorizontalLayoutGroup rowLayout = labelRow.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;

        Text labelText = CreateText(labelRow.transform, "Label", label, 22, TextAnchor.MiddleLeft, primaryTextColor);
        AddLayoutWidth(labelText.gameObject, 0f, 1f);
        valueText = CreateText(labelRow.transform, "Value", Percentage(value), 22, TextAnchor.MiddleRight, accentColor);
        AddLayoutWidth(valueText.gameObject, 96f, 0f);

        Slider slider = CreateSlider(group.transform, label + " Slider", value);
        slider.onValueChanged.AddListener(onChanged);
    }

    private Slider CreateSlider(Transform parent, string name, float value)
    {
        // Layout components control only this outer object. Its visual children use
        // slider-owned anchors, preventing fill/handle images from being stretched.
        GameObject sliderObject = new GameObject(name, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        sliderObject.transform.SetParent(parent, false);
        SetLayoutHeight(sliderObject, 30f);
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        Image background = CreateImage(sliderObject.transform, "Track", pastelTrackColor);
        RectTransform track = background.rectTransform;
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.offsetMin = new Vector2(12f, -4f);
        track.offsetMax = new Vector2(-12f, 4f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.offsetMin = new Vector2(12f, -4f);
        fillAreaRect.offsetMax = new Vector2(-12f, 4f);
        Image fill = CreateImage(fillArea.transform, "Fill", pastelFillColor);
        Stretch(fill.rectTransform);
        slider.fillRect = fill.rectTransform;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0.5f);
        handleAreaRect.anchorMax = new Vector2(1f, 0.5f);
        handleAreaRect.offsetMin = new Vector2(12f, -12f);
        handleAreaRect.offsetMax = new Vector2(-12f, 12f);
        Image handle = CreateImage(handleArea.transform, "Handle", primaryTextColor);
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(20f, 26f);
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.SetValueWithoutNotify(value);
        return slider;
    }

    private void AddCredit(Transform parent, string role, string name)
    {
        Transform credit = CreateCompactGroup(parent, role + " Credit Entry", 6f);
        AddDisplayText(credit, role, 23, TextAnchor.MiddleCenter, mutedTextColor, 26f);
        AddDisplayText(credit, name, 29, TextAnchor.MiddleCenter, primaryTextColor, 32f);
    }

    private Text AddDisplayText(Transform parent, string value, int size, TextAnchor alignment, Color color, float height)
    {
        Text text = CreateText(parent, value, value, size, alignment, color);
        SetLayoutHeight(text.gameObject, height);
        return text;
    }

    private void CreateBackChoice(Transform parent, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button), typeof(EventTrigger));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 34f);
        rect.sizeDelta = new Vector2(250f, 46f);
        buttonObject.GetComponent<Image>().color = Color.clear;
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(action);
        Text label = CreateText(buttonObject.transform, "Label", "> BACK", 27, TextAnchor.MiddleCenter, primaryTextColor);
        Stretch(label.rectTransform);
        EventTrigger trigger = buttonObject.GetComponent<EventTrigger>();
        AddPointerEnter(trigger, () => label.color = accentColor);
    }

    private Text CreateText(Transform parent, string objectName, string value, int size, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
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

    private static void AddPointerEnter(EventTrigger trigger, UnityEngine.Events.UnityAction callback)
    {
        if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => callback.Invoke());
        trigger.triggers.Add(entry);
    }

    private static void AddSelect(EventTrigger trigger, UnityEngine.Events.UnityAction callback)
    {
        if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
        entry.callback.AddListener(_ => callback.Invoke());
        trigger.triggers.Add(entry);
    }

    private static void SetLayoutHeight(GameObject gameObject, float height)
    {
        LayoutElement element = gameObject.GetComponent<LayoutElement>();
        if (element == null) element = gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
    }

    private static void AddLayoutWidth(GameObject gameObject, float preferredWidth, float flexibleWidth)
    {
        LayoutElement element = gameObject.GetComponent<LayoutElement>();
        if (element == null) element = gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = preferredWidth;
        element.flexibleWidth = flexibleWidth;
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

        // This prevents a Unity 6 built-in-font exception on machines that do not
        // have Comic Sans installed, while keeping the menu usable for that player.
        Debug.LogWarning("Comic Sans MS is not installed; Main Menu is using Unity's safe fallback font.");
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static string Percentage(float value) => Mathf.RoundToInt(value * 100f) + "%";

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void EnsureMenuCamera()
    {
        if (Camera.main != null) return;

        GameObject cameraObject = new GameObject("Main Menu Camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera menuCamera = cameraObject.GetComponent<Camera>();
        menuCamera.clearFlags = CameraClearFlags.SolidColor;
        menuCamera.backgroundColor = new Color(0.98f, 0.94f, 0.84f, 1f);
        menuCamera.orthographic = true;
    }
}
