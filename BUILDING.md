# Building

Requires the .NET 8 SDK and Python 3. The mod targets .NET 6 and tests run on
.NET 8. Launch Schedule I with MelonLoader once to generate its IL2CPP
assemblies.

Set the MelonLoader path to the directory containing both `net6` and
`Il2CppAssemblies`. These game and loader dependencies stay on your machine.

## Windows (PowerShell)

```powershell
$loader = "C:\path\to\profile\MelonLoader"
dotnet build ModSettings.csproj --configuration Release "-p:MelonLoaderDir=$loader"
dotnet run --project tests/Tests.csproj
python package.py --prepare
```

## Linux / Nix

```sh
MELONLOADER_DIR='/path/to/profile/MelonLoader' ./build.sh
nix-shell -p dotnet-sdk_8 --run 'dotnet run --project tests/Tests.csproj'
```

The DLL is written to `bin/Release/net6.0/`. Packaging produces a Thunderstore
ZIP in `dist/`. Run `python package.py --prepare` after building to copy the DLL
into `package/` and create the ZIP. Commit that DLL alongside its source
changes. The `bin/` and `dist/` directories remain ignored.

## Formatting

Set `MelonLoaderDir` as an environment variable when formatting the main
project:
`MelonLoaderDir=/path/to/profile/MelonLoader dotnet format ModSettings.csproj`
on Linux, or set `$env:MelonLoaderDir` in PowerShell before running the
formatter. Run `dotnet format tests/Tests.csproj` for tests and
`ruff format package.py` for the packaging script.

Format Markdown as Neovim does with `mdformat --wrap 80 --number` and the GFM,
footnote, and frontmatter plugins. Avoid semicolons in prose.

## In-game checks

Standalone checks cover grouping, bounds, integer steps, and both menu bindings.
To test the preference adapters against real MelonLoader entries locally, run:

```sh
nix-shell -p dotnet-sdk_8 --run 'dotnet run --project tests/Tests.csproj -p:MelonLoaderDir=/path/to/profile/MelonLoader'
```

These checks construct entries without starting the game. Native UI behavior
still requires manual checks with mods that register all control types:

- Open settings in the main menu and pause menu. Confirm the Mods tab fits and
  all native tabs still switch correctly.
- Check one heading per mod, correct row names, current values after reopening,
  integer slider steps, and fractional multiplier values.
- Change every control and restart the game. Confirm saved values survive.
- Change an entry from code and verify both menus refresh. Reload scenes and
  reopen the menus to check replacement bindings.
- Temporarily prevent preference writes, change a setting, then restore access.
  Confirm saving retries and the value survives a restart.
- Check keyboard, controller focus, and bumper navigation through the new tab.

## GitHub Actions

`Package and publish` runs standalone tests and release-metadata checks on pull
requests. Pushes to main and manual runs also package the committed DLL into a
validated Thunderstore ZIP. Download the `thunderstore-package` artifact from
the run and extract the mod ZIP inside. Artifacts expire after 30 days.

CI packages `package/ModSettings.dll`. It does not compile the mod or need game
references. Rebuild locally and run `python package.py --prepare` whenever the
mod source changes. CI tests source code separately. Those tests do not prove
the committed DLL matches that source or that multiplayer works.

## Publishing

1. Update the version in the project, package manifest and MelonInfo attribute
   together.
2. Build locally, run tests, then run `python package.py --prepare`.
3. Commit and push the source, metadata and refreshed `package/ModSettings.dll`.
4. Create and push a matching `vX.Y.Z` tag.

**Pushing the version tag publishes to Thunderstore automatically** after checks
and packaging pass. Main-branch pushes and manual runs only create ZIP
artifacts. The tag must match the manifest version.

Publishing uses the existing `TCLI_AUTH_TOKEN` Actions secret and official
`tcli` 0.2.4, uploading the exact ZIP artifact to `holyfurries` / `schedule-i`.
No private reference repository or reference token is needed.

Existing Thunderstore versions cannot be replaced. Bump the version for each
release. If an upload fails, check Thunderstore before rerunning the workflow.
