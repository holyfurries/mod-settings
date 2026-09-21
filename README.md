# Mod Settings

Puts mod options where players look for them: a **Mods** tab on the game's own settings screen,
in the main menu and the pause menu. It uses the game's switches, sliders and dropdowns, so it
looks and behaves like the rest of the menu.

On its own it does nothing. Mods that use it list their options under their own heading.
Changes apply straight away and are saved to `UserData/MelonPreferences.cfg`.

## For mod authors

Reference `ModSettings.dll`, add `holyfurries-ModSettings` to your Thunderstore dependencies,
and register your existing MelonPreferences entries in `OnInitializeMelon`:

```csharp
MelonPreferences_Category preferences = MelonPreferences.CreateCategory("MyMod");
MelonPreferences_Entry<bool> enabled = preferences.CreateEntry("enabled", true);
MelonPreferences_Entry<float> size = preferences.CreateEntry("size", 1f);
MelonPreferences_Entry<Corner> corner = preferences.CreateEntry("corner", Corner.TopRight);

Settings.toggle("My Mod", "Show overlay", enabled);
Settings.slider("My Mod", "Overlay size", size, minimum: 0.5f, maximum: 3f);
Settings.dropdown("My Mod", "Overlay corner", corner);
```

- The entry stays the source of truth. Read `entry.Value` as usual, or subscribe to
  `entry.OnEntryValueChanged` to react.
- Changing the entry from code, such as a hotkey, updates the control.
- `slider` takes `whole_numbers: true` for integer steps. `dropdown` works with any enum and
  lists its names.
- Up to 128 settings in total. Labels are at most 48 characters, mod names 32.

If a game update changes the settings screen, the tab is skipped with a warning in the log and
the config file keeps working.

## Installation

Install with a Thunderstore mod manager, or copy `Mods/ModSettings.dll` into the game's `Mods` folder
with MelonLoader 0.7.3 (IL2CPP).

## Development

See `BUILDING.md` and `DEVELOPMENT.md`.

## License

MIT
