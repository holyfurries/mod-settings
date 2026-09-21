using System;
using System.Globalization;

namespace ModSettings;

internal enum ControlKind { Toggle, Slider, Dropdown }

internal sealed class Setting
{
    public readonly string mod;
    public readonly string label;
    public readonly ControlKind kind;
    public readonly float minimum;
    public readonly float maximum;
    public readonly bool whole_numbers;
    public readonly string[] options;
    public readonly Func<float> read;
    public readonly Action<float> write;
    public Action<float>? show;

    public Setting(string mod, string label, ControlKind kind, float minimum, float maximum, bool whole_numbers,
        string[] options, Func<float> read, Action<float> write)
    {
        if (string.IsNullOrWhiteSpace(mod) || mod.Length > 32) throw new ArgumentOutOfRangeException(nameof(mod));
        if (string.IsNullOrWhiteSpace(label) || label.Length > 48) throw new ArgumentOutOfRangeException(nameof(label));
        if (!float.IsFinite(minimum) || !float.IsFinite(maximum) || maximum <= minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
        if (kind == ControlKind.Dropdown && (options.Length < 2 || options.Length > 32)) throw new ArgumentOutOfRangeException(nameof(options));
        this.mod = mod;
        this.label = label;
        this.kind = kind;
        this.minimum = minimum;
        this.maximum = maximum;
        this.whole_numbers = whole_numbers;
        this.options = options;
        this.read = read;
        this.write = write;
    }

    public float clamp(float value)
    {
        if (!float.IsFinite(value)) return minimum;
        float bounded = Math.Clamp(value, minimum, maximum);
        return whole_numbers || kind != ControlKind.Slider ? MathF.Round(bounded) : bounded;
    }

    public string format(float value)
    {
        float shown = clamp(value);
        if (whole_numbers) return shown.ToString("0", CultureInfo.InvariantCulture);
        return shown.ToString(maximum - minimum >= 20f ? "0" : "0.00", CultureInfo.InvariantCulture);
    }
}

internal sealed class Registry
{
    public const int setting_count_max = 128;
    private readonly Setting[] settings = new Setting[setting_count_max];
    public int count { get; private set; }

    public void add(Setting setting)
    {
        if (count == setting_count_max) throw new InvalidOperationException("Too many mod settings registered.");
        for (int i = 0; i < count; i++)
        {
            if (settings[i].mod == setting.mod && settings[i].label == setting.label) throw new InvalidOperationException($"{setting.mod} already registered \"{setting.label}\".");
        }
        int insert_index = count;
        for (int i = 0; i < count; i++)
        {
            if (settings[i].mod == setting.mod) insert_index = i + 1;
        }
        Array.Copy(settings, insert_index, settings, insert_index + 1, count - insert_index);
        settings[insert_index] = setting;
        count++;
    }

    public Setting at(int index)
    {
        if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
        return settings[index];
    }

    public bool starts_group(int index) => index == 0 || at(index).mod != at(index - 1).mod;
}
