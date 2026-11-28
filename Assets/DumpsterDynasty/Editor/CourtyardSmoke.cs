using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DumpsterDynasty.Editor
{
    public static class CourtyardSmoke
    {
        private const string Running = "DumpsterDynasty.CourtyardSmoke.Running";
        private static readonly int Left = CourtyardGrid.FromLegacyPlot(0);
        private static readonly int Middle = CourtyardGrid.FromLegacyPlot(1);
        private static readonly int Right = CourtyardGrid.FromLegacyPlot(2);
        private static double started;
        private static Vector2 initialResident, initialTraveler;
        private static int phase;
        private static bool sawDepartureGate, sawReturnGate, sawSnackingDuringTrip;
        private static float lastRemaining;
        private static int reward;

        public static void Run()
        {
            // A blank project's first-launch index must finish its startup callback
            // before entering Play Mode, or Unity skips creating the default Search index.
            EditorApplication.delayCall += () =>
            {
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        var furnishings = Resources.Load<Texture2D>("RaccoonRowFurnishings");
                        Require(furnishings != null && furnishings.width == 1024 && furnishings.height == 512,
                            "Courtyard furnishings atlas is missing or invalid.");
                        VerifyPresetTable();
                        VerifyGateChoreography();
                        VerifySharedResidentActions();
                        VerifyNestAndSnackSpots();
                        VerifyGrowthWithCommonLoot();
                        VerifyMovingStashAndSorting();
                        VerifyGridAndLegacyMigration();
                        VerifySavesAndAwayReturn();
                    }
                    catch (Exception error)
                    {
                        Debug.LogException(error);
                        Finish(1);
                        return;
                    }
                    EditorSceneManager.OpenScene(CourtyardSetup.ScenePath);
                    SessionState.SetBool(Running, true);
                    EditorApplication.isPlaying = true;
                };
            };
        }

        [InitializeOnLoadMethod]
        private static void Register()
        {
            if (!SessionState.GetBool(Running, false)) return;
            EditorApplication.update += Verify;
        }

        private static void Verify()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                var view = UnityEngine.Object.FindFirstObjectByType<CourtyardView>();
                Require(view != null && view.Model != null, "Saved scene did not start the runtime controller.");
                CourtyardModel model = view.Model;
                switch (phase)
                {
                    case 0:
#if UNITY_IOS || UNITY_ANDROID
                        Require(Application.targetFrameRate == 60, "The saved scene did not request 60 fps for mobile.");
#endif
                        Require(Camera.main != null && Camera.main.orthographic, "Saved courtyard camera missing.");
                        var bindings = new SerializedObject(view);
                        Require(bindings.FindProperty("greenPortrait").objectReferenceValue != null &&
                            bindings.FindProperty("orangePortrait").objectReferenceValue != null &&
                            bindings.FindProperty("pipPortrait").objectReferenceValue != null,
                            "Approved portraits were not linked.");
                        started = EditorApplication.timeSinceStartup;
                        initialResident = model.ResidentAt(0).Position;
                        initialTraveler = model.ResidentAt(1).Position;
                        Require(model.BeginPlacement(), "Build cannot start.");
                        Require(!model.TryPlaceStash(-1), "Invalid plot was accepted.");
                        model.CancelPlacement();
                        Require(model.Scrap == CourtyardModel.SnackCost && model.StashPlot == -1, "Cancel changed the yard.");
                        Require(model.ResidentAt(1).Activity == CourtyardModel.Activity.Rest &&
                            model.ResidentAt(1).Position == CourtyardModel.NestSpot(1), "Courtyard did not start with the traveler resting in the Nest.");
                        Require(!model.HasBottleCap && model.CrewCount == 2 && model.NestCapacity == 2 &&
                            !model.TryUpgradeNest() && !model.TryRecruit(), "Growth unlocked or spent resources too early.");
                        Require(model.TryStartScavenge() && !model.TryStartScavenge(), "Trip did not start once.");
                        lastRemaining = model.ScavengeRemaining;
                        phase = 1;
                        break;
                    case 1:
                        double elapsed = EditorApplication.timeSinceStartup - started;
                        if (elapsed > 45) throw new Exception("Timed out waiting for the full scavenging trip.");
                        Require(!float.IsNaN(model.ScavengeRemaining) && model.ScavengeRemaining >= 0 &&
                            model.ScavengeRemaining <= lastRemaining, "Timer did not count down correctly.");
                        lastRemaining = model.ScavengeRemaining;
                        if (model.ScavengeStage == CourtyardModel.TripStage.Departing)
                            sawDepartureGate |= model.GateOpen;
                        if (model.ScavengeStage == CourtyardModel.TripStage.Scavenging)
                        {
                            Require(!model.GateOpen, "Gate stayed open after departure.");
                            if (model.StashPlot < 0)
                            {
                                Require(model.BeginPlacement() && model.TryPlaceStash(Middle), "Could not build during a trip.");
                                Require(model.Scrap == 0 && !model.BeginPlacement(), "Purchase was not applied once.");
                            }
                            sawSnackingDuringTrip |= model.SnacksTaken > 0;
                        }
                        if (model.ScavengeStage == CourtyardModel.TripStage.Returning)
                            sawReturnGate |= model.GateOpen;
                        if (model.CompletedTrips == 0)
                        {
                            Require(!model.HasBottleCap && !model.LastTripFoundBottleCap && model.Food == 0, "First-trip rewards arrived before return.");
                            if (model.StashPlot >= 0)
                                Require(model.Scrap == 0 && model.LastTripScrap == 0, "Loot awarded before return.");
                            break;
                        }
                        Require(sawDepartureGate && sawReturnGate && !model.GateOpen, "Gate did not open and close at both crossings.");
                        Require(sawSnackingDuringTrip && Vector2.Distance(initialResident, model.ResidentAt(0).Position) > 1,
                            "Resident did not carry on at home during the trip.");
                        Require(Vector2.Distance(initialTraveler, model.ResidentAt(1).Position) > 1, "traveler did not move.");
                        reward = model.LastTripScrap;
                        Require((reward == 2 || reward == 4) && model.Scrap == reward && model.CompletedTrips == 1,
                            "Trip loot did not land exactly once.");
                        Require(model.HasBottleCap && model.LastTripFoundBottleCap && model.Food == 1, "First return did not unlock the first find.");
                        started = EditorApplication.timeSinceStartup;
                        phase = 2;
                        break;
                    case 2:
                        if (EditorApplication.timeSinceStartup - started < 1) break;
                        Require(model.Scrap == reward && model.CompletedTrips == 1 && model.HasBottleCap,
                            "Trip award changed again after returning.");
                        Require(model.TryUpgradeNest() && model.NestCapacity == 3 && model.CrewCount == 2 &&
                            model.Scrap == reward - CourtyardModel.NestUpgradeCost && !model.TryUpgradeNest(),
                            "Nest upgrade did not spend once and add one spot.");
                        Require(model.TryRecruit() && model.CrewCount == 3 && model.Food == 0 && !model.TryRecruit(),
                            "Recruit did not spend food once and add one resident.");
                        started = EditorApplication.timeSinceStartup;
                        phase = 3;
                        break;
                    case 3:
                        if (EditorApplication.timeSinceStartup - started > 10) throw new Exception("Recruit did not reach the new nest spot.");
                        if (model.Recruit.Activity != CourtyardModel.Activity.Rest) break;
                        Require(model.Recruit.Position == CourtyardModel.NestSpot(2), "Recruit rested at the wrong nest spot.");
                        Debug.Log("DD_SMOKE_PASS Furnishing atlas, courtyard grid and legacy migration, snack/relocation, gate and find, Nest/recruit, Sort Station, save recovery and offline settlement");
                        Finish(0);
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static void VerifyPresetTable()
        {
            foreach (var pair in new[] { (0f, 2), (0.799f, 2), (0.8f, 4), (1f, 4) })
            {
                int rolls = 0;
                var model = new CourtyardModel(() => { rolls++; return pair.Item1; });
                Require(model.TryStartScavenge(), "Model trip could not start.");
                for (int i = 0; i < 400; i++) model.Tick(0.1f);
                Require(rolls == 1 && model.LastTripScrap == pair.Item2 &&
                    model.Scrap == CourtyardModel.SnackCost + pair.Item2 && model.CompletedTrips == 1 &&
                    model.HasBottleCap && model.LastTripFoundBottleCap && model.Food == 1,
                    "Preset zone table selected or awarded the wrong loot.");
                Require(model.TryStartScavenge() && model.LastTripScrap == 0, "Next trip could not start cleanly.");
                for (int i = 0; i < 400; i++) model.Tick(0.1f);
                Require(rolls == 2 && model.CompletedTrips == 2 &&
                    model.Scrap == CourtyardModel.SnackCost + 2 * pair.Item2 &&
                    model.HasBottleCap && !model.LastTripFoundBottleCap && model.Food == 1, "Repeat trip awarded the wrong loot or rediscovered the first find.");
            }
        }

        private static void VerifyNestAndSnackSpots()
        {
            Require(Vector2.Distance(CourtyardModel.NestSpot(0), CourtyardModel.NestSpot(1)) > 40,
                "Rest spots do not leave readable space.");
            for (int plot = 0; plot < CourtyardGrid.AnchorCount; plot++)
            {
                if (!CourtyardGrid.Fits(plot, CourtyardGrid.Building.Stash)) continue;
                var model = new CourtyardModel();
                Require(model.BeginPlacement() && model.TryPlaceStash(plot) &&
                    model.ResidentAt(0).Activity == CourtyardModel.Activity.WalkToStash, "Resident did not respond to the first build.");
                bool pickedUp = false, carried = false, ate = false;
                for (int i = 0; i < 300 && model.SnacksTaken == 0; i++)
                {
                    model.Tick(.1f);
                    var resident = model.ResidentAt(0);
                    switch (resident.Activity)
                    {
                        case CourtyardModel.Activity.PickUpCookie:
                            pickedUp = true;
                            Require(resident.HasCookie && resident.Position == CourtyardModel.CookiePickup(plot), "Cookie did not transfer at the stash.");
                            break;
                        case CourtyardModel.Activity.CarryCookie:
                            carried = true;
                            Require(pickedUp && resident.HasCookie, "Resident carried a cookie before picking it up.");
                            break;
                        case CourtyardModel.Activity.EatCookie:
                            ate = true;
                            Require(carried && resident.HasCookie && resident.Position == CourtyardModel.CookieSeat(plot) &&
                                Vector2.Distance(resident.Position, CourtyardModel.CookiePickup(plot)) > 35, "Resident did not sit separately with the cookie.");
                            break;
                    }
                }
                Require(pickedUp && carried && ate && model.SnacksTaken == 1 && !model.ResidentAt(0).HasCookie,
                    "Snack must count once, after the seated bite.");
                for (int i = 0; i < 120 && model.ResidentAt(0).Activity != CourtyardModel.Activity.Rest; i++) model.Tick(.1f);
                Require(model.ResidentAt(0).Activity == CourtyardModel.Activity.Rest && model.ResidentAt(0).Position == CourtyardModel.NestSpot(0),
                    "Resident did not reach its own nesting spot.");
            }
        }

        private static void VerifyGrowthWithCommonLoot()
        {
            foreach (bool buildFirst in new[] { true, false })
            {
                var model = new CourtyardModel(() => 0f);
                if (buildFirst) Require(model.BeginPlacement() && model.TryPlaceStash(Left), "Early stash placement failed.");
                Require(model.TryStartScavenge(), "Could not start growth fixture trip.");
                for (int i = 0; i < 400; i++) model.Tick(.1f);
                Require(model.LastTripScrap == 2 && model.Food == 1 && !model.TryRecruit(),
                    "Ordinary first trip has the wrong rewards or recruited without room.");
                Require(model.TryUpgradeNest() && model.TryRecruit(), "Ordinary first trip could not fund growth.");
                if (!buildFirst) Require(model.BeginPlacement() && model.TryPlaceStash(Left), "Growth prevented the first stash.");
                Require(model.CrewCount == 3 && model.Scrap == 0 && model.Food == 0 &&
                    !model.TryUpgradeNest() && !model.TryRecruit(), "Growth balances or capacity are inconsistent.");
            }
        }

        private static void VerifyMovingStashAndSorting()
        {
            var model = new CourtyardModel(() => 0f);
            Require(!model.BeginMoveStash() && !model.BeginBuildSort(), "Unavailable plot operations were accepted.");
            Require(model.BeginPlacement() && model.TryTargetPlot(Left), "Could not place first furnishing.");
            Require(model.BeginMoveStash() && !model.TryTargetPlot(Left) && !model.TryTargetPlot(CourtyardGrid.AnchorCount),
                "Move accepted current or invalid plot.");
            model.CancelPlacement();
            Require(model.StashPlot == Left && model.Scrap == 0, "Cancel changed a placed furnishing or charged scrap.");
            for (int i = 0; i < 300 && model.ResidentAt(0).Activity != CourtyardModel.Activity.PickUpCookie; i++) model.Tick(.1f);
            Require(model.ResidentAt(0).Activity == CourtyardModel.Activity.PickUpCookie, "Could not arrange mid-pickup test.");
            Require(model.BeginMoveStash() && model.TryTargetPlot(Middle) && model.Scrap == 0 &&
                model.ResidentAt(0).Activity == CourtyardModel.Activity.WalkToStash && !model.ResidentAt(0).HasCookie,
                "Moving while picking up must retarget without teleporting or paying.");
            for (int i = 0; i < 300 && model.ResidentAt(0).Activity != CourtyardModel.Activity.CarryCookie; i++) model.Tick(.1f);
            Require(model.ResidentAt(0).Activity == CourtyardModel.Activity.CarryCookie &&
                model.ResidentAt(0).Position == CourtyardModel.CookiePickup(Middle), "Resident did not use the moved furnishing.");
            Require(model.BeginMoveStash() && model.TryTargetPlot(Right) && model.ResidentAt(0).HasCookie,
                "Moving after the handoff should not delete the carried cookie.");
            for (int i = 0; i < 150 && model.ResidentAt(0).Activity != CourtyardModel.Activity.EatCookie; i++) model.Tick(.1f);
            Require(model.ResidentAt(0).Activity == CourtyardModel.Activity.EatCookie &&
                model.ResidentAt(0).Position == CourtyardModel.CookieSeat(Middle), "Moving sent the held cookie to the wrong seat.");
            for (int i = 0; i < 300 && model.SnackVisitor?.Activity != CourtyardModel.Activity.PickUpCookie; i++) model.Tick(.1f);
            Require(model.SnackVisitor?.Activity == CourtyardModel.Activity.PickUpCookie &&
                model.SnackVisitor.Position == CourtyardModel.CookiePickup(Right) && model.SnacksTaken == 1,
                "Next visit did not use the current furnishing or a snack was counted twice.");
            Require(model.TryStartScavenge(), "First fixture trip failed to start.");
            for (int i = 0; i < 400; i++) model.Tick(.1f);
            Require(model.TryUpgradeNest() && model.TryRecruit() && !model.BeginBuildSort(),
                "Growth could not be bought or Sort was offered without enough scrap.");
            Require(model.TryStartScavenge(), "Second fixture trip failed to start.");
            for (int i = 0; i < 400; i++) model.Tick(.1f);
            Require(model.BeginBuildSort() && !model.TryTargetPlot(Right) && model.Scrap == 2,
                "Sort could replace the stash or spent scrap on rejected placement.");
            model.CancelPlacement();
            Require(model.SortPlot == -1 && model.Scrap == 2, "Cancel bought a Sort Station.");
            Require(model.BeginBuildSort() && model.TryTargetPlot(Middle) && model.SortPlot == Middle &&
                model.Scrap == 0 && !model.BeginBuildSort(), "Sort must buy exactly one open plot.");
            Require(model.BeginMoveStash() && !model.TryTargetPlot(Middle) && model.TryTargetPlot(Left),
                "Relocation must reject the built Sort Station without trapping placement.");
            bool sawSort = false;
            for (int i = 0; i < 250 && model.ItemsSorted == 0; i++)
            {
                model.Tick(.1f);
                if (model.SortVisitor?.Activity != CourtyardModel.Activity.Sort) continue;
                Require(model.SortVisitor.Position == CourtyardModel.SortApproach(Middle), "Pip did not approach a usable edge.");
                sawSort = true;
            }
            Require(sawSort && model.ItemsSorted == 1 && model.Scrap == 0, "Sorting is a visible action, not passive income.");
        }

        private static void VerifyGridAndLegacyMigration()
        {
            var stash = CourtyardGrid.Building.Stash;
            var sort = CourtyardGrid.Building.Sort;
            int pointCount = 0;
            for (int anchor = 0; anchor < CourtyardGrid.AnchorCount; anchor++)
            {
                if (!CourtyardGrid.Fits(anchor, stash)) continue;
                pointCount++;
                bool roomForSort = false;
                for (int other = 0; other < CourtyardGrid.AnchorCount; other++)
                    roomForSort |= CourtyardGrid.Fits(other, sort) &&
                        CourtyardGrid.HasClearance(anchor, stash, other, sort);
                Require(roomForSort, "A valid first placement blocks all later Sort Station choices.");
            }
            Require(pointCount == 21 && !CourtyardGrid.Fits(CourtyardGrid.Anchor(7, 0), stash) &&
                !CourtyardGrid.Fits(CourtyardGrid.Anchor(0, 3), stash), "Grid count or border fits were wrong.");
            Require(!CourtyardGrid.HasClearance(Left, stash, Left + 1, sort) &&
                !CourtyardGrid.HasClearance(Left, stash, Left + 2, sort) &&
                CourtyardGrid.HasClearance(Left, stash, Middle, sort), "One-cell buffer was not enforced.");

            const long now = 639000000000000000;
            var legacy = new CourtyardSnapshot
            {
                version = 1, savedUtcTicks = now, stashPlot = 2, sortPlot = 0,
                recruited = true, nestUpgraded = true, trips = 2, scrap = 12, tripScrap = 4, tripSeconds = 8
            };
            Require(CourtyardSaveStore.IsValid(legacy), "Previously valid v1 save was rejected.");
            var migrated = CourtyardModel.Restore(legacy, now, () => throw new InvalidOperationException("Trip was rerolled."));
            Require(migrated.StashPlot == Right && migrated.SortPlot == Left && migrated.CrewCount == 3 &&
                migrated.Scrap == 12 && migrated.ScavengeRemaining == 8 && migrated.CompletedTrips == 2 &&
                migrated.HasBottleCap, "Migration lost placement, progress, or the pending haul.");
            var saved = migrated.Capture(now);
            Require(saved.version == CourtyardSaveStore.CurrentVersion && CourtyardSaveStore.IsValid(saved),
                "Migration did not write a valid v2 snapshot.");
            saved.sortPlot = Right - 2;
            Require(!CourtyardSaveStore.IsValid(saved), "Persisted placements accepted a missing clearance cell.");
            Require(migrated.BeginMoveStash() &&
                migrated.CheckPlacement(Right) == CourtyardModel.PlacementCheck.Unchanged &&
                migrated.CheckPlacement(Left + 2) == CourtyardModel.PlacementCheck.TooClose &&
                !migrated.TryTargetPlot(Left + 2) && migrated.Scrap == 12 &&
                migrated.TryTargetPlot(Middle) && migrated.Scrap == 12,
                "Blocked relocation changed state, or a valid free relocation failed.");
        }

        private static void VerifyGateChoreography()
        {
            var atlas = Resources.Load<Texture2D>("RaccoonRowFlatGate");
            Require(atlas != null && atlas.width == 512 && atlas.height == 512, "Flat gate atlas missing or resized.");
            float closed = CourtyardView.GateClearWidth(0);
            float partlyOpen = CourtyardView.GateClearWidth(.5f);
            float open = CourtyardView.GateClearWidth(1);
            Require(closed == 0 && partlyOpen > 0 && partlyOpen < open && open >= 60,
                "Gate leaf geometry does not give the 50-pixel resident enough clearance.");
            var yard = new CourtyardModel(() => 0);
            Require(yard.GateOpening == 0 && yard.ShowTraveler && yard.TryStartScavenge(),
                "The gate did not start shut with the traveler at home.");
            var traveler = yard.Traveler;
            var previousStage = yard.ScavengeStage;
            bool approachSwing = false, departureClose = false, awayClosed = false;
            bool returnSwing = false, returnClose = false, firstReturnFrame = true;
            bool crossedOut = false, crossedIn = false;
            for (int i = 0; i < 1500 && yard.ScavengeStage != CourtyardModel.TripStage.None; i++)
            {
                Vector2 position = traveler.Position;
                yard.Tick(.02f);
                var stage = yard.ScavengeStage;
                float opening = yard.GateOpening;
                Require(opening >= 0 && opening <= 1, "Gate swing escaped its normalized range.");
                bool partial = opening > 0 && opening < 1;
                if (stage == CourtyardModel.TripStage.Departing) approachSwing |= partial;
                if (stage == CourtyardModel.TripStage.Scavenging)
                {
                    Require(!yard.ShowTraveler, "traveler was visible during the alley timer.");
                    departureClose |= partial;
                    awayClosed |= opening == 0;
                }
                if (stage == CourtyardModel.TripStage.Returning)
                {
                    if (firstReturnFrame)
                    {
                        Require(opening == 0 && !yard.ShowTraveler, "traveler appeared before the return swing.");
                        firstReturnFrame = false;
                    }
                    if (partial && !yard.ShowTraveler)
                    {
                        returnSwing = true;
                        Require(position == traveler.Position, "traveler moved into a closed doorway.");
                    }
                    returnClose |= partial && yard.ShowTraveler;
                }
                if (position.x == 180 && position.y != traveler.Position.y &&
                    position.y <= 265 && yard.ShowTraveler)
                {
                    Require(yard.GateOpen, "traveler crossed a partly shut gate.");
                    crossedOut |= previousStage == CourtyardModel.TripStage.Departing;
                    crossedIn |= stage == CourtyardModel.TripStage.Returning;
                }
                previousStage = stage;
            }
            Require(approachSwing && departureClose && awayClosed && returnSwing && returnClose &&
                crossedOut && crossedIn, "A gate animation phase was skipped.");
            Require(yard.GateOpening == 0 && yard.ShowTraveler && yard.CompletedTrips == 1 &&
                yard.Scrap == 4 && yard.Food == 1, "Gate sequence changed first-haul settlement.");
        }

        private static void VerifySharedResidentActions()
        {
            var yard = new CourtyardModel(() => 0);
            Require(yard.TryStartScavenge(), "No resident could leave on the first trip.");
            int first = yard.TravelSlot;
            for (int i = 0; i < 350; i++) yard.Tick(.1f);
            Require(yard.TryUpgradeNest() && yard.TryRecruit() && yard.TryStartScavenge(),
                "Could not set up three-resident dispatch.");
            int second = yard.TravelSlot;
            Require(second != first, "Scavenging was pinned to one resident.");
            for (int i = 0; i < 350; i++) yard.Tick(.1f);
            Require(yard.TryStartScavenge() && yard.TravelSlot != second, "Rotation did not advance.");
            // The cursor was initialized with two residents; let it reach the new third slot.
            if (yard.TravelSlot == first)
            {
                for (int i = 0; i < 350; i++) yard.Tick(.1f);
                Require(yard.TryStartScavenge(), "Could not start the next rotation slot.");
            }
            Require(yard.TravelSlot == 2, "The third resident could not participate in the rotation.");
            long now = DateTime.UtcNow.Ticks;
            var state = yard.Capture(now);
            var restored = CourtyardModel.Restore(state, now,
                () => throw new InvalidOperationException("Pending shared trip was rerolled."));
            Require(restored.TravelSlot == yard.TravelSlot && state.tripResident == yard.TravelSlot + 1,
                "Restoring a trip switched resident cosmetics.");
            state.tripResident = 0;
            Require(CourtyardModel.Restore(state, now).TravelSlot == 1, "Legacy pending traveler was lost.");
            state.tripResident = 4;
            Require(!CourtyardSaveStore.IsValid(state), "Save accepted a nonexistent traveler.");
            for (int i = 0; i < 350; i++) yard.Tick(.1f);
            Require(yard.BeginPlacement() && yard.TryTargetPlot(Left) &&
                yard.BeginBuildSort() && yard.TryTargetPlot(Right), "Could not place the shared facilities.");
            var ate = new bool[3];
            var sorted = new bool[3];
            for (int i = 0; i < 5000; i++)
            {
                yard.Tick(.1f);
                for (int slot = 0; slot < yard.CrewCount; slot++)
                {
                    var activity = yard.ResidentAt(slot).Activity;
                    ate[slot] |= activity == CourtyardModel.Activity.EatCookie;
                    sorted[slot] |= activity == CourtyardModel.Activity.Sort;
                }
            }
            for (int slot = 0; slot < yard.CrewCount; slot++)
                Require(ate[slot] && sorted[slot], $"Resident {slot} could not use both shared facilities.");
        }

        private static void VerifySavesAndAwayReturn()
        {
            string directory = Path.Combine(Path.GetTempPath(), "rr-save-test-" + Guid.NewGuid().ToString("N"));
            var store = new CourtyardSaveStore(directory);
            long now = new DateTime(2026, 11, 27, 4, 0, 0, DateTimeKind.Utc).Ticks;
            try
            {
                Require(store.Load(out _) == null, "A new profile already had data.");
                var yard = new CourtyardModel(() => 0f);
                Require(yard.BeginPlacement() && yard.TryTargetPlot(Left) && yard.TryStartScavenge(), "Cannot start saved-game fixture.");
                for (int i = 0; i < 90; i++) yard.Tick(.1f);
                var pending = yard.Capture(now);
                store.Write(pending);
                File.WriteAllText(Path.Combine(directory, "courtyard.json.tmp"), "{an interrupted write");
                var state = store.Load(out bool backupUsed);
                Require(!backupUsed && state.tripScrap == 2 && state.stashPlot == Left && state.tripSeconds > 4,
                    "An incomplete write displaced the valid primary.");
                var shortVisit = CourtyardModel.Restore(state, now + TimeSpan.FromSeconds(2).Ticks,
                    () => throw new InvalidOperationException("Trip was rerolled."));
                Require(shortVisit.ScavengeStage == CourtyardModel.TripStage.Scavenging &&
                    Mathf.Approximately(shortVisit.ScavengeRemaining, state.tripSeconds - 2) &&
                    !shortVisit.HasBottleCap && shortVisit.AwayIncome == 0, "Short absences did not preserve an active trip.");
                var reversed = CourtyardModel.Restore(state, now - TimeSpan.FromHours(2).Ticks);
                Require(reversed.AwayIncome == 0 && Mathf.Approximately(reversed.ScavengeRemaining, state.tripSeconds),
                    "Moving the clock backward progressed the game.");
                long tomorrow = now + TimeSpan.FromDays(1).Ticks;
                var returned = CourtyardModel.Restore(state, tomorrow,
                    () => throw new InvalidOperationException("Trip was rerolled offline."));
                Require(returned.Scrap == 14 && returned.Food == 1 && returned.HasBottleCap &&
                    returned.TripCompletedWhileAway && returned.CompletedTrips == 1 &&
                    returned.ScavengeStage == CourtyardModel.TripStage.None, "Offline reward or cap was wrong.");
                Require(returned.TryUpgradeNest() && returned.TryRecruit() && returned.BeginBuildSort() &&
                    returned.TryTargetPlot(Middle) && returned.BeginMoveStash() && returned.TryTargetPlot(Right),
                    "Could not spend away gains on visible progress.");
                store.Write(returned.Capture(tomorrow));
                var reopened = CourtyardModel.Restore(store.Load(out backupUsed), tomorrow);
                Require(!backupUsed && reopened.Scrap == 10 && reopened.CrewCount == 3 && reopened.NestCapacity == 3 &&
                    reopened.StashPlot == Right && reopened.SortPlot == Middle && reopened.HasBottleCap &&
                    !reopened.TripCompletedWhileAway && reopened.AwayIncome == 0 && reopened.CompletedTrips == 1,
                    "Reload lost progress or double-credited a saved offline return.");
                long later = tomorrow + TimeSpan.FromMinutes(6).Ticks;
                var briefReturn = CourtyardModel.Restore(reopened.Capture(tomorrow), later);
                Require(briefReturn.AwayIncome == 1 && briefReturn.Scrap == 11,
                    "A short absence did not provide a useful saved gain.");
                store.Write(briefReturn.Capture(later));
                var primaryPath = Path.Combine(directory, "courtyard.json");
                File.WriteAllText(primaryPath, "{broken");
                var fallback = store.Load(out backupUsed);
                Require(backupUsed && fallback != null && fallback.stashPlot == Right && fallback.scrap == 10,
                    "Corrupt primary did not recover the prior known-good save.");
                store.Write(fallback);
                Require(store.Load(out backupUsed)?.stashPlot == Right && !backupUsed,
                    "Recovery failed to repair the primary.");
                File.WriteAllText(primaryPath, "{broken again");
                Require(store.Load(out backupUsed)?.scrap == 10 && backupUsed,
                    "Repair overwrote the valid backup with a corrupt primary.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(int result)
        {
            SessionState.EraseBool(Running);
            EditorApplication.update -= Verify;
            EditorApplication.Exit(result);
        }
    }
}
