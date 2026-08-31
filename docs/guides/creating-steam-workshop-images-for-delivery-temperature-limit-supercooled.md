# Creating Steam Workshop images for Delivery Temperature Limit (Supercooled)

This document is the production brief for the Steam Workshop preview and screenshot carousel for **Delivery Temperature Limit (Supercooled)**.

## Recommendation

Replace the old four-screenshot plan. It had the right goal—show the gameplay benefit—but several shots were either hard to reproduce or no longer matched the mod:

- The Workshop preview image and the screenshot carousel serve different purposes and should be designed separately.
- A stock **Storage Bin** is a safer subject than a Smart Storage Bin or any modded building.
- The temperature overlay shows the surrounding thermal scene; it does not, by itself, prove the exact temperature of carried debris.
- A simultaneous “hot delivery refused, cold delivery accepted” action shot depends on Duplicant timing. A controlled before/after state is clearer and repeatable.
- Construction limits are configured in the blueprint **material-selection panel**, not in a normal building side screen while the blueprint is being dragged.
- The options screen changes more often than the core gameplay and should not displace the rocket Storage Tile demonstration.

The strongest asset set is one square preview plus four 16:9 gameplay screenshots:

| Order | Asset | What it must communicate |
| --- | --- | --- |
| Preview | `Preview.png` | The mod controls delivery temperature, even when viewed as a small Workshop thumbnail. |
| 1 | Set a safe range | The controls are native-looking, simple, and attached to a normal Storage Bin. |
| 2 | Cold accepted, hot rejected | The configured range changes actual delivery behavior. |
| 3 | Rocket Storage Tile | The feature works in compact rocket interiors, not only in ordinary colony storage. |
| 4 | Construction limit | The same protection can filter materials before a blueprint is built. |
| Optional 5 | Mod options | The two behavior toggles and construction defaults are configurable. |

If only four carousel slots are maintained, omit the options screenshot—not the rocket or construction screenshots.

## Asset briefs

### Preview: readable at thumbnail size

**Format:** square, preferably `512 × 512`, and under `1 MB`.

Use a clean crop of the cold-storage scene rather than a full UI screenshot. Show a stock Storage Bin between clearly cold and hot Dirt piles. Add one short title, such as **TEMPERATURE-SAFE DELIVERIES**, plus a simple accepted/rejected visual cue. The title and symbols must still be legible when the image is displayed at roughly 200 pixels wide.

Do not put small settings text, a version number, the sandbox toolbar, notification clutter, or third-party buildings in the preview. Preserve a clean, unannotated master before creating the square crop.

### Screenshot 1: set a safe range

**Scene:** a stock Storage Bin beside a small Sleet Wheat growing area or another visibly cold room.

**State to show:**

- The Storage Bin is selected.
- Only **Dirt** is enabled in its storage filters.
- The mod's temperature controls are open and unobstructed.
- The range is set to `-50 °C` through `5 °C`.
- The game is using Celsius so the values match the example.

Frame the bin, a little of the cold-use context, and the complete side screen. This is the “how to use it” image. It should not need arrows if the controls are already prominent. If an annotation is necessary, use one small callout—never a giant ellipse over the interface.

Suggested caption: **Set the temperature range on any supported delivery target.**

### Screenshot 2: cold accepted, hot rejected

Use the same bin and camera position so the viewer can compare it directly with screenshot 1.

**State to show:**

- The bin has free capacity and contains the cold Dirt created for the test.
- A hot Dirt pile remains reachable on the floor.
- Hovering or selecting the remaining pile shows its exact temperature.
- The bin still shows the `-50 °C` to `5 °C` range.
- With the status-warning option enabled, the yellow **Lacks Resources** status can supply additional evidence that all remaining Dirt is outside the allowed range.

The deterministic after-state is the proof: cold Dirt was eligible and delivered; hot Dirt was not. Do not rely on catching two Duplicants mid-haul, and do not present the ambient temperature overlay as proof of a debris item's exact temperature.

Suggested caption: **−20 °C delivered. 95 °C rejected.**

### Screenshot 3: rocket Storage Tile

**Scene:** the interior of a Spaced Out! rocket with a stock Storage Tile built into a believable compact layout.

**State to show:**

- The Storage Tile is selected.
- Its temperature range is visible.
- Enough of the rocket-interior border and furnishings remain in frame to make the location unmistakable.
- The UI does not cover the Storage Tile itself.

This is more valuable than a generic settings screenshot because it demonstrates a distinctive supported target and an important use case: preventing a small rocket cabin from accepting excessively hot material.

Suggested caption: **Temperature limits also work on Storage Tiles—even inside rockets.**

### Screenshot 4: construction-material limit

**Scene:** a cold-room wall or other temperature-sensitive build area, with **Insulated Tile** selected from the Build menu.

**State to show:**

- **Apply Limits to Construction Materials** was enabled before the game was restarted.
- The blueprint material-selection panel is open.
- A valid construction material, such as Igneous Rock, is selected or clearly available.
- The injected construction range shows `-50 °C` through `20 °C`.
- The screenshot is taken before placement, while the material-selection panel is still visible.

