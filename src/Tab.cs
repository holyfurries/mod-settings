using System;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne;
using Il2CppScheduleOne.UI.MainMenu;
using Il2CppScheduleOne.UI.Settings;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace ModSettings;

internal static class Tab
{
    private const string tab_name = "ModSettingsTab";
    private const int category_count_max = 16;
    private readonly record struct Templates(GameObject panel, SettingsToggle? toggle, SettingsSlider? slider, SettingsDropdown? dropdown, Button? keybind, TextMeshProUGUI heading, TextMeshProUGUI label);
    private sealed record KeyCapture(Setting setting, SettingsScreen screen, GameObject row, Button button, TextMeshProUGUI caption, GameObject blocker, int frame, float expires_seconds);
    private static KeyCapture? capture;

    public static void install(HarmonyLib.Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(SettingsScreen), nameof(SettingsScreen.Awake)),
            postfix: new HarmonyMethod(typeof(Tab), nameof(add_tab)));
    }

    private static void add_tab(SettingsScreen __instance)
    {
        try { build(__instance); }
        catch (Exception error) { MelonLogger.Warning($"Mod Settings: Tab unavailable, edit UserData/MelonPreferences.cfg instead: {error}"); }
    }

    private static void build(SettingsScreen screen)
    {
        Registry registry = Settings.registry;
        if (registry.count == 0) return;
        Il2CppReferenceArray<SettingsScreen.SettingsCategory> categories = screen.Categories;
        if (categories == null || categories.Length == 0 || categories.Length >= category_count_max) throw new InvalidOperationException("Unexpected settings categories.");
        SettingsScreen.SettingsCategory last = categories[categories.Length - 1];
        if (last.Toggle == null || last.Panel == null) throw new InvalidOperationException("Settings category is incomplete.");
        if (last.Toggle.transform.parent.Find(tab_name) != null) return;
        Templates templates = find_templates(categories);

        GameObject panel = UnityEngine.Object.Instantiate(templates.panel, templates.panel.transform.parent);
        panel.name = "ModSettings";
        panel.SetActive(false);
        VerticalLayoutGroup layout = SettingsLayout.prepare_panel(panel, templates.heading.transform.parent.GetComponent<VerticalLayoutGroup>());
        ScrollRect scroll = panel.GetComponentInChildren<ScrollRect>(true) ?? throw new InvalidOperationException("Settings panel has no scroll view.");
        RectTransform content = scroll.content ?? throw new InvalidOperationException("Settings scroll view has no content.");
        Transform rows = layout.transform;
        for (int i = 0; i < rows.childCount; i++) rows.GetChild(i).gameObject.SetActive(false);
        float row_width = rows.childCount > 0 ? rows.GetChild(0).GetComponent<RectTransform>().sizeDelta.x : 450f;
        TextMeshProUGUI label_template = templates.heading;

        for (int i = 0; i < registry.count; i++)
        {
            Setting setting = registry.at(i);
            if (registry.starts_group(i)) add_header(rows, label_template, setting.mod, row_width, i == 0);
            switch (setting.kind)
            {
                case ControlKind.Toggle: add_toggle(rows, templates.toggle, setting); break;
                case ControlKind.Slider: add_slider(rows, templates.slider, setting); break;
                case ControlKind.Dropdown: add_dropdown(rows, templates.dropdown, setting); break;
                case ControlKind.Keybind: add_keybind(screen, rows, templates.dropdown, templates.keybind, setting); break;
                default: throw new InvalidOperationException("Unknown control kind.");
            }
        }

        for (int i = 0; i < rows.childCount; i++)
        {
            GameObject row = rows.GetChild(i).gameObject;
            if (!row.activeSelf) continue;
            SettingsLayout.typography(row, templates.label);
            ContentSizeFitter? fitter = row.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            LayoutElement element = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
            element.minWidth = 0;
            element.preferredWidth = -1;
            element.flexibleWidth = 1;
        }

        Toggle tab = UnityEngine.Object.Instantiate(last.Toggle.gameObject, last.Toggle.transform.parent).GetComponent<Toggle>();
        tab.name = tab_name;
        tab.SetIsOnWithoutNotify(false);
        TextMeshProUGUI? tab_label = tab.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tab_label != null) tab_label.text = "Mods";
        int index = categories.Length;
        var extended = new Il2CppReferenceArray<SettingsScreen.SettingsCategory>(index + 1);
        for (int i = 0; i < index; i++) extended[i] = categories[i];
        extended[index] = new SettingsScreen.SettingsCategory { Toggle = tab, Panel = panel };
        screen.Categories = extended;
        tab.onValueChanged.AddListener(new Action<bool>(selected =>
        {
            if (!selected) return;
            screen.ShowCategory(index);
            for (int i = 0; i < registry.count; i++) registry.at(i).show(registry.at(i).read());
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            SettingsLayout.fit_columns(rows, registry);
        }));
        fit_tabs(screen.GetComponent<RectTransform>(), tab.transform.parent);
    }

    private static Templates find_templates(Il2CppReferenceArray<SettingsScreen.SettingsCategory> categories)
    {
        GameObject? panel = null;
        int panel_kind_count = -1;
        SettingsToggle? toggle = null;
        SettingsSlider? slider = null;
        SettingsDropdown? dropdown = null;
        Button? keybind = null;
        TextMeshProUGUI? heading = null;
        TextMeshProUGUI? label = null;
        for (int i = 0; i < categories.Length; i++)
        {
            GameObject candidate = categories[i].Panel;
            if (candidate == null) continue;
            SettingsToggle? candidate_toggle = candidate.GetComponentInChildren<SettingsToggle>(true);
            SettingsSlider? candidate_slider = candidate.GetComponentInChildren<SettingsSlider>(true);
            SettingsDropdown? candidate_dropdown = candidate.GetComponentInChildren<SettingsDropdown>(true);
            toggle ??= candidate_toggle;
            slider ??= candidate_slider;
            dropdown ??= candidate_dropdown;
            Keybinder? candidate_keybind = candidate.GetComponentInChildren<Keybinder>(true);
            if (candidate_keybind != null)
            {
                keybind ??= candidate_keybind.GetComponentInChildren<Button>(true);
                var texts = candidate.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int j = 0; j < Math.Min(texts.Length, 256); j++)
                {
                    if (texts[j].text == "Settings") { heading ??= texts[j]; break; }
                }
            }
            TextMeshProUGUI? category_label = categories[i].Toggle.GetComponentInChildren<TextMeshProUGUI>(true);
            if (category_label != null && category_label.text == "Gamepad")
            {
                var texts = candidate.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int j = 0; j < Math.Min(texts.Length, 256); j++)
                {
                    TextMeshProUGUI text = texts[j];
                    if (text.text == "Settings") heading = text;
                    if (text.name.StartsWith("Option Name", StringComparison.Ordinal) || text.name == "Label") label ??= text;
                }
            }
            if (candidate.GetComponentInChildren<VerticalLayoutGroup>(true) == null) continue;
            int kind_count = (candidate_toggle != null ? 1 : 0) + (candidate_slider != null ? 1 : 0) + (candidate_dropdown != null ? 1 : 0);
            if (kind_count <= panel_kind_count) continue;
            panel = candidate;
            panel_kind_count = kind_count;
            toggle = candidate_toggle ?? toggle;
            slider = candidate_slider ?? slider;
            dropdown = candidate_dropdown ?? dropdown;
        }
        if (panel == null) throw new InvalidOperationException("No scrolling settings panel to copy.");
        if (heading == null) throw new InvalidOperationException("No native settings heading to copy.");
        if (label == null) throw new InvalidOperationException("No Gamepad settings label to copy.");
        return new Templates(panel, toggle, slider, dropdown, keybind, heading, label);
    }

    private static void add_header(Transform rows, TextMeshProUGUI label_template, string mod, float row_width, bool first)
    {
        GameObject header = UnityEngine.Object.Instantiate(label_template.gameObject, rows);
        header.name = "Header";
        TextMeshProUGUI text = header.GetComponent<TextMeshProUGUI>();
        text.enableWordWrapping = false;
        text.enableAutoSizing = false;
        Vector4 margin = label_template.margin;
        text.margin = new Vector4(SettingsLayout.label_left, margin.y, margin.z, margin.w);
        text.raycastTarget = false;
        text.text = mod;
        text.ForceMeshUpdate(true, true);
        float height = text.preferredHeight;
        if (!float.IsFinite(height) || height <= 0) throw new InvalidOperationException("Native heading height is invalid.");
        float padding_top = first ? 0 : 20f;
        height += padding_top;
        text.margin = new Vector4(SettingsLayout.label_left, margin.y + padding_top, margin.z, margin.w);
        header.GetComponent<RectTransform>().sizeDelta = new Vector2(row_width, height);
        LayoutElement element = header.GetComponent<LayoutElement>() ?? header.AddComponent<LayoutElement>();
        element.layoutPriority = 2;
        element.minHeight = height;
        element.preferredHeight = height;
        element.flexibleHeight = 0;
    }

    private static void add_toggle(Transform rows, SettingsToggle? template, Setting setting)
    {
        if (template == null) throw new InvalidOperationException("No switch row to copy.");
        GameObject row = UnityEngine.Object.Instantiate(template.gameObject, rows);
        UnityEngine.Object.DestroyImmediate(row.GetComponent<SettingsToggle>());
        UIToggle toggle = row.GetComponent<UIToggle>() ?? throw new InvalidOperationException("Switch row has no UIToggle.");
        toggle.optionName = setting.label;
        name_row(row, setting.label);
        SettingsLayout.toggle(row, toggle);
        toggle.OnChanged.AddListener(new Action<bool>(value => Settings.change(setting, value ? 1f : 0f, Time.unscaledTime)));
        setting.bind(value => { if (toggle != null) toggle.SetStateWithoutNotify(value >= 0.5f); });
        row.SetActive(true);
    }

    private static void add_slider(Transform rows, SettingsSlider? template, Setting setting)
    {
        if (template == null) throw new InvalidOperationException("No slider row to copy.");
        Transform template_row = template.transform.parent.GetComponent<UISlider>() != null ? template.transform.parent : template.transform;
        GameObject row = UnityEngine.Object.Instantiate(template_row.gameObject, rows);
        UnityEngine.Object.DestroyImmediate(row.GetComponentInChildren<SettingsSlider>(true));
        Slider slider = row.GetComponentInChildren<Slider>(true) ?? throw new InvalidOperationException("Slider row has no Slider.");
        UISlider? option = row.GetComponentInChildren<UISlider>(true);
        TextMeshProUGUI? value_text = option?.valueText;
        if (option != null)
        {
            option.optionName = setting.label;
            option.canUpdateValueText = false;
            option.stepSize = setting.whole_numbers ? 1f : (setting.maximum - setting.minimum) / 20f;
        }
        name_row(row, setting.label);
        slider.minValue = setting.minimum;
        slider.maxValue = setting.maximum;
        slider.wholeNumbers = setting.whole_numbers;
        SettingsLayout.slider(row, slider, value_text);
        if (value_text != null)
        {
            value_text.gameObject.SetActive(true);
            value_text.alpha = 1f;
            value_text.enableWordWrapping = false;
        }
        slider.onValueChanged.AddListener(new Action<float>(value =>
        {
            Settings.change(setting, value, Time.unscaledTime);
            if (value_text != null) value_text.text = setting.format(value);
        }));
        setting.bind(value =>
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(value);
            if (value_text != null) value_text.text = setting.format(value);
        });
        row.SetActive(true);
    }

    private static void add_dropdown(Transform rows, SettingsDropdown? template, Setting setting)
    {
        if (template == null) throw new InvalidOperationException("No dropdown row to copy.");
        GameObject row = UnityEngine.Object.Instantiate(template.transform.parent.gameObject, rows);
        UnityEngine.Object.DestroyImmediate(row.GetComponentInChildren<SettingsDropdown>(true));
        TMP_Dropdown dropdown = row.GetComponentInChildren<TMP_Dropdown>(true) ?? throw new InvalidOperationException("Dropdown row has no TMP_Dropdown.");
        name_row(row, setting.label);
        SettingsLayout.dropdown(row, dropdown);
        var options = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (string option in setting.options) options.Add(option);
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.onValueChanged.AddListener(new Action<int>(value => Settings.change(setting, value, Time.unscaledTime)));
        setting.bind(value => { if (dropdown != null) dropdown.SetValueWithoutNotify((int)value); });
        row.SetActive(true);
    }

    private static void add_keybind(SettingsScreen screen, Transform rows, SettingsDropdown? template, Button? button_template, Setting setting)
    {
        if (template == null) throw new InvalidOperationException("No keybind row to copy.");
        if (button_template == null) throw new InvalidOperationException("No native keybind button to copy.");
        GameObject row = UnityEngine.Object.Instantiate(template.transform.parent.gameObject, rows);
        UnityEngine.Object.DestroyImmediate(row.GetComponentInChildren<SettingsDropdown>(true));
        TMP_Dropdown dropdown = row.GetComponentInChildren<TMP_Dropdown>(true) ?? throw new InvalidOperationException("Keybind row has no dropdown.");
        TextMeshProUGUI caption = dropdown.captionText.TryCast<TextMeshProUGUI>() ?? throw new InvalidOperationException("Keybind row has no caption.");
        GameObject control = dropdown.gameObject;
        dropdown.Hide();
        UnityEngine.Object.DestroyImmediate(dropdown);
        Transform? arrow = control.transform.Find("Arrow");
        if (arrow != null) arrow.gameObject.SetActive(false);
        Button button = control.AddComponent<Button>();
        button.targetGraphic = control.GetComponent<Image>();
        button.transition = button_template.transition;
        button.colors = button_template.colors;
        button.spriteState = button_template.spriteState;
        Image? background = button.targetGraphic.TryCast<Image>();
        Image? native_background = button_template.targetGraphic.TryCast<Image>();
        if (background != null && native_background != null)
        {
            background.sprite = native_background.sprite;
            background.type = native_background.type;
            background.color = native_background.color;
        }
        caption.gameObject.SetActive(true);
        caption.enabled = true;
        caption.color = Color.white;
        caption.alpha = 1f;
        caption.alignment = TextAlignmentOptions.Midline;
        caption.raycastTarget = false;
        caption.enableAutoSizing = true;
        caption.fontSizeMax = caption.fontSize;
        caption.fontSizeMin = caption.fontSizeMax * 0.75f;
        TextMeshProUGUI? native_caption = button_template.GetComponentInChildren<TextMeshProUGUI>(true);
        if (native_caption != null)
        {
            caption.font = native_caption.font;
            caption.fontSizeMax = native_caption.fontSize;
            caption.fontSizeMin = native_caption.fontSize * 0.75f;
            caption.fontStyle = native_caption.fontStyle;
            caption.color = native_caption.color;
        }
        caption.overflowMode = TextOverflowModes.Ellipsis;
        caption.margin = new Vector4(12f, 0, 12f, 0);
        caption.rectTransform.anchorMin = Vector2.zero;
        caption.rectTransform.anchorMax = Vector2.one;
        caption.rectTransform.offsetMin = Vector2.zero;
        caption.rectTransform.offsetMax = Vector2.zero;
        caption.text = setting.format(setting.read());
        name_row(row, setting.label);
        SettingsLayout.button(row, button, button_template.GetComponent<RectTransform>().rect.size);
        button.onClick.AddListener(new Action(() =>
        {
            if (capture?.row == row) return;
            try
            {
                cancel_capture();
                Canvas canvas = row.GetComponentInParent<Canvas>() ?? throw new InvalidOperationException("Keybind row has no canvas.");
                var blocker = new GameObject("KeybindCapture", new[] { Il2CppType.Of<RectTransform>() });
                capture = new KeyCapture(setting, screen, row, button, caption, blocker, Time.frameCount, Time.unscaledTime + 10f);
                blocker.transform.SetParent(canvas.transform, false);
                RectTransform rect = blocker.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                Image image = blocker.AddComponent<Image>();
                image.color = new Color(0, 0, 0, 0.05f);
                image.raycastTarget = true;
                Button focus = blocker.AddComponent<Button>();
                focus.targetGraphic = image;
                focus.navigation = new Navigation { mode = Navigation.Mode.None };
                focus.Select();
                name_row(row, "Esc cancels · Backspace clears");
                caption.text = "Press a key…";
            }
            catch (Exception error)
            {
                cancel_capture();
                MelonLogger.Warning($"Mod Settings: Could not start key capture: {error.Message}");
            }
        }));
        setting.bind(value =>
        {
            if (caption != null && capture?.caption != caption) caption.text = setting.format(value);
        });
        row.SetActive(true);
    }

    public static void update_capture()
    {
        KeyCapture? active = capture;
        if (active == null) return;
        try
        {
            if (active.screen == null || !active.screen.IsOpen || active.row == null || !active.row.activeInHierarchy ||
                !Application.isFocused || Time.unscaledTime >= active.expires_seconds)
            {
                cancel_capture();
                return;
            }
            if (Time.frameCount <= active.frame) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                cancel_capture();
                return;
            }
            int index = Input.GetKeyDown(KeyCode.Backspace) ? 0 : -1;
            if (index < 0)
            {
                for (int i = 1; i < Settings.key_codes.Length; i++)
                {
                    if (!Input.GetKeyDown(Settings.key_codes[i])) continue;
                    index = i;
                    break;
                }
            }
            if (index < 0) return;
            cancel_capture();
            Settings.change(active.setting, index, Time.unscaledTime);
        }
        catch (Exception error)
        {
            cancel_capture();
            MelonLogger.Warning($"Mod Settings: Could not capture key: {error.Message}");
        }
    }

    public static void cancel_capture()
    {
        KeyCapture? active = capture;
        capture = null;
        if (active == null) return;
        if (active.blocker != null) UnityEngine.Object.Destroy(active.blocker);
        if (active.row != null)
        {
            name_row(active.row, active.setting.label);
            if (active.screen != null && active.screen.IsOpen && active.row.activeInHierarchy && active.button != null) active.button.Select();
        }
        active.setting.show(active.setting.read());
    }

    private static void name_row(GameObject row, string label)
    {
        row.name = label;
        foreach (TextMeshProUGUI text in row.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.transform.parent != row.transform) continue;
            if (text.name.StartsWith("Option Name", StringComparison.Ordinal) || text.name == "Label")
            {
                text.text = label;
            }
            else if (text.name.StartsWith("Label (", StringComparison.Ordinal)) text.gameObject.SetActive(false);
        }
        SettingsLayout.label(row);
    }

    private static void fit_tabs(RectTransform screen, Transform tabs)
    {
        const float side_margin = 21f;
        const float spacing = 5f;
        int tab_count = 0;
        for (int i = 0; i < tabs.childCount; i++)
        {
            if (tabs.GetChild(i).gameObject.activeSelf) tab_count++;
        }
        if (tab_count == 0 || tab_count > category_count_max) throw new InvalidOperationException("Unexpected settings tab count.");
        float width_max = (screen.rect.width - side_margin * 2f - spacing * (tab_count - 1)) / tab_count;
        if (!float.IsFinite(width_max) || width_max < 30f) throw new InvalidOperationException("Settings tabs do not fit.");
        for (int i = 0; i < tabs.childCount; i++)
        {
            RectTransform rect = tabs.GetChild(i).GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(Math.Min(rect.sizeDelta.x, width_max), rect.sizeDelta.y);
            TextMeshProUGUI? label = rect.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null || label.enableAutoSizing) continue;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = 6f;
            label.enableAutoSizing = true;
        }
    }
}
