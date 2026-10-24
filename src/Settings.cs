using System;
using MelonLoader;
using UnityEngine;

namespace ModSettings;

public static class Settings
{
    private const int slider_integer_magnitude_max = 1 << 24;
    internal static readonly Registry registry = new();
    internal static readonly KeyCode[] key_codes = Enum.GetValues<KeyCode>();
    internal static float unsaved_since_seconds = float.NaN;

    public static void toggle(string mod, string label, MelonPreferences_Entry<bool> entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        registry.add(new Setting(mod, label, ControlKind.Toggle, 0f, 1f, true, Array.Empty<string>(),
            () => entry.Value ? 1f : 0f, value => entry.Value = value >= 0.5f));
        entry.OnEntryValueChanged.Subscribe((_, value) => registry_show(mod, label, value ? 1f : 0f));
    }

    public static void slider(string mod, string label, MelonPreferences_Entry<float> entry, float minimum, float maximum,
        bool whole_numbers = false)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        registry.add(new Setting(mod, label, ControlKind.Slider, minimum, maximum, whole_numbers, Array.Empty<string>(),
            () => entry.Value, value => entry.Value = value));
        entry.OnEntryValueChanged.Subscribe((_, value) => registry_show(mod, label, value));
    }

    public static void slider(string mod, string label, MelonPreferences_Entry<int> entry, int minimum, int maximum)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        if (minimum < -slider_integer_magnitude_max) throw new ArgumentOutOfRangeException(nameof(minimum));
        if (maximum > slider_integer_magnitude_max) throw new ArgumentOutOfRangeException(nameof(maximum));
        registry.add(new Setting(mod, label, ControlKind.Slider, minimum, maximum, true, Array.Empty<string>(),
            () => entry.Value, value => entry.Value = checked((int)value)));
        entry.OnEntryValueChanged.Subscribe((_, value) => registry_show(mod, label, value));
    }

    public static void dropdown<T>(string mod, string label, MelonPreferences_Entry<T> entry) where T : struct, Enum
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        T[] values = Enum.GetValues<T>();
        string[] names = Array.ConvertAll(Enum.GetNames<T>(), Setting.spaced);
        registry.add(new Setting(mod, label, ControlKind.Dropdown, 0f, Math.Max(1, values.Length - 1), true, names,
            () => Array.IndexOf(values, entry.Value), value => entry.Value = values[(int)value]));
        entry.OnEntryValueChanged.Subscribe((_, value) => registry_show(mod, label, Array.IndexOf(values, value)));
    }

    public static void keybind(string mod, string label, MelonPreferences_Entry<KeyCode> entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        if (key_codes.Length < 2 || key_codes.Length > 512 || key_codes[0] != KeyCode.None)
            throw new InvalidOperationException("Unexpected Unity key codes.");
        string[] names = Array.ConvertAll(key_codes, key => key == KeyCode.None ? "Unbound" : Setting.spaced(key.ToString()));
        registry.add(new Setting(mod, label, ControlKind.Keybind, 0f, key_codes.Length - 1, true, names,
            () => Array.IndexOf(key_codes, entry.Value), value => entry.Value = key_codes[(int)value]));
        entry.OnEntryValueChanged.Subscribe((_, value) => registry_show(mod, label, Array.IndexOf(key_codes, value)));
    }

    internal static void change(Setting setting, float value, float now_seconds)
    {
        float bounded = setting.clamp(value);
        if (bounded == setting.read()) return;
        setting.write(bounded);
        if (float.IsNaN(unsaved_since_seconds)) unsaved_since_seconds = now_seconds;
    }

    private static void registry_show(string mod, string label, float value)
    {
        for (int i = 0; i < registry.count; i++)
        {
            Setting setting = registry.at(i);
            if (setting.mod == mod && setting.label == label) setting.show(value);
        }
    }
}
