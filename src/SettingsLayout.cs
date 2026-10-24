using System;
using Il2CppInterop.Runtime;
using Il2CppScheduleOne;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModSettings;

internal static class SettingsLayout
{
    public const float label_left = -4f;
    private const float value_gap = 12f;
    private const float row_height = 50f;
    private const float row_gap = 10f;
    private const float control_height_max = 48f;

    public static VerticalLayoutGroup prepare_panel(GameObject panel, VerticalLayoutGroup native_layout)
    {
        if (panel.name != "ModSettings") throw new InvalidOperationException("Only the Mods panel can be laid out.");
        VerticalLayoutGroup layout = panel.GetComponentInChildren<VerticalLayoutGroup>(true) ?? throw new InvalidOperationException("Settings panel has no row layout.");
        ScrollRect? scroll = panel.GetComponentInChildren<ScrollRect>(true);
        if (scroll != null && scroll.content != null) stretch_width(scroll.content);
        stretch_width(layout.GetComponent<RectTransform>());
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.spacing = row_gap;
        RectOffset padding = native_layout.padding;
        layout.padding = new RectOffset(24, 24, padding.top, padding.bottom);
        return layout;
    }

    public static void toggle(GameObject row, UIToggle option)
    {
        prepare_row(row);
        if (option.toggleImage == null) return;
        Transform control = option.toggleImage.transform;
        for (int depth = 0; depth < 16 && control.parent != null && control.parent != row.transform; depth++) control = control.parent;
        if (control.parent != row.transform) throw new InvalidOperationException("Toggle visual has no row parent within 16 levels.");
        Image? background = null;
        Transform parent = option.toggleImage.transform.parent;
        for (int depth = 0; depth < 16 && parent != null && parent != row.transform; depth++)
        {
            background = parent.GetComponent<Image>();
            if (background != null) break;
            parent = parent.parent;
        }
        if (background == null) throw new InvalidOperationException("Toggle indicator has no background image.");
        RectTransform outline = background.rectTransform;
        Vector2 size = outline.rect.size;
        if (size.y > control_height_max) size *= control_height_max / size.y;
        if (background.transform == control)
        {
            var group = new GameObject("ToggleControl", new[] { Il2CppType.Of<RectTransform>() });
            group.transform.SetParent(row.transform, false);
            group.GetComponent<RectTransform>().sizeDelta = size;
            control = group.transform;
        }
        RectTransform container = control.GetComponent<RectTransform>();
        container.SetParent(row.transform, false);
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;
        outline.SetParent(control, false);
        outline.anchorMin = new Vector2(0, 0.5f);
        outline.anchorMax = outline.anchorMin;
        outline.pivot = new Vector2(0, 0.5f);
        outline.anchoredPosition = Vector2.zero;
        outline.sizeDelta = size;
        if (option.buttonText != null)
        {
            RectTransform text = option.buttonText.rectTransform;
            text.SetParent(control, false);
            text.anchorMin = Vector2.zero;
            text.anchorMax = Vector2.one;
            text.offsetMin = new Vector2(size.x + value_gap, 0);
            text.offsetMax = Vector2.zero;
            option.buttonText.alignment = TextAlignmentOptions.MidlineLeft;
        }
    }

    public static void slider(GameObject row, Slider control, TextMeshProUGUI? value)
    {
        prepare_row(row);
        RectTransform rect = control.GetComponent<RectTransform>();
        float height = Math.Min(rect.rect.height, control_height_max);
        LayoutElement native_size = control.GetComponent<LayoutElement>() ?? control.gameObject.AddComponent<LayoutElement>();
        native_size.preferredWidth = rect.rect.width;
        rect.SetParent(row.transform, false);
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(0, -height / 2);
        rect.offsetMax = new Vector2(0, height / 2);
        if (value == null) return;
        RectTransform text = value.rectTransform;
        text.SetParent(row.transform, false);
        text.anchorMin = new Vector2(1, 0.5f);
        text.anchorMax = text.anchorMin;
        text.pivot = new Vector2(0, 0.5f);
        text.anchoredPosition = new Vector2(value_gap, 0);
        text.sizeDelta = new Vector2(96f, row_height);
        value.alignment = TextAlignmentOptions.MidlineLeft;
        value.margin = Vector4.zero;
        value.enableWordWrapping = false;
    }

    public static void dropdown(GameObject row, TMP_Dropdown control)
    {
        button(row, control, control.GetComponent<RectTransform>().rect.size);
    }

