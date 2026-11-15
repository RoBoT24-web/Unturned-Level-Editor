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
| Assets | Search assets, choose 16/32/64/128 px previews, and choose one for placement |
| Fit slot | Fit and center a selected custom slot, manually or with an auto-fit preview |
| Keybinds | Change shortcuts live |
| Slots | Toggle colored slot outlines and facing arrows |

In **Assets**, the preview slider selects 16, 32, 64, or 128 px and saves
the chosen size. Some assets without a renderable model may only show a name.

**Finding nearby slots:** in Hierarchy, search `Barricade Slot`, enable
**Nearby only**, and adjust the radius (initially 25 m). **Nearest first**
sorts by camera distance. Results update as you move.
Click to select, Ctrl+click to add to selection, double-click to fly there.
**Select results** uses the filtered list (up to 500 objects);
**Select same** selects matching assets across the map, also up to 500 objects.

**Slots** shows the nearest 512 slot outlines within 50 m of the editor camera,
refreshing every two seconds.

**Exporting the entire hierarchy:** **Export detailed** saves one CSV row per
placement with Name, Asset ID, Asset GUID, Category, and Asset Source. An
accompanying `.status.txt` file reports the overall missing-asset placement count,
not a status column on every row. Unknown sources are marked `unknown`; unavailable
GUIDs are left blank.
**Export summary** saves one row per distinct asset with Name, Asset ID,
Category, and Quantity. Assets are grouped by GUID (or ID when unavailable), with
objects and buildable items kept separate, rather than by name alone.
Both exports include all map objects and editor buildables, including missing
assets, ignore search/nearby filters, and have no 500-object selection limit.
They do not include terrain, foliage, or spawnpoints outside this Hierarchy.
Timestamped UTF-8 files are saved in the map's `HierarchyExports` folder;
the Console logs the full export path.

Windows can be dragged, resized, collapsed, or closed. Layout and keybindings
save in `Modules\LevelEditorPlus\LevelEditorPlus.settings.json`.
The vanilla Ctrl+C object clipboard also persists across restarts/maps.

## Default shortcuts

Use the **Objects** tool with the editor HUD visible.

| Action | Default |
|---|---|
| Pick fitting point / confirm preview | F10 |
| Cancel fitting | F11 |
| Duplicate selection | Ctrl+D |
| Paste at cursor | Ctrl+G |
| Start auto-fit or manual Fit + Center | Ctrl+F10 |
| Flip facing | Ctrl+Shift+G |

**Always auto-fit** is on by default: select a slot and press **F10** on a frame
face to start and pick it immediately, then **F10** inside the opening and again
to confirm. No start button or Ctrl+F10 is needed between slots.
Turn off **Always auto-fit**, or choose **Manual Fit + Center** / **Center only**,
to disable automatic starting. The mode is saved across restarts.
**Auto-fit preview** turns it back on; **Ctrl+F10** starts auto-fit when enabled,
or manual Fit + Center when disabled. To use Center only, click its button.

In **Keybinds**, click an action and press a key combination.
Esc cancels; Delete/Backspace clears it. Changes apply immediately and save.
Duplicate module shortcuts are rejected, but vanilla game conflicts are not
checked. Rebinding cancels an active fit.

## Barricade slots

See the [asset guide](BarricadeSlots/README.md) for placement, fitting, and map
sharing.

Players need the slot assets when a map uses them, but do not need this editor
module. Assets alone do not require disabling BattlEye.

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
