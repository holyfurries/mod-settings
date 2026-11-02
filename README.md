# Mod Settings

[Holy Furries Studio](https://holyfurries.com) ·
[Thunderstore](https://thunderstore.io/c/schedule-i/p/holyfurries/ModSettings/)
· [Source](https://github.com/holyfurries/mod-settings)

Adds a **Mods** tab to Schedule I's settings screen in the main menu and pause
menu. Participating mods appear under their own headings with native switches,
sliders, dropdowns, and keybind controls. Changes are saved to
`UserData/MelonPreferences.cfg`.

Mod options use consistent columns and compact row spacing. Slider values sit
beside their tracks. Built-in settings panels keep their original layout.

## Install

**IL2CPP only.** Requires MelonLoader **0.7.3**.

Install with a Thunderstore mod manager, or copy `ModSettings.dll` from the
release ZIP's `Mods/` folder into the game's `Mods/` folder. Options appear when
other installed mods register them.

## For mod authors

Reference `ModSettings.dll`, add `holyfurries-ModSettings-0.3.0` to your
Thunderstore dependencies, and register existing MelonPreferences entries in
`OnInitializeMelon`:

```csharp
using MelonLoader;
using ModSettings;
using UnityEngine;

MelonPreferences_Category preferences = MelonPreferences.CreateCategory("MyMod");
MelonPreferences_Entry<bool> enabled = preferences.CreateEntry("enabled", true);
MelonPreferences_Entry<int> count = preferences.CreateEntry("count", 5);
MelonPreferences_Entry<float> size = preferences.CreateEntry("size", 1f);
MelonPreferences_Entry<Corner> corner = preferences.CreateEntry("corner", Corner.TopRight);
MelonPreferences_Entry<KeyCode> shortcut = preferences.CreateEntry("shortcut", KeyCode.F7);

Settings.toggle("My Mod", "Show overlay", enabled);
Settings.slider("My Mod", "Item count", count, minimum: 0, maximum: 16);
Settings.slider("My Mod", "Overlay size", size, minimum: 0.5f, maximum: 3f);
Settings.dropdown("My Mod", "Overlay corner", corner);
Settings.keybind("My Mod", "Toggle overlay", shortcut);
```

Read `entry.Value` or subscribe to `entry.OnEntryValueChanged` to apply changes.
Programmatic changes refresh controls in both menus. Integer entries use whole
steps. Float entries retain fractions unless `whole_numbers: true` is specified.
Dropdowns use enum names with spaces, such as `Top Center`.

Click a keybind control and press a keyboard, mouse, or controller button.
**Backspace** disables the shortcut by saving `KeyCode.None`. **Escape**
cancels. Closing the menu, losing focus, or waiting ten seconds also cancels.
Escape and Backspace are reserved during capture. Mods must skip disabled
shortcuts when reading input.

Register at most 128 settings across all mods. Mod names allow 32 characters and
labels allow 48. Integer slider bounds must be within ±16,777,216 so every value
can be represented exactly by the game's float-based slider.

If the tab cannot be built, check the warning in `MelonLoader/Latest.log` and
edit the config file directly.

## Development

See
[BUILDING.md](https://github.com/holyfurries/mod-settings/blob/main/BUILDING.md)
for builds, tests, packaging, and in-game checks.

[Code: MIT](https://github.com/holyfurries/mod-settings/blob/main/LICENSE).
[Icon: studio asset terms](https://github.com/holyfurries/brand-assets/blob/main/LICENSE).