Do not instruct the player to open a normal side screen while dragging a blueprint. The relevant integration is the material-selection panel used to choose the blueprint's construction material.

Suggested caption: **Keep hot construction materials out of cold builds.**

### Optional screenshot 5: current options

Capture this only after the four gameplay images are complete. Show the current labels exactly as they appear in game:

- **Include Temperature in "Lacks Resources" Warning**
- **Apply Limits to Construction Materials**
- **Default Max Construction Temperature**
- **Default Min Construction Temperature**

Do not add a version number to the image. Options screenshots become stale quickly; recapture this one whenever labels or layout change.

## Fast, repeatable game setup

The following workflow uses a disposable sandbox colony. It avoids waiting for research, mining suitable debris, cooling materials, or launching a rocket.

### 1. Preflight at the main menu

1. Set the game to `1920 × 1080` or higher at a 16:9 aspect ratio.
2. Use an interface scale around `100–110%`: large enough to read, but small enough to keep the full side screen in frame.
3. Set the temperature unit to Celsius.
4. In the Mods menu, enable exactly one copy of **Delivery Temperature Limit (Supercooled)**. If both a local-development copy and the subscribed Workshop copy exist, leave one disabled.
5. Disable unrelated UI, storage, capacity, texture, and notification mods for the capture session. They can make the screenshots misleading or visually date them.
6. Open this mod's options and set:
   - **Include Temperature in "Lacks Resources" Warning:** enabled
   - **Apply Limits to Construction Materials:** enabled
   - **Default Min Construction Temperature:** `-50 °C`
   - **Default Max Construction Temperature:** `20 °C`
7. Restart the game once. These options are restart-required.

### 2. Create a disposable capture colony

1. Start a new custom **Spaced Out!** game so the same save can produce the rocket-interior screenshot.
2. Choose **No Sweat** mode and enable **Sandbox Mode**. The starting asteroid and seed are not important.
3. Enter the colony, pause immediately, and save it as `Delivery Temperature Limit - Workshop Images - Clean`.
4. Press `Shift+S` when you need to toggle Sandbox Mode. Sandbox provides instant building, map reveal, and controlled resource spawning; turning it off again does not remove what was created.
5. Use the sandbox reveal and dig tools to clear one compact, well-lit staging area. Keep the Printing Pod, alerts, rockets, and unrelated machinery outside the frame.

Make a second save, `Delivery Temperature Limit - Workshop Images - Working`, before hauling begins. Returning to this checkpoint is faster than rebuilding the scene after an unwanted delivery.

### 3. Build the shared storage test scene

While paused and in Sandbox Mode:

1. Build one stock **Storage Bin** on an accessible floor.
2. For a recognizably cold context, add a short row of Farm Tiles with Sleet Wheat behind or beside it. The plants are visual context; they do not need to be mature.
3. Leave clear floor space on both sides of the bin for two Dirt piles and make sure a Duplicant can reach everything.
4. Select the sandbox **Sprinkle** tool—not the Brush tool—then create:
   - `200 kg` of Dirt at `-20 °C`
   - `200 kg` of Dirt at `95 °C`
5. Put the two piles far enough apart that each can be hovered and read cleanly.
6. Configure the Storage Bin:
   - storage filter: Dirt only
   - capacity: `1000 kg`
   - priority: `9`
   - minimum temperature: `-50 °C`
   - maximum temperature: `5 °C`

The spare capacity is important. If the bin becomes full after accepting the cold Dirt, the hot pile remaining outside no longer proves temperature filtering.

### 4. Capture screenshots 1 and 2

For screenshot 1:

1. Keep the game paused.
2. Select the Storage Bin and open the temperature controls.
3. Frame the stock bin, the cold-use context, and the complete `-50 °C` to `5 °C` control range.
4. Capture a clean master.

For screenshot 2:

1. Turn Sandbox Mode off so normal errands run.
2. Unpause and allow a Duplicant to deliver the `-20 °C` Dirt.
3. Wait until the cold Dirt is stored and the `95 °C` Dirt is the only eligible material type still on the floor.
4. Confirm that the bin still has free capacity.
5. Pause again. Select the bin, keep the configured range visible, and hover or select the hot pile so `95 °C` is readable.
6. If it appears cleanly, include the temperature-aware **Lacks Resources** status in frame.
7. Capture the proof master before adding the short accepted/rejected caption.

If the wrong state develops, reload `Delivery Temperature Limit - Workshop Images - Working`; do not spend time repairing the scene.

### 5. Capture the construction screenshot

1. Confirm the construction option was enabled before the last restart.
2. Pause and enable Sandbox Mode again.
3. Open **Build → Base → Insulated Tile** near the cold-room scene.
4. Keep the blueprint's material-selection panel open and choose a material such as Igneous Rock.
5. Set or confirm the construction range is `-50 °C` through `20 °C`.
6. Compose the scene before placing the blueprint, with the full material-selection panel readable.
7. Capture the master.

