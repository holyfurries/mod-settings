using System;
using ModSettings;

internal static class Program
{
    private static void Main()
    {
        float stored = 0f;
        Setting make(string mod, string label, ControlKind kind = ControlKind.Toggle, float minimum = 0f, float maximum = 1f,
            bool whole_numbers = true, string[]? options = null) =>
            new(mod, label, kind, minimum, maximum, whole_numbers, options ?? Array.Empty<string>(), () => stored, value => stored = value);

        var registry = new Registry();
        registry.add(make("Casino Ledger", "Standings"));
        registry.add(make("Death Notices", "Position", ControlKind.Dropdown, 0f, 2f, true, new[] { "Left", "Center", "Right" }));
        registry.add(make("Casino Ledger", "Size", ControlKind.Slider, 0.5f, 3f, false));
        require(registry.count == 3, "Settings are counted");
        require(registry.at(0).label == "Standings" && registry.at(1).label == "Size" && registry.at(2).label == "Position", "A mod's settings stay together in registration order");
        require(registry.starts_group(0) && !registry.starts_group(1) && registry.starts_group(2), "Each mod gets one header");
        rejects<InvalidOperationException>(() => registry.add(make("Casino Ledger", "Size")), "Duplicate setting rejected");
        rejects<ArgumentOutOfRangeException>(() => registry.at(3), "Out of range index rejected");

        Setting size = registry.at(1);
        require(size.clamp(9f) == 3f && size.clamp(-1f) == 0.5f && size.clamp(float.NaN) == 0.5f, "Slider values are bounded");
        require(size.clamp(1.26f) == 1.26f && size.format(1.256f) == "1.26", "Fractional sliders keep and show two decimals");
        Setting patrols = make("Coordinated Police", "Patrols", ControlKind.Slider, 0f, 16f);
        require(patrols.clamp(5.6f) == 6f, "Integer sliders round to a whole count");
        require(patrols.clamp(-1f) == 0f && patrols.clamp(99f) == 16f, "Integer sliders stay within their range");
        require(patrols.format(6f) == "6", "Integer counts have no decimal suffix");
        float menu_value = -1f;
        float pause_value = -1f;
        patrols.bind(value => menu_value = value);
        patrols.bind(value => pause_value = value);
        patrols.show(5.6f);
        require(menu_value == 6f && pause_value == 6f, "Main and pause menu controls both refresh");
        float replacement_value = -1f;
        patrols.bind(value => replacement_value = value);
        patrols.show(7f);
        require(menu_value == 6f && pause_value == 7f && replacement_value == 7f,
            "Recreated controls replace the oldest binding without accumulating views");
        Setting percent = make("Test", "Percent", ControlKind.Slider, 0f, 100f, false);
        require(percent.format(41.6f) == "42", "Wide sliders show whole numbers");
        Setting position = registry.at(2);
        require(position.clamp(1.4f) == 1f && position.clamp(7f) == 2f, "Dropdown values snap to an option");

        require(Setting.spaced("TopCenter") == "Top Center" && Setting.spaced("top_left") == "top left" && Setting.spaced("HUDOnly") == "HUD Only" && Setting.spaced("Off") == "Off", "Enum names read as words");
        rejects<ArgumentOutOfRangeException>(() => make("", "Label"), "Empty mod name rejected");
        rejects<ArgumentOutOfRangeException>(() => make("Mod", new string('a', 49)), "Oversized label rejected");
        rejects<ArgumentOutOfRangeException>(() => make("Mod", "Range", ControlKind.Slider, 2f, 2f), "Empty slider range rejected");
        rejects<ArgumentOutOfRangeException>(() => make("Mod", "Range", ControlKind.Slider, 0.5f, 3f), "Fractional bounds on integer sliders rejected");
        rejects<ArgumentOutOfRangeException>(() => make("Mod", "Options", ControlKind.Dropdown, 0f, 1f, true, new[] { "Only" }), "Single option dropdown rejected");

        var full = new Registry();
        for (int i = 0; i < Registry.setting_count_max; i++) full.add(make("Mod", $"Setting {i}"));
        rejects<InvalidOperationException>(() => full.add(make("Mod", "One too many")), "Registry is bounded");
#if MELONLOADER
        check_preferences();
#endif
        Console.WriteLine("Grouping, bounds, formatting and validation checks passed.");
    }

#if MELONLOADER
    private static void check_preferences()
    {
        var entry = (MelonLoader.MelonPreferences_Entry<int>)Activator.CreateInstance(
            typeof(MelonLoader.MelonPreferences_Entry<int>), nonPublic: true)!;
        entry.Value = 5;
        int observed = -1;
        entry.OnEntryValueChanged.Subscribe((_, value) => observed = value);
        Settings.slider("Coordinated Police", "Patrols", entry, minimum: 0, maximum: 16);
        Setting setting = Settings.registry.at(0);
        float menu = -1f;
        float pause = -1f;
        setting.bind(value => menu = value);
        setting.bind(value => pause = value);
        Settings.change(setting, 7.6f, 0);
        require(entry.Value == 8 && observed == 8, "Integer slider updates the real entry and subscriber");
        require(menu == 8 && pause == 8, "Both menu controls reflect the real entry");
        require(Settings.unsaved_since_seconds == 0, "UI changes schedule persistence");
        entry.Value = 3;
        require(menu == 3 && pause == 3, "Programmatic changes refresh both controls");
        entry.Value = 999;
        Settings.change(setting, 16, 1);
        require(entry.Value == 16, "Selecting a bound repairs an out-of-range entry");
        rejects<ArgumentOutOfRangeException>(() => Settings.slider("Test", "Range", entry, 0, 16777217),
            "Integer slider range stays within float precision");

        var multiplier = (MelonLoader.MelonPreferences_Entry<float>)Activator.CreateInstance(
            typeof(MelonLoader.MelonPreferences_Entry<float>), nonPublic: true)!;
        multiplier.Value = 1;
        Settings.slider("Coordinated Police", "Response", multiplier, minimum: 0.25f, maximum: 2f);
        Settings.change(Settings.registry.at(1), 0.75f, 2);
        require(multiplier.Value == 0.75f, "Fractional preference values are preserved");

        var corner = (MelonLoader.MelonPreferences_Entry<TestCorner>)Activator.CreateInstance(
            typeof(MelonLoader.MelonPreferences_Entry<TestCorner>), nonPublic: true)!;
        corner.Value = (TestCorner)99;
        Settings.dropdown("Coordinated Police", "Corner", corner);
        Settings.change(Settings.registry.at(2), 0, 3);
        require(corner.Value == TestCorner.TopLeft, "Selecting the first option repairs an undefined enum value");
    }

    private enum TestCorner { TopLeft, TopRight }
#endif

    private static void require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void rejects<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException(message);
    }
}
