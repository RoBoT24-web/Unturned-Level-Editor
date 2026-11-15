# BarricadeSlots

Invisible map objects that provide attachment points for vanilla doors, gates,
window barricades, and hatches. They do not remove or modify existing buildings.

## Install

Use **one** source for the assets:

- **Workshop:** subscribe to the [workshop mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3838037462), then restart.
- **Installer:** enable the optional BarricadeSlots checkbox.
- **Manual:** copy [Content/BarricadeSlots](Content/BarricadeSlots) into
  `Unturned\Sandbox\BarricadeSlots`, or run:

  ```powershell
  .\BarricadeSlots\Install-Content.ps1 -GamePath "C:\path\to\Unturned"
  ```

Do not load the same GUIDs from Workshop, Sandbox, and a map's embedded assets
at the same time.

## Place and fit

1. Open a map in the editor and search **Barricade Slot** in Objects.
2. Place the Door, Gate, Window, or Hatch variant.
3. With LevelEditorPlus installed, enable **Slots** to see outlines and facing
   arrows for the nearest 512 slots within 50 m of the editor camera. Select the
   small marker near the opening, or use Hierarchy.
4. With **Always auto-fit** enabled (the default), press **F10** on a solid
   front frame face to start auto-fit and pick the face immediately.
5. Press **F10** inside the empty opening.
   Check the preview and press **F10** again to apply.
6. Save, then test placement of the actual vanilla item in singleplayer.

Measurements use collision surfaces, so they may differ from visible edges.
For manual fitting, open **Fit slot**, click **Manual Fit + Center**, and pick
solid **bottom-left**, **bottom-right**, then **top-left** inside frame corners
on the same face using **F10** (or your configured pick key).

| Setting | What it does |
|---|---|
| Inset (m) | Shrinks each edge inward; `0.005` = 5 mm per edge |
| Depth (m) | Moves the slot relative to the picked face; negative goes into the wall |
| Upright / wall-aligned | Keeps normal frames level/vertical; disable for slopes or hatches |
| Center only | Moves the slot without resizing or rotating it |
| Flip facing | Reverses facing without moving the slot center |

Upright fitting ignores a side-face direction if it disagrees with your
left-to-right picks. To center in a 0.2 m thick wall, try depth `-0.1`.
Use F11 to cancel and Ctrl+Z to undo. Ctrl+F10 starts auto-fit when enabled or
manual Fit + Center otherwise; use the button for Center only. Flip facing
defaults to Ctrl+Shift+G. Change these live in **Keybinds**.

**Resizing a slot does not resize the vanilla door, glass, shutter, or board.**
Always test the actual barricade's visual fit.

### Assisted auto-fit

For an empty rectangular opening, select one slot and open **Fit slot**:

1. Set Inset, Depth, and Upright as for manual fitting.
2. Select a slot, aim at a solid **front face** of its frame, and press **F10**.
   With **Always auto-fit** on, this starts fitting and picks the face in one step,
   even with the fitting window closed.
3. Aim inside the empty opening on that face and press **F10** again.
4. Check the cyan rectangle and facing line. Press **F10** or click
   **Confirm preview** to apply. **F11** / **Cancel** leaves the slot unchanged;
   **Ctrl+Z** undoes an applied fit.

Auto-fit stays enabled between slots. **Manual Fit + Center**, **Center only**,
or turning off **Always auto-fit** disables automatic starting; the preference
is saved. Click **Auto-fit preview** or enable **Always auto-fit** to restore it.
**Ctrl+F10** explicitly starts auto-fit or manual Fit + Center, not Center only.

Auto-fit probes collision surfaces near the picked plane (up to 15 cm behind
it), searching up to 6 m from your interior pick in each direction. It checks
for rectangular edges and obstructions, but collision meshes may not match
visible geometry. Frame features thinner than the 2 cm scan step or small
obstructions between validation samples can be missed: always inspect the
preview and test the actual item.
Closed doors, irregular/arched openings, missing frame edges, or unsuitable
colliders need **Manual Fit + Center**. Detection failures do not change the
slot; pick inside again or switch to manual fitting. For hatches/slopes, disable
**Upright / wall-aligned** and orient the slot's height along the opening before
starting. Restart the preview if you change the fitting offsets.

## Share a map

Slot objects save into the map by GUID. Players and servers need their asset
definitions, but not the editor module. Player-placed barricades use normal
game/server save data.

Choose one distribution method:

- **Workshop dependency:** add this pack to the map's Required Items.
  Players must subscribe/download it; server owners must configure it too.
- **Self-contained map:** embed the pack into the map's Bundles folder:

  ```powershell
  .\BarricadeSlots\Embed-InMap.ps1 -MapPath "C:\path\to\Unturned\Maps\MyMap"
  ```

  The map then carries the assets. Remove the duplicate Sandbox copy (the
  script supports `-RemoveSandboxCopy`) and avoid a Workshop copy too.

## Advanced: rebuild the assets

Only needed to change the prefabs, not to install or use the exported pack.

1. Clone the [official U3-SDK](https://github.com/SmartlyDressedGames/U3-SDK)
   beside this folder. Use Unity **2022.3.62f3** for this project's current
   export workflow; check the SDK version if using a newer checkout.
2. Copy [BarricadeSlotsGenerator.cs](UnityScript/BarricadeSlotsGenerator.cs)
   into the SDK's `Assets\Editor` folder.
3. In Unity, select **Tools > Barricade Slots > 1. Generate Prefabs**.
4. Select **2. Export Master Bundle...** and choose
   `BarricadeSlots\Content\BarricadeSlots`.
5. Reinstall locally or prepare a Workshop update. Keep the existing GUIDs.

| Variant | Default opening |
|---|---|
| Door | 1.1 x 2.0 m |
| Gate | 3.6 x 2.6 m |
| Window | 1.1 x 0.95 m |
| Hatch | 1.2 x 1.2 m, flat |

Legacy Y-up exports must be regenerated with the current Z-up generator.
Editor-module changes alone do not require a Unity export.
