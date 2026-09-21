# Development

Version 0.1.1, built against local Schedule I 0.4.6f13 IL2CPP interop assemblies.

## Design

`Settings` is the public API. Each call wraps a MelonPreferences entry in a `Setting` (pure, in
`Registry.cs`): read and write closures over a float, where a switch is 0 or 1 and a dropdown is
the option index. `Registry` keeps a bounded list grouped by mod name in registration order.

`Tab` postfixes `SettingsScreen.Awake`, once for the main menu screen and once for the pause menu
screen. It picks the category panel that has a `VerticalLayoutGroup` and the most control kinds,
clones it, hides the cloned rows, and adds a header plus cloned rows per setting:

- switch: the `SettingsToggle` row, game component removed, driven through `UIToggle.OnChanged`.
  The name goes through `UIOption.optionName` because `UIOption.Awake` rewrites the label.
- slider: the parent of a `SettingsSlider`, game component removed, `UISlider.canUpdateValueText`
  off so the value label shows `Setting.format`.
- dropdown: the parent of a `SettingsDropdown`, game component removed, options replaced.

It then clones the last tab toggle, appends a `SettingsCategory` to `Categories` so the game's
`ShowCategory` handles it, and narrows all tabs to fit the 700 unit window (the tab bar is a
left-aligned layout of fixed 90 unit tabs). Selecting the tab refreshes every control from its
entry, since the game components' `Awake` runs on first show. Controls hold `show` callbacks so
entry changes from code update them; callbacks ignore destroyed controls from the other screen.

Preferences are saved one second after the first unsaved change, from `Main.OnUpdate`, so a
slider drag does not write the file every frame. Any failure while building the tab logs a
warning and leaves the screen untouched.

## In-game checks

The game cannot be run from CI. With a mod that registers all three kinds:

- Main menu and pause menu: a Mods tab follows the last game tab, all tabs fit and still switch.
- Each mod has one heading; rows show the right names and current values after reopening.
- Switch, slider and dropdown change the entry at once and survive a restart.
- A hotkey that flips an entry updates the open tab.
- Known gap: controller navigation of the cloned rows and bumper cycling to the tab are untested.
