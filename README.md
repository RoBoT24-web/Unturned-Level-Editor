# LevelEditorPlus

Extra tools for Unturned's map editor, plus optional invisible barricade-slot
assets for doors, windows, gates, and hatches.

## Install

1. Download `LevelEditorPlus-Setup.exe` from this repository's **Releases**.
2. Close Unturned, run the installer, and confirm the game folder.
3. Choose whether to install the optional BarricadeSlots assets. **Uncheck this
   if you already have the same assets from Workshop or embedded in a map.**
4. Click **Install / Update**.
5. Launch Unturned **without BattlEye** and open the map editor.

The toolbar appears at the top-right. Running the installer again updates the
files without replacing your settings or clipboard.

Windows needs .NET Framework 4.8. The installer requests administrator access
and is unsigned, so Windows may show a publisher/SmartScreen warning.
Only run installers from a source you trust.

## Tools

| Toolbar button | Purpose |
|---|---|
| Console | View, search, and copy game logs |
| Transform | Edit position, rotation, scale, and snapping; drop or focus objects |
| Hierarchy | Find placed objects, select them, or double-click to fly to them |
| Assets | Search assets and choose one for placement |
| Fit slot | Fit and center a selected custom slot inside an opening |
| Keybinds | Change shortcuts live |
| Slots | Toggle colored slot outlines and facing arrows |

**Finding nearby slots:** in Hierarchy, search `Barricade Slot`, enable
**Nearby only**, and adjust the radius (initially 25 m). **Nearest first**
sorts by camera distance. Results update as you move.
Click to select, Ctrl+click to add to selection, double-click to fly there.
**Select results** uses the filtered list (up to 500 objects);
**Select same** selects matching assets across the map.

Windows can be dragged, resized, collapsed, or closed. Layout and keybindings
save in `Modules\LevelEditorPlus\LevelEditorPlus.settings.json`.
The vanilla Ctrl+C object clipboard also persists across restarts/maps.

## Default shortcuts

Use the **Objects** tool with the editor HUD visible.

| Action | Default |
|---|---|
| Pick fitting corner | F10 |
| Cancel fitting | F11 |
| Duplicate selection | Ctrl+D |
| Paste at cursor | Ctrl+G |
| Fit + Center | Ctrl+F10 |
| Flip facing | Ctrl+Shift+G |

In **Keybinds**, click an action and press a key combination.
Esc cancels; Delete/Backspace clears it. Changes apply immediately and save.
Duplicate module shortcuts are rejected, but vanilla game conflicts are not
checked. Rebinding cancels an active fit.

## Barricade slots

See the [asset guide](BarricadeSlots/README.md) for placement, fitting, map
sharing, Unity builds, and **Workshop publishing instructions**.

Players need the slot assets when a map uses them, but do not need this editor
module. Assets alone do not require disabling BattlEye.

## Build from source

Requires Windows, a .NET SDK, and an installed copy of Unturned.
Run from the repository root:

```powershell
dotnet build ".\LevelEditorPlus\src\LevelEditorPlus\LevelEditorPlus.csproj" -p:GamePath="C:\path\to\Unturned"
```

This installs the module into that game's `Modules\LevelEditorPlus` folder.
The default game path is configured in the project file.

Build the single-file installer using the default game path:

```powershell
& ".\Installer\Build-Installer.ps1"
```

Output: `Installer\dist\LevelEditorPlus-Setup.exe`. Publish this EXE in
**GitHub Releases**, not in the source repository.
The installer embeds the current Release build and exported slot assets.
Its build script does not install the module locally.

### Tests

```powershell
dotnet run --project ".\LevelEditorPlus\tests\OpeningGeometry.Tests\OpeningGeometry.Tests.csproj"
dotnet run --project ".\LevelEditorPlus\tests\Keybinds.Tests\Keybinds.Tests.csproj"
dotnet run --project ".\LevelEditorPlus\tests\Hierarchy.Tests\Hierarchy.Tests.csproj"
dotnet run --project ".\Installer\Tests\Installer.Tests.csproj" -- (Get-Location).Path
```

Build the installer before running its tests. Geometry/input/hierarchy tests
use managed stand-ins; installer tests use its real payload and temporary
folders. Native interactions, placement, undo, and map save/load still need
in-game testing.

## Troubleshooting

- **No toolbar:** confirm installation and launch without BattlEye.
- **Duplicate asset GUIDs:** use one asset source, not Workshop + Sandbox +
  embedded copies together.
- **Window problems:** use the default uGUI menu renderer. Close/collapse
  module windows if they cover the pause menu.
- **Missing assets:** restart after installing assets; toggle an Asset Browser
  filter if the list was built before loading finished.
- **After game updates:** the module may need rebuilding or API changes.
  Check the Console or `Unturned\Logs\Client.log` for errors.

Landscape-hole enhancements and material tools are not currently implemented.
