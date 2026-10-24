# Building

Requires the .NET 8 SDK, Rust, and a local Schedule I installation with
MelonLoader. The mod targets .NET 6. Tests run on .NET 8.

Launch the game with MelonLoader once to generate references. Set
`MelonLoaderDir` to the directory containing `net6/` and `Il2CppAssemblies/`:

```sh
export MelonLoaderDir='/path/to/profile/MelonLoader'
```

In PowerShell, use `$env:MelonLoaderDir = 'C:\path\to\profile\MelonLoader'`. On
Nix, enter `nix-shell -p dotnet-sdk_8 cargo rustc` first.

## Build and test

```console
dotnet build ModSettings.csproj --configuration Release
dotnet run --project tests/Tests.csproj -p:MelonLoaderDir=
dotnet run --project tests/Tests.csproj "-p:MelonLoaderDir=$MelonLoaderDir"
cargo test --locked
```

The optional third command tests preference adapters against real MelonLoader
entries. In PowerShell, use `$env:MelonLoaderDir` in that argument.

Copy `bin/Release/net6.0/ModSettings.dll` into your test profile's `Mods/`
directory, then restart the game.

Check both the main-menu and pause-menu settings:

- Labels, headings, and controls align without clipping. Built-in tabs still
  work.
- Sliders, toggles, dropdowns, and keybinds update correctly. Values survive
  reopening the menu and restarting the game.
- Keybind capture ignores its activation click. Escape cancels, Backspace
  clears, and closing the menu cancels capture.

## Format

With `MelonLoaderDir` set:

```console
dotnet format ModSettings.csproj
dotnet format tests/Tests.csproj
cargo fmt
```

## Package and release

Copy the Release DLL to `package/ModSettings.dll` before packaging. Use
`Copy-Item` in PowerShell or `cp` on Linux.

```console
cargo run --locked -- metadata
dotnet tool restore
dotnet tool run tcli build --config-path dist/thunderstore.toml
cargo run --locked -- package
```

The validated ZIP is written to `dist/holyfurries-ModSettings-<version>.zip`.
The icon comes from
[studio brand assets](https://github.com/holyfurries/brand-assets/tree/main/logos)
and ships with `icon-LICENSE`.

For a release, align the versions in `ModSettings.csproj`,
`package/manifest.json`, and `MelonInfo`. Commit the source, metadata, and
refreshed DLL together, then push a matching `vX.Y.Z` tag.

**Pushing the tag publishes to Thunderstore automatically.** Main-branch pushes
produce downloadable package artifacts. CI packages the committed DLL without
compiling against game references. Publishing requires the `TCLI_AUTH_TOKEN`
Actions secret.