    public static void button(GameObject row, Selectable control, Vector2 size)
    {
        prepare_row(row);
        if (!float.IsFinite(size.sqrMagnitude) || size.x <= 0 || size.y <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        RectTransform rect = control.GetComponent<RectTransform>();
        rect.SetParent(row.transform, false);
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(0, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size.x, Math.Min(size.y, control_height_max));
        LayoutElement native_size = control.GetComponent<LayoutElement>() ?? control.gameObject.AddComponent<LayoutElement>();
        native_size.preferredWidth = size.x;
    }

    public static void fit_columns(Transform rows, Registry registry)
    {
        var labels = new TextMeshProUGUI[registry.count];
        var controls = new RectTransform[registry.count];
        var values = new TextMeshProUGUI?[registry.count];
        var widths = new float[registry.count];
        var value_widths = new float[registry.count];
        float label_width_max = 0;
        float control_width_max = 0;
        float row_width = float.PositiveInfinity;
        int count = 0;
        for (int i = 0; i < Math.Min(rows.childCount, 512); i++)
        {
            GameObject row = rows.GetChild(i).gameObject;
            if (!row.activeSelf || row.GetComponent<TextMeshProUGUI>() != null) continue;
            if (count >= registry.count) throw new InvalidOperationException("Unexpected Mods row count.");
            Setting setting = registry.at(count);
            var texts = row.GetComponentsInChildren<TextMeshProUGUI>(true);
            TextMeshProUGUI? label = null;
            for (int j = 0; j < Math.Min(texts.Length, 32); j++)
            {
                if (texts[j].transform.parent != row.transform) continue;
                if (texts[j].name.StartsWith("Option Name", StringComparison.Ordinal) || texts[j].name == "Label") { label = texts[j]; break; }
            }
            if (label == null) throw new InvalidOperationException("Mods row has no label.");
            label.ForceMeshUpdate(true, true);
            label_width_max = Math.Max(label_width_max, label.GetPreferredValues(setting.label).x);
            RectTransform control;
            float width;
            if (setting.kind == ControlKind.Slider)
            {
                Slider slider = row.GetComponentInChildren<Slider>(true);
                control = slider.GetComponent<RectTransform>();
                width = control.GetComponent<LayoutElement>().preferredWidth;
                values[count] = row.GetComponentInChildren<UISlider>(true)?.valueText;
                TextMeshProUGUI? value = values[count];
                if (value != null)
                {
                    value.ForceMeshUpdate(true, true);
                    float bounds_width = Math.Max(value.GetPreferredValues(setting.format(setting.minimum)).x, value.GetPreferredValues(setting.format(setting.maximum)).x);
                    value_widths[count] = Math.Max(bounds_width, value.GetPreferredValues(setting.format(setting.read())).x);
                }
            }
            else if (setting.kind == ControlKind.Toggle)
            {
                UIToggle toggle = row.GetComponentInChildren<UIToggle>(true);
                Transform branch = toggle.toggleImage.transform;
                for (int depth = 0; depth < 16 && branch.parent != row.transform; depth++) branch = branch.parent;
                if (branch.parent != row.transform) throw new InvalidOperationException("Toggle control has no row parent.");
                control = branch.GetComponent<RectTransform>();
                RectTransform background = toggle.toggleImage.transform.parent.GetComponent<RectTransform>();
                width = background.rect.width + value_gap + toggle.buttonText.GetPreferredValues("Off").x;
            }
            else
            {
                Selectable selectable = setting.kind == ControlKind.Dropdown
                    ? row.GetComponentInChildren<TMP_Dropdown>(true)
                    : row.GetComponentInChildren<Button>(true);
                control = selectable.GetComponent<RectTransform>();
                width = control.GetComponent<LayoutElement>().preferredWidth;
            }
            if (!float.IsFinite(width) || width <= 0) throw new InvalidOperationException("Mods control width is invalid.");
            labels[count] = label;
            controls[count] = control;
            widths[count] = width;
            float required_width = width + (values[count] != null ? value_gap + value_widths[count] : 0);
            control_width_max = Math.Max(control_width_max, required_width);
            row_width = Math.Min(row_width, row.GetComponent<RectTransform>().rect.width);
            count++;
        }
        if (count != registry.count || !float.IsFinite(row_width) || row_width <= 0) throw new InvalidOperationException("Mods rows have no resolved width.");
        float start = Math.Clamp(label_left + label_width_max + 16f, 0, Math.Max(0, row_width - control_width_max));
        for (int i = 0; i < count; i++)
        {
            RectTransform label = labels[i].rectTransform;
            label.anchorMin = new Vector2(0, label.anchorMin.y);
            label.anchorMax = new Vector2(0, label.anchorMax.y);
            label.offsetMin = new Vector2(label_left, label.offsetMin.y);
            label.offsetMax = new Vector2(start - 16f, label.offsetMax.y);
            RectTransform control = controls[i];
            float value_width = values[i] != null ? value_gap + value_widths[i] : 0;
            float width = Math.Min(widths[i], Math.Max(0, row_width - start - value_width));
            control.anchorMin = new Vector2(0, control.anchorMin.y);
            control.anchorMax = new Vector2(0, control.anchorMax.y);
            control.offsetMin = new Vector2(start, control.offsetMin.y);
            control.offsetMax = new Vector2(start + width, control.offsetMax.y);
            TextMeshProUGUI? value = values[i];
            if (value == null) continue;
            RectTransform rect = value.rectTransform;
            rect.anchorMin = new Vector2(0, rect.anchorMin.y);
            rect.anchorMax = new Vector2(0, rect.anchorMax.y);
            rect.offsetMin = new Vector2(start + width + value_gap, rect.offsetMin.y);
            rect.offsetMax = new Vector2(start + width + value_gap + value_widths[i], rect.offsetMax.y);
        }
    }

    public static void typography(GameObject row, TextMeshProUGUI template)
    {
        var texts = row.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < Math.Min(texts.Length, 32); i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text.transform.parent != row.transform ||
                !(text.name.StartsWith("Option Name", StringComparison.Ordinal) || text.name == "Label")) continue;
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
            text.fontSize = template.fontSize;
            text.fontWeight = template.fontWeight;
            text.fontStyle = template.fontStyle;
            text.characterSpacing = template.characterSpacing;
            text.wordSpacing = template.wordSpacing;
            text.lineSpacing = template.lineSpacing;
            text.color = template.color;
            text.fontSizeMin = template.fontSizeMin;
            text.fontSizeMax = template.fontSizeMax;
            text.enableAutoSizing = template.enableAutoSizing;
            text.alignment = template.alignment;
            RectTransform rect = text.rectTransform;
            RectTransform native_rect = template.rectTransform;
            rect.pivot = new Vector2(rect.pivot.x, native_rect.pivot.y);
            rect.anchorMin = new Vector2(rect.anchorMin.x, native_rect.anchorMin.y);
            rect.anchorMax = new Vector2(rect.anchorMax.x, native_rect.anchorMax.y);
            rect.offsetMin = new Vector2(rect.offsetMin.x, native_rect.offsetMin.y);
            rect.offsetMax = new Vector2(rect.offsetMax.x, native_rect.offsetMax.y);
            text.margin = new Vector4(0, template.margin.y, 0, template.margin.w);
        }
    }

    public static void label(GameObject row)
    {
        var texts = row.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < Math.Min(texts.Length, 32); i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text.transform.parent != row.transform ||
                !(text.name.StartsWith("Option Name", StringComparison.Ordinal) || text.name == "Label")) continue;
            text.rectTransform.offsetMin = new Vector2(label_left, text.rectTransform.offsetMin.y);
            text.margin = new Vector4(0, text.margin.y, 0, text.margin.w);
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableWordWrapping = false;
        }
    }

    private static void prepare_row(GameObject row)
    {
        Transform? ancestor = row.transform;
        bool owned = false;
        for (int depth = 0; depth < 16 && ancestor != null; depth++)
        {
            if (ancestor.name == "ModSettings") { owned = true; break; }
            ancestor = ancestor.parent;
        }
        if (!owned) throw new InvalidOperationException("Only Mods rows can be laid out.");
        ContentSizeFitter? fitter = row.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
        LayoutElement element = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
        element.layoutPriority = 2;
        element.minWidth = 0;
        element.preferredWidth = -1;
        element.flexibleWidth = 1;
        element.minHeight = row_height;
        element.preferredHeight = row_height;
        element.flexibleHeight = 0;
        label(row);
        VerticalLayoutGroup? group = row.transform.parent.GetComponent<VerticalLayoutGroup>();
        if (group == null) return;
        group.childControlWidth = true;
        group.childForceExpandWidth = true;
        group.childControlHeight = true;
        group.childForceExpandHeight = false;
        group.spacing = row_gap;
    }

    private static void stretch_width(RectTransform rect)
    {
        ContentSizeFitter? fitter = rect.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rect.anchorMin = new Vector2(0, rect.anchorMin.y);
        rect.anchorMax = new Vector2(1, rect.anchorMax.y);
        rect.offsetMin = new Vector2(0, rect.offsetMin.y);
        rect.offsetMax = new Vector2(0, rect.offsetMax.y);
    }
}
