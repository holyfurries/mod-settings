using System;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne;
using Il2CppScheduleOne.UI.MainMenu;
using Il2CppScheduleOne.UI.Settings;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace ModSettings;

/// Builds the Mods tab by cloning the game's own tab, scrolling panel and option rows, so it keeps the game's look.
internal static class Tab
{
    private const string tab_name = "ModSettingsTab";
    private const int category_count_max = 16;
    private const float header_height = 40f;
    private readonly record struct Templates(GameObject panel, SettingsToggle? toggle, SettingsSlider? slider, SettingsDropdown? dropdown);

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
        Transform rows = panel.GetComponentInChildren<VerticalLayoutGroup>(true).transform;
        for (int i = 0; i < rows.childCount; i++) rows.GetChild(i).gameObject.SetActive(false);
        float row_width = rows.childCount > 0 ? rows.GetChild(0).GetComponent<RectTransform>().sizeDelta.x : 450f;
        TextMeshProUGUI label_template = templates.panel.GetComponentInChildren<TextMeshProUGUI>(true);

        for (int i = 0; i < registry.count; i++)
        {
            Setting setting = registry.at(i);
            if (registry.starts_group(i)) add_header(rows, label_template, setting.mod, row_width);
            switch (setting.kind)
            {
                case ControlKind.Toggle: add_toggle(rows, templates.toggle, setting); break;
                case ControlKind.Slider: add_slider(rows, templates.slider, setting); break;
                case ControlKind.Dropdown: add_dropdown(rows, templates.dropdown, setting); break;
                default: throw new InvalidOperationException("Unknown control kind.");
            }
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
            for (int i = 0; i < registry.count; i++) registry.at(i).show?.Invoke(registry.at(i).clamp(registry.at(i).read()));
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
        return new Templates(panel, toggle, slider, dropdown);
    }

    private static void add_header(Transform rows, TextMeshProUGUI? label_template, string mod, float row_width)
    {
        var header = new GameObject("Header");
        header.transform.SetParent(rows, false);
        header.AddComponent<RectTransform>().sizeDelta = new Vector2(row_width, header_height);
        TextMeshProUGUI text = header.AddComponent<TextMeshProUGUI>();
        if (label_template != null)
        {
            text.font = label_template.font;
            text.fontSize = label_template.fontSize + 2f;
        }
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.margin = new Vector4(10f, 0f, 0f, 4f);
        text.raycastTarget = false;
        text.text = mod;
    }

    private static void add_toggle(Transform rows, SettingsToggle? template, Setting setting)
    {
        if (template == null) throw new InvalidOperationException("No switch row to copy.");
        GameObject row = UnityEngine.Object.Instantiate(template.gameObject, rows);
        UnityEngine.Object.DestroyImmediate(row.GetComponent<SettingsToggle>());
        UIToggle toggle = row.GetComponent<UIToggle>() ?? throw new InvalidOperationException("Switch row has no UIToggle.");
        toggle.optionName = setting.label;
        name_row(row, setting.label);
        toggle.OnChanged.AddListener(new Action<bool>(value => Settings.change(setting, value ? 1f : 0f, Time.unscaledTime)));
        setting.show = value => { if (toggle != null) toggle.SetStateWithoutNotify(value >= 0.5f); };
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
        if (value_text != null)
        {
            value_text.gameObject.SetActive(true);
            value_text.alpha = 1f;
        }
        slider.onValueChanged.AddListener(new Action<float>(value =>
        {
            Settings.change(setting, value, Time.unscaledTime);
            if (value_text != null) value_text.text = setting.format(value);
        }));
        setting.show = value =>
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(value);
            if (value_text != null) value_text.text = setting.format(value);
        };
        row.SetActive(true);
    }

    private static void add_dropdown(Transform rows, SettingsDropdown? template, Setting setting)
    {
        if (template == null) throw new InvalidOperationException("No dropdown row to copy.");
        GameObject row = UnityEngine.Object.Instantiate(template.transform.parent.gameObject, rows);
        UnityEngine.Object.DestroyImmediate(row.GetComponentInChildren<SettingsDropdown>(true));
        TMP_Dropdown dropdown = row.GetComponentInChildren<TMP_Dropdown>(true) ?? throw new InvalidOperationException("Dropdown row has no TMP_Dropdown.");
        name_row(row, setting.label);
        var options = new Il2CppSystem.Collections.Generic.List<string>();
        foreach (string option in setting.options) options.Add(option);
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.onValueChanged.AddListener(new Action<int>(value => Settings.change(setting, value, Time.unscaledTime)));
        setting.show = value => { if (dropdown != null) dropdown.SetValueWithoutNotify((int)value); };
        row.SetActive(true);
    }

    private static void name_row(GameObject row, string label)
    {
        row.name = label;
        foreach (TextMeshProUGUI text in row.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.transform.parent != row.transform) continue;
            if (text.name.StartsWith("Option Name", StringComparison.Ordinal) || text.name == "Label") text.text = label;
            else if (text.name.StartsWith("Label (", StringComparison.Ordinal)) text.gameObject.SetActive(false);
        }
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
