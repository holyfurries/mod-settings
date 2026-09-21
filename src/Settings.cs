using System;
using MelonLoader;

namespace ModSettings;

/// Entry point for other mods. Call these from OnInitializeMelon; each adds one control to the Mods tab of the
/// game's settings screen, grouped under the mod name. The control edits the entry and the preferences are saved.
public static class Settings
{
    internal static readonly Registry registry = new();
    internal static float unsaved_since_seconds = float.NaN;

    public static void toggle(string mod, string label, MelonPreferences_Entry<bool> entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        register(new Setting(mod, label, ControlKind.Toggle, 0f, 1f, true, Array.Empty<string>(),
            () => entry.Value ? 1f : 0f, value => entry.Value = value >= 0.5f));
        entry.OnEntryValueChanged.Subscribe((_, value) => registry_show(mod, label, value ? 1f : 0f));
    }

    public static void slider(string mod, string label, MelonPreferences_Entry<float> entry, float minimum, float maximum,
        bool whole_numbers = false)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        register(new Setting(mod, label, ControlKind.Slider, minimum, maximum, whole_numbers, Array.Empty<string>(),
            () => entry.Value, value => entry.Value = value));
        entry.OnEntryValueChanged.Subscribe((_, value) => registry_show(mod, label, value));
    }

    public static void dropdown<T>(string mod, string label, MelonPreferences_Entry<T> entry) where T : struct, Enum
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        T[] values = Enum.GetValues<T>();
        string[] names = Array.ConvertAll(Enum.GetNames<T>(), Setting.spaced);
        register(new Setting(mod, label, ControlKind.Dropdown, 0f, Math.Max(1, values.Length - 1), true, names,
            () => Math.Max(0, Array.IndexOf(values, entry.Value)), value => entry.Value = values[(int)value]));
        entry.OnEntryValueChanged.Subscribe((_, value) => registry_show(mod, label, Math.Max(0, Array.IndexOf(values, value))));
    }

    internal static void change(Setting setting, float value, float now_seconds)
    {
        float bounded = setting.clamp(value);
        if (bounded == setting.clamp(setting.read())) return;
        setting.write(bounded);
        if (float.IsNaN(unsaved_since_seconds)) unsaved_since_seconds = now_seconds;
    }

    private static void register(Setting setting)
    {
        registry.add(setting);
    }

    private static void registry_show(string mod, string label, float value)
    {
        for (int i = 0; i < registry.count; i++)
        {
            Setting setting = registry.at(i);
            if (setting.mod == mod && setting.label == label) setting.show?.Invoke(setting.clamp(value));
        }
    }
}
