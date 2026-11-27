# Raccoon Row

A separate, clean Unity 6000.3.15f1 project. The previous Dumpster Dynasty project remains the source archive and is not edited by this prototype.

1. In Unity Hub choose **Add → Add project from disk** and choose this folder.
2. Open `Assets/DumpsterDynasty/Scenes/Courtyard.unity` if it is not already open.
3. Set the Game view to **9:16 (Portrait)** and press **Play**.
4. Choose **Build → Snack Stash**, tap a green snap point to preview its footprint, then choose **Place**. Cancel leaves the yard and Scrap unchanged. Watch Scout take a cookie and eat nearby while the other raccoon continues its routine.
5. Choose **Scavenge** to send the Hauler to Back Alley. The yard remains usable during the trip. When the Hauler returns, open **Collection** to see the first Blue Bottle Cap. Spend the first haul to **Upgrade Nest Pile**, then **Welcome Pip**.
6. Open **Build** again to move the Snack Stash for free or, after recruitment and another ordinary haul, place the optional Sort Station. Stop and restart normal Play Mode to reset.

This is a **layout and behavior blockout**, not production visuals. The Scout, Hauler and Pip markers are exact copies of approved portrait thumbnails. The little bob, roam, rest tags and prop cues are placeholders for full-body movement and object interactions. The yard and the opening/closing gate use simple blockout shapes. No art from the legacy sprite/animation systems was imported.

See [Game Vision](Docs/GameVision.md), [Current Scope](Docs/CurrentScope.md), and [Decision Record](Docs/DecisionRecord.md).

## Verification

The editor-only `DumpsterDynasty.Editor.CourtyardSmoke.Run` entry point opens the saved scene, starts Play Mode, and checks scene bindings, independent crew movement, rejected and cancelled placement, the one-time purchase during a trip, the Scout's interaction, both gate crossings, the countdown and exactly one loot award. The Nest and cookie interaction are checked across all 21 available snap points, and the checks cover spacing, bounds and migration from older three-plot saves; and the first find cannot be awarded twice. In-memory fixtures also exercise both reward tiers and repeat trips. Use `-batchmode -nographics -projectPath <this folder> -executeMethod DumpsterDynasty.Editor.CourtyardSmoke.Run -logFile <path>` with this version of Unity (omit `-quit`; the smoke test exits when done). Success logs `DD_SMOKE_PASS`. This checks behavior in the saved scene, not graphical touch hit targets on a phone.

## Saves and device verification

Native builds checkpoint local progress, restore a valid backup if the primary is damaged, and settle pending trips on a return without replaying cutscenes or animations. To deliberately preview persistence on the Mac, launch Unity with `-rr-save-preview`; it uses a separate Editor test folder. The default Play Mode and smoke run do not write player saves. The batch test uses temporary fixture files and simulated short and 24-hour absences. The first iOS TestFlight build launched on an iPhone. Build 1 preceded the 60 fps target change. Sustained frame rate and real-device background/relaunch are not yet verified.


## Mobile frame rate

The courtyard requests 60 fps on iOS and Android. Most non-interactive drawing runs only on Unity's repaint pass, and changing HUD labels are reformatted only when their displayed values change. Device refresh, power/thermal limits, and rendering cost can still lower the actual frame rate. The smoke test checks the configured target for a mobile active build target; measure frame times on hardware before treating 60 fps as verified.
