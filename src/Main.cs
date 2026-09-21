using System;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(ModSettings.Main), "Mod Settings", "0.1.1", "holyfurries")]
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
        Settings.unsaved_since_seconds = float.NaN;
        try { MelonPreferences.Save(); }
        catch (Exception error) { LoggerInstance.Warning($"Could not save preferences: {error.Message}"); }
    }
}