A hot Igneous Rock pile in the background can add context, but the panel and its range are the primary evidence.

### 6. Capture the rocket screenshot without launching

1. In Sandbox Mode, reveal and clear the surface of the starting asteroid.
2. Build a Rocket Platform, a compatible engine, and a Spacefarer Module. Sandbox construction removes the research and material wait.
3. Select the Spacefarer Module and use **View Interior**. A launch is not required.
4. Inside the module, build a stock **Storage Tile** and a few ordinary furnishings so the scene reads as an intentional cabin rather than a test chamber.
5. Select the Storage Tile, enable a simple material filter such as Dirt, and set a visible temperature range.
6. Frame both the tile and enough of the rocket-interior boundary to establish location, then capture the master.

If the rocket builder becomes the slowest part of the session, save it as a reusable `Delivery Temperature Limit - Workshop Images - Rocket` checkpoint for future recaptures.

## Capture and export rules

- Capture clean masters first. Add at most one short caption and one restrained visual cue in post-production.
- Use `F12` for Steam screenshots when the Steam overlay is active. Check the saved image immediately after the first shot before staging the remaining scenes.
- Do not use `Alt+S` for UI screenshots; Screenshot Mode hides the interface the images need to demonstrate.
- Avoid `F3` in the evidence shots. The thermal overlay is useful for ambience, but exact debris temperatures should come from the item's own tooltip or selection panel.
- Clear **NEW** badges, tutorial prompts, sandbox controls, red alerts, tooltip collisions, and the mouse cursor from the subject area.
- Keep all essential text comfortably inside the frame so Steam's responsive page layout cannot crowd it.
- Keep an unannotated 16:9 master for every image. Export Workshop copies as high-quality JPEGs, approximately `85–90%` quality, and verify that every uploaded preview is under `1 MB`.
- Check every final image at both full size and thumbnail size. If the key point is not obvious within two seconds, simplify the crop or caption.
- Do not show third-party buildings, extreme storage capacities, development tools, or stale mod versions.

Suggested working filenames:

```text
Preview.png
steam-workshop-01-temperature-range-controls.jpg
steam-workshop-02-temperature-filtering-proof.jpg
steam-workshop-03-rocket-storage-tile-support.jpg
steam-workshop-04-construction-material-limits.jpg
steam-workshop-05-mod-options.jpg                      # optional
```

The repository's Workshop pipeline publishes `Preview.png` as the listing preview. Add and order the 16:9 carousel images separately through the Workshop item's owner controls.

## Upload order and final checks

Before replacing the live assets, verify:

- [ ] `Preview.png` is square, readable when small, and under `1 MB`.
- [ ] Screenshot 1 teaches where and how to set the range.
- [ ] Screenshot 2 visibly proves an in-range delivery succeeded while an out-of-range item remained rejected despite spare capacity.
- [ ] Screenshot 3 unmistakably shows a Storage Tile inside a rocket interior.
- [ ] Screenshot 4 shows the construction range in the material-selection panel before blueprint placement.
- [ ] All featured buildings and UI elements come from the base game, DLC, or this mod only.
- [ ] All labels match the currently released build.
- [ ] No capture contains sandbox chrome, unrelated mod UI, alert clutter, oversized annotations, or a stale version number.
- [ ] Each carousel file is 16:9, sharp at full size, and under `1 MB`.
- [ ] The live carousel order is: set range → prove behavior → rocket Storage Tile → construction limit → options, if included.

## Evidence used to keep this guide current

- The configured Workshop preview asset is defined in [`oni-mod-pipeline.toml`](../../mods/delivery-temperature-limit-supercooled/oni-mod-pipeline.toml).
- Current option names and restart behavior are defined in [`DeliveryTemperatureLimitOptions.cs`](../../mods/delivery-temperature-limit-supercooled/Source/DeliveryTemperatureLimitOptions.cs).
- Storage Tile support is explicit in [`TemperatureLimitedDeliveryTargetPrefabConfigurator.cs`](../../mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitedDeliveryTargets/TemperatureLimitedDeliveryTargetPrefabConfigurator.cs).
- Construction controls are integrated with the material-selection panel in [`ConstructionMaterialTemperatureLimit.cs`](../../mods/delivery-temperature-limit-supercooled/Source/TemperatureLimitedDeliveryTargets/ConstructionMaterialTemperatureLimit.cs).
- Steam's image-preview limits are documented by Valve's [ISteamUGC API](https://partner.steamgames.com/doc/api/isteamugc).
- Sandbox and shortcut behavior can be checked against the ONI Wiki's [game settings](https://oxygennotincluded.wiki.gg/wiki/Game_Settings) and [controls](https://oxygennotincluded.wiki.gg/wiki/Controls) references.
- Scene-specific context is documented on the ONI Wiki's [Sleet Wheat](https://oxygennotincluded.wiki.gg/wiki/Sleet_Wheat) and [Storage Tile](https://oxygennotincluded.wiki.gg/wiki/Storage_Tile) pages.
