using System;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(ModSettings.Main), "Mod Settings", "0.2.0", "holyfurries")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ModSettings;

public sealed class Main : MelonMod
{
    private const float save_delay_seconds = 1f;

    public override void OnInitializeMelon() => Tab.install(HarmonyInstance);

    public override void OnUpdate()
    {
        float unsaved_since_seconds = Settings.unsaved_since_seconds;
        if (float.IsNaN(unsaved_since_seconds) || Time.unscaledTime - unsaved_since_seconds < save_delay_seconds) return;
        try
        {
            MelonPreferences.Save();
            Settings.unsaved_since_seconds = float.NaN;
        }
        catch (Exception error)
        {
            Settings.unsaved_since_seconds = Time.unscaledTime;
            LoggerInstance.Warning($"Mod Settings: Could not save preferences: {error.Message}");
        }
    }
}
