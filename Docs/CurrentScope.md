# Current scope

## Implemented Courtyard prototype

- One portrait courtyard with skyline, flat gate blockout and sparse weeds.
- The starting Nest Pile has two staggered rest spots. The Hauler starts resting while the Scout roams.
- The player can open or cancel placement, or spend two prototype scrap to place one Snack Stash on the courtyard grid.
- The Scout independently walks to the new building and uses it; the other raccoon continues its routine. The Scout takes a cookie, carries it to a separate spot and sits to eat. The poses and prop are still blockout art.
- One repeatable Back Alley trip with the Hauler, physical departure and return, a compact corner timer and one scrap award on return and a guaranteed first Blue Bottle Cap, visible in the small Collection panel. The Scout and the build controls keep working while the Hauler is away.
- The first common haul funds the Nest upgrade, which adds a third nook. One guaranteed food welcomes Pip, a third resident using an approved thumbnail, not a role/class. Pip walks to the new nook and rests there.
- Move the Snack Stash to an empty plot free of charge. If the Scout hasn't taken the cookie yet, it finds the new Stash; a cookie already in hand stays with it.
- After recruitment, a second ordinary trip can fund the optional Sort Station. Pip visits and moves a prop without an assignment or a resource boost.
- Builds save progress locally and can finish an existing scavenge while away without rerolling. The current away-income rule is one scrap per five minutes, capped at 12. Rewards are checkpointed and a prior good save can recover a corrupt primary. Normal Unity Play Mode starts fresh.

- The first-visit interface uses a compact, icon-first Scavenge / Build / Collection dock. The Build tray shows Snack Stash price or free relocation, reveals Sort Station after recruitment, and shows a placement-only grid with a footprint preview and separate confirmation. Short contextual prompts replace a tutorial modal; the economy, gate route and save format did not change.

- The prototype grid is 8×4 cells with 21 candidate centers for the two 2×2 movable furnishings. A one-cell gap between buildings, a front approach lane, and a separate lane to the gate preserve access. The Nest stays fixed outside the grid. Version 1 saves migrate to equivalent left, middle and right positions without resetting progress.

## First playable target and remaining validation

- One compact courtyard with two starting raccoons. The first minute should show autonomous activity, then a Snack Stash placement and a readable approach, cookie pickup and nearby seated snack. Moving the Stash should preserve access.
- Cap the slice at three base structures (Nest Pile, Snack Stash, Sort Station) and one Nest Pile upgrade. Start with the Nest. The upgrade adds a readable ground-sleep nook and room for a third raccoon. The Sort Station is an optional improvement with a simple visible sorting action.
- A gate trip yields scrap and a real collectible, with the first discovery easy to see. A typical fresh visit should allow the Nest upgrade and third recruit without relying on a rare drop. Keep the collection view limited to items that can actually be obtained. Costs and tuning remain open; Food for recruitment and Scrap for building are candidates.
- Show helpful offline gains without upkeep or replaying offscreen animations. Verify saves after resume and relaunch, one-time trip/offline credit, and recovery from a valid backup on a disposable profile.
- Do routine checks internally. Check the whole loop on a phone at a meaningful milestone, including portrait readability, touch, pathing and background/resume. Ask John to review significant changes to direction, not each iteration.

Scavenging direction retained: physical departure/return; preset zone rewards; no mid-trip choices. Use small informational corner countdowns showing thumbnail and crew count; no separate active-scavenge screen. Dispatch rules and tuning remain open.

Prototype-only trip tuning: one crew member per trip, one active trip at a time, a 12-second away timer and a zone loot table of 80% two scrap / 20% four scrap. No final crew rules or concurrent trips yet. TestFlight build 1 launched on the owner’s iPhone. The locally updated HUD and 60 fps target are not in that build; updated device touch, sustained frame rate and real background/relaunch remain unverified.
