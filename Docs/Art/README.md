# Runtime art sources

**Flat gate.** `FlatGateAtlas.svg` is the editable vector source, drawn to match the approved Courtyard Kit / Flat Gate reference. Export the whole page as a transparent 512 × 512 sRGB PNG to `Assets/DumpsterDynasty/Resources/RaccoonRowFlatGate.png`.

Keep the 128 × 128 view box and registered cells: left leaf (2, 24, 42, 90), right leaf (46, 24, 42, 90), and shared stationary post (90, 14, 24, 100). Coordinates are left, top, width and height. The paw badge is on the left leaf only. At runtime each leaf projects inward from its hinge; keep the pair’s open clearance wider than the silhouette through both crossings.

**Courtyard furnishings.** `CourtyardFurnishings.svg` is the editable 256 × 128 source for the drawer, shallow upgrade canopy, three staggered blankets, Snack Stash, Sort Station, whole/bitten cookie, and salvage washer. Export the full transparent page at 1024 × 512 to `Assets/DumpsterDynasty/Resources/RaccoonRowFurnishings.png`. Preserve padding and cell positions; the corresponding rectangles are in `CourtyardView`. Cookie art aligns with the same tray anchor throughout pickup, carry and the seated bite. Sort bench art and the washer stay anchored to the footprint center; no added income or resident specialization.
