# BarricadeSlots

Invisible map objects that provide attachment points for vanilla doors, gates,
window barricades, and hatches. They do not remove or modify existing buildings.

## Install

Use **one** source for the assets:

- **Workshop:** subscribe to the pack once it is published, then restart.
  A public Workshop link has not been added yet.
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
   arrows. Select the small marker above the opening, or use Hierarchy.
4. Open **Fit slot** and click **Fit + Center**.
5. Pick **bottom-left**, **bottom-right**, then **top-left** inside corners
   using **F10** (or your configured pick key).
6. Save, then test placement of the actual vanilla item in singleplayer.

Pick solid frame edges on the same face, not the empty opening.
Measurements use collision surfaces, so they may differ from visible edges.

| Setting | What it does |
|---|---|
| Inset (m) | Shrinks each edge inward; `0.005` = 5 mm per edge |
| Depth (m) | Moves the slot relative to the picked face; negative goes into the wall |
| Upright / wall-aligned | Keeps normal frames level/vertical; disable for slopes or hatches |
| Center only | Moves the slot without resizing or rotating it |
| Flip facing | Reverses facing without moving the slot center |

Upright fitting ignores a side-face direction if it disagrees with your
left-to-right picks. To center in a 0.2 m thick wall, try depth `-0.1`.
Use F11 to cancel and Ctrl+Z to undo. Fit + Center defaults to Ctrl+F10;
Flip facing defaults to Ctrl+Shift+G. Change these live in **Keybinds**.

**Resizing a slot does not resize the vanilla door, glass, shutter, or board.**
Always test the actual barricade's visual fit.

## Publish the pack to Workshop

You need Steam signed in and a PNG/JPG preview image (use your own screenshot).
No module DLLs, SDK source, or installer should be included.

1. From the repository root, prepare the upload:

   ```powershell
   & ".\BarricadeSlots\Prepare-Workshop.ps1"
   ```

2. Launch Unturned and choose **Workshop > Submit**.
3. Fill in:

   | Field | Value |
   |---|---|
   | Name | `Barricade Slots` |
   | Collection Path | Full path to `BarricadeSlots\WorkshopUpload` printed by the script |
   | Preview Image | Full path to your PNG/JPG screenshot |
   | Change Note | `Initial release` |
   | Asset Type | Object |
   | Visibility | Private for the initial test |
   | Allowed IPs | Leave blank |
   | Workshop Section | Ready-to-Use |

4. Accept Steam's Workshop terms if prompted, then click **Create**.
5. Open the new Workshop page and add the description below.
6. Close the game. Move your local `Sandbox\BarricadeSlots` copy outside the
   game folder, and test using the Workshop copy only. Also avoid testing on a
   map containing an embedded copy.
7. Subscribe if needed, restart, and confirm all four variants appear and
   actual barricades can be placed. Test saving and reopening a map.
8. Once verified, change visibility to **Public** on the Workshop page.
   Add its URL to this guide and your GitHub project description.

The generated collection contains only the exported pack:

```text
WorkshopUpload\
  BarricadeSlots\
    MasterBundle.dat
    barricadeslots.masterbundle
    Objects\
```

**Updates:** rerun the preparation script, fill in Collection Path and a change
note in Submit, then select your existing published item at the bottom.
Do not create a new item for each update or change the asset GUIDs.

Official reference: [Unturned Workshop publishing](https://docs.smartlydressedgames.com/en/latest/about/steam-workshop.html#publishing-mods).

### Suggested Workshop description

> Invisible attachment-point objects for vanilla doors, gates, window
> barricades, and hatches. For map creators: search "Barricade Slot" in the
> editor's Objects tab.
>
> The optional LevelEditorPlus editor module provides outlines and fitting
> tools. It is installed separately, not included in this Workshop pack.
> Players do not need the module or to disable BattlEye to use these assets.
>
> Slot scaling does not resize vanilla barricades. Slots do not remove existing
> window bars or alter building meshes. Do not install duplicate Sandbox or
> embedded copies of this pack.

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
