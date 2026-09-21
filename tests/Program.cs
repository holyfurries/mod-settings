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
        Setting percent = make("Test", "Percent", ControlKind.Slider, 0f, 100f, false);
        require(percent.format(41.6f) == "42", "Wide sliders show whole numbers");
        Setting position = registry.at(2);
        require(position.clamp(1.4f) == 1f && position.clamp(7f) == 2f, "Dropdown values snap to an option");

        rejects<ArgumentOutOfRangeException>(() => make("", "Label"), "Empty mod name rejected");
        rejects<ArgumentOutOfRangeException>(() => make("Mod", new string('a', 49)), "Oversized label rejected");
        rejects<ArgumentOutOfRangeException>(() => make("Mod", "Range", ControlKind.Slider, 2f, 2f), "Empty slider range rejected");
        rejects<ArgumentOutOfRangeException>(() => make("Mod", "Options", ControlKind.Dropdown, 0f, 1f, true, new[] { "Only" }), "Single option dropdown rejected");

        var full = new Registry();
        for (int i = 0; i < Registry.setting_count_max; i++) full.add(make("Mod", $"Setting {i}"));
        rejects<InvalidOperationException>(() => full.add(make("Mod", "One too many")), "Registry is bounded");
        Console.WriteLine("Grouping, bounds, formatting and validation checks passed.");
    }

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
