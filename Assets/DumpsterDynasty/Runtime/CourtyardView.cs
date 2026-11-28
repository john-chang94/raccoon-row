using UnityEngine;

namespace DumpsterDynasty
{
    /// <summary>Portrait blockout. Logical coordinates keep the touch targets and map aligned.</summary>
    public sealed class CourtyardView : MonoBehaviour
    {
        [SerializeField] private Texture2D greenPortrait;
        [SerializeField] private Texture2D orangePortrait;
        [SerializeField] private Texture2D pipPortrait;
        private const string GateAtlasResource = "RaccoonRowFlatGate";
        private const float LeftHinge = 138f, RightHinge = 222f, GateGround = 256f;
        private const float GateClosedLeaf = 42f, GateOpenLeaf = 8f;
        private static readonly Rect LeftLeafUv = GateAtlasUv(2, 24, 42, 90);
        private static readonly Rect RightLeafUv = GateAtlasUv(46, 24, 42, 90);
        private static readonly Rect PostUv = GateAtlasUv(90, 14, 24, 100);
        private Texture2D gateAtlas;
        private Texture2D furnishings;
        private const float NestLeft = 32f, NestTop = 419f;
        private const float SortTransferSeconds = 1.65f;
        private static readonly Rect NestDrawer = new Rect(2, 2, 164, 55);
        private static readonly Rect NestCanopy = new Rect(2, 59, 164, 20);
        private static readonly Rect[] NestMats =
        {
            new Rect(4, 85, 52, 27), new Rect(58, 85, 52, 27), new Rect(112, 85, 52, 27)
        };
        private static readonly Rect StashCrate = new Rect(170, 2, 76, 62);
        private static readonly Rect SortBench = new Rect(170, 66, 76, 60);
        private static readonly Rect WholeCookie = new Rect(8, 113, 12, 12);
        private static readonly Rect BittenCookie = new Rect(24, 113, 12, 12);
        private static readonly Rect Washer = new Rect(42, 113, 12, 12);
        private readonly ActorDraw[] drawOrder = new ActorDraw[3];
        public const int MobileTargetFrameRate = 60;
        private static readonly string UpgradeAction = $"Upgrade Nest Pile  •  {CourtyardModel.NestUpgradeCost} scrap";
        private static readonly string RecruitAction = $"Welcome {CourtyardModel.RecruitName}  •  {CourtyardModel.RecruitCost} food";
        private static readonly string StashCostText = $"{CourtyardModel.SnackCost} scrap";
        private static readonly string SortCostText = $"{CourtyardModel.SortCost} scrap";
        private static readonly string RecruitedStatus = $"{CourtyardModel.RecruitName} joined the yard";
        private int hudScrap = -1, hudFood = -1, hudCrew = -1, hudCapacity = -1;
        private int hudSorted = -1, hudTripScrap = -1, hudSeconds = -1, hudAwayIncome = -1;
        private bool hudFoundCap, repainting;
        private string resourcesText, crewText, sortedText, tripText, countdownText, awayIncomeText;

        private struct ActorDraw
        {
            public CourtyardModel.Raccoon Raccoon;
            public Texture2D Portrait;
            public bool Traveling;
        }
        public CourtyardModel Model { get; private set; }
        private GUIStyle heading, small, centered, lightCentered, tileTitle;
        private bool collectionOpen, buildOpen;
        private int selectedAnchor = -1;
        private CourtyardSaveStore saveStore;
        private int savedRevision;
        private long lastSavedUtc, pausedUtc;
        private float nextHeartbeat;
        private bool paused, saveIssue, recoveredBackup, showAwaySummary;
        private static readonly int[] SkylineHeights = { 12, 25, 43, 29, 58, 43, 21, 32, 54, 30 };
        private static readonly Color Cream = new Color32(255, 246, 215, 255);
        private static readonly Color Ink = new Color32(55, 57, 46, 255);
        private static readonly Color Leaf = new Color32(91, 114, 74, 255);
        private static readonly Color PaleLeaf = new Color32(200, 214, 153, 255);
        private static readonly Color Wood = new Color32(113, 81, 63, 255);
        private static readonly Color Soil = new Color32(211, 186, 139, 255);
        private static readonly Color SoilPatch = new Color32(199, 176, 129, 255);
        private static readonly Color Fence = new Color32(163, 127, 93, 255);
        private static readonly Color Sky = new Color32(210, 224, 188, 255);
        private static readonly Color Skyline = new Color32(162, 185, 157, 255);
        private static readonly Color Shadow = new Color32(166, 147, 105, 255);
        private static readonly Color LightWood = new Color32(182, 143, 96, 255);
        private static readonly Color CapBlue = new Color32(88, 143, 174, 255);
        private static readonly Color DeepLeaf = new Color32(58, 80, 54, 255);
        private static readonly Color Muted = new Color32(221, 209, 177, 255);
        private static readonly Color GridLine = new Color32(113, 81, 63, 115);
        private static readonly Color GridOkay = new Color32(125, 159, 97, 142);
        private static readonly Color GridBlocked = new Color32(185, 99, 73, 155);
        private static readonly Color GridInvalidInk = new Color32(123, 63, 48, 255);
        private static readonly Color Cookie = new Color32(239, 181, 96, 255);

        private void Awake()
        {
#if UNITY_IOS || UNITY_ANDROID
            // Unity defaults to 30 fps on mobile; vSyncCount does not select the mobile rate.
            Application.targetFrameRate = MobileTargetFrameRate;
#endif
#if UNITY_EDITOR
            // Regular Play Mode stays a fresh preview. Only opt-in sessions write to a separate test folder.
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-rr-save-preview") >= 0)
                saveStore = new CourtyardSaveStore(System.IO.Path.Combine(Application.persistentDataPath, "RaccoonRowEditorPreview"));
#else
            saveStore = new CourtyardSaveStore(System.IO.Path.Combine(Application.persistentDataPath, "RaccoonRow"));
#endif
            gateAtlas = Resources.Load<Texture2D>(GateAtlasResource);
            furnishings = Resources.Load<Texture2D>("RaccoonRowFurnishings");
            long now = System.DateTime.UtcNow.Ticks;
            var snapshot = saveStore?.Load(out recoveredBackup);
            if (snapshot != null) lastSavedUtc = snapshot.savedUtcTicks;
            Model = CourtyardModel.Restore(snapshot, now);
            showAwaySummary = Model.AwayIncome > 0 || Model.TripCompletedWhileAway;
            Checkpoint(now);
        }

        private void Update()
        {
            if (paused) return;
            Model.Tick(Time.deltaTime);
            if (saveStore != null && Time.unscaledTime >= nextHeartbeat)
                Checkpoint(System.DateTime.UtcNow.Ticks);
            else SaveIfChanged();
        }

        private void SaveIfChanged()
        {
            if (saveStore != null && savedRevision != Model.ProgressRevision)
                Checkpoint(System.DateTime.UtcNow.Ticks);
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (saveStore == null || Model == null || paused == isPaused) return;
            long now = System.DateTime.UtcNow.Ticks;
            paused = isPaused;
            if (paused)
            {
                pausedUtc = System.Math.Max(lastSavedUtc, now);
                Checkpoint(pausedUtc);
                return;
            }
            Model.ApplyAwayProgress(System.TimeSpan.FromTicks(System.Math.Max(0L, now - pausedUtc)).TotalSeconds);
            showAwaySummary = Model.AwayIncome > 0 || Model.TripCompletedWhileAway;
            Checkpoint(now);
        }

        private void OnApplicationQuit()
        {
            if (saveStore != null && Model != null && !paused) Checkpoint(System.DateTime.UtcNow.Ticks);
        }

        private void Checkpoint(long now)
        {
            if (saveStore == null) return;
            long savedAt = System.Math.Max(lastSavedUtc, now);
            // Avoid hammering storage on a failed write; retry at the next change or lifecycle checkpoint.
            savedRevision = Model.ProgressRevision;
            nextHeartbeat = Time.unscaledTime + 60f;
            try
            {
                saveStore.Write(Model.Capture(savedAt));
                lastSavedUtc = savedAt;
                saveIssue = false;
            }
            catch (System.IO.IOException) { saveIssue = true; }
            catch (System.UnauthorizedAccessException) { saveIssue = true; }
        }

        private void ConfigureStyles()
        {
            heading = TextStyle(22, TextAnchor.MiddleLeft, FontStyle.Bold);
            small = TextStyle(13, TextAnchor.MiddleLeft);
            centered = TextStyle(13, TextAnchor.MiddleCenter, FontStyle.Bold);
            lightCentered = TextStyle(13, TextAnchor.MiddleCenter, FontStyle.Bold);
            lightCentered.normal.textColor = Cream;
            tileTitle = TextStyle(13, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private static GUIStyle TextStyle(int size, TextAnchor alignment, FontStyle fontStyle = FontStyle.Normal)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = alignment, fontStyle = fontStyle };
            style.normal.textColor = Ink;
            return style;
        }

        private void OnGUI()
        {
            if (Model == null) return;
            if (heading == null) ConfigureStyles();
            Rect safe = Screen.safeArea;
            float scale = Mathf.Min(safe.width / 360f, safe.height / 640f);
            if (scale <= 0f) return;
            float left = safe.xMin + (safe.width - 360f * scale) * 0.5f;
            float top = Screen.height - safe.yMax + (safe.height - 640f * scale) * 0.5f;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(left, top, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            GUI.BeginGroup(new Rect(0, 0, 360, 640));
            repainting = Event.current.type == EventType.Repaint;
            // Keep interactive IMGUI controls on every event pass; decorative drawing needs only Repaint.
            if (repainting)
            {
                DrawYard();
                DrawTop();
            }
            if (Model.IsPlacing) DrawPlacementGrid();
            DrawPlots();
            if (repainting)
            {
                DrawNest();
                DrawCrew();
                DrawScavenge();
            }
            DrawAwaySummary();
            DrawActions();
            if (repainting && collectionOpen) DrawCollection();
            GUI.EndGroup();
            GUI.matrix = previous;
            SaveIfChanged();
        }

        private void DrawAwaySummary()
        {
            if (!showAwaySummary && !recoveredBackup && !saveIssue) return;
            Box(14, 131, 332, 33, Cream);
            if (repainting)
            {
                if (hudAwayIncome != Model.AwayIncome)
                {
                    hudAwayIncome = Model.AwayIncome;
                    awayIncomeText = $"Welcome back  •  +{hudAwayIncome} scrap while away";
                }
                string message = saveIssue ? "Couldn't save your progress" : recoveredBackup ? "Recovered your last backup" :
                    hudAwayIncome > 0 ? awayIncomeText : "Your scavenge finished while you were away";
                GUI.Label(new Rect(23, 133, 271, 29), message, small);
            }
            if (!saveIssue && YardButton(new Rect(301, 132, 39, 31), "OK"))
            {
                recoveredBackup = false;
                showAwaySummary = false;
            }
        }

        private void DrawCrew()
        {
            int count = 0;
            for (int slot = 0; slot < Model.CrewCount; slot++)
            {
                bool traveling = slot == Model.TravelSlot;
                if (!traveling || Model.ShowTraveler)
                    AddActor(ref count, Model.ResidentAt(slot), PortraitFor(slot), traveling);
            }
            for (int i = 0; i < count; i++)
                DrawRaccoon(drawOrder[i].Raccoon, drawOrder[i].Portrait, drawOrder[i].Traveling);
        }

        private Texture2D PortraitFor(int slot) =>
            slot == 0 ? greenPortrait : slot == 1 ? orangePortrait : pipPortrait;

        private void AddActor(ref int count, CourtyardModel.Raccoon raccoon, Texture2D portrait, bool traveling)
        {
            int i = count++;
            while (i > 0 && drawOrder[i - 1].Raccoon.Position.y > raccoon.Position.y)
            {
                drawOrder[i] = drawOrder[i - 1];
                i--;
            }
            drawOrder[i] = new ActorDraw { Raccoon = raccoon, Portrait = portrait, Traveling = traveling };
        }

        private static void Box(float x, float y, float w, float h, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color prior = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = prior;
        }

        private void DrawYard()
        {
            Box(0, 0, 360, 640, Soil);
            Box(0, 0, 360, 195, Sky);
            for (int i = 0; i < SkylineHeights.Length; i++)
                Box(i * 38, 195 - SkylineHeights[i], 35, SkylineHeights[i], Skyline);

            Box(LeftHinge, 196, RightHinge - LeftHinge, GateGround - 196,
                new Color32(171, 152, 115, 255));
            Box(0, 191, LeftHinge, 66, Fence);
            Box(RightHinge, 191, 360 - RightHinge, 66, Fence);
            for (int x = 0; x < 360; x += 24)
                if (x < LeftHinge || x >= RightHinge) Box(x, 192, 2, 65, Wood);
            Box(0, 244, LeftHinge, 6, LightWood);
            Box(RightHinge, 244, 360 - RightHinge, 6, LightWood);
            Box(0, GateGround, LeftHinge, 5, Shadow);
            Box(RightHinge, GateGround, 360 - RightHinge, 5, Shadow);
            DrawGate();

            Box(20, 280, 44, 18, SoilPatch);
            Box(292, 275, 43, 17, SoilPatch);
            Box(45, 472, 33, 19, SoilPatch);
            Box(293, 492, 26, 11, SoilPatch);
            Weed(29, 289); Weed(323, 280); Weed(63, 480); Weed(308, 500);
        }

        private static Rect GateAtlasUv(float x, float y, float width, float height) =>
            new Rect(x / 128f, (128f - y - height) / 128f, width / 128f, height / 128f);

        public static float GateClearWidth(float opening) =>
            RightHinge - LeftHinge - 2f * Mathf.Lerp(GateClosedLeaf, GateOpenLeaf, opening);

        private void DrawGate()
        {
            // Horizontal projection shortens each leaf around its outside hinge.
            // The paw badge is part of the left leaf, not painted over the doorway.
            float width = (RightHinge - LeftHinge - GateClearWidth(Model.GateOpening)) * 0.5f;
            var left = new Rect(LeftHinge, GateGround - 90, width, 90);
            var right = new Rect(RightHinge - width, GateGround - 90, width, 90);
            if (gateAtlas == null)
            {
                Box(LeftHinge - 18, GateGround - 52, 18, 52, Wood);
                Box(RightHinge, GateGround - 52, 18, 52, Wood);
                Box(left.x, GateGround - 56, width, 56, LightWood);
                Box(right.x, GateGround - 56, width, 56, LightWood);
                return;
            }

            GUI.DrawTextureWithTexCoords(left, gateAtlas, LeftLeafUv);
            GUI.DrawTextureWithTexCoords(right, gateAtlas, RightLeafUv);
            GUI.DrawTextureWithTexCoords(new Rect(LeftHinge - 21, GateGround - 100, 24, 100), gateAtlas, PostUv);
            GUI.DrawTextureWithTexCoords(new Rect(RightHinge - 3, GateGround - 100, 24, 100), gateAtlas, PostUv);
        }

        private static void Weed(int x, int y)
        {
            Box(x, y - 7, 3, 9, Leaf);
            Box(x - 4, y - 4, 3, 6, Leaf);
            Box(x + 4, y - 3, 3, 5, Leaf);
        }

        private void DrawTop()
        {
            Box(14, 17, 332, 54, Cream);
            GUI.Label(new Rect(26, 21, 208, 29), "Raccoon Row", heading);
            GUI.Label(new Rect(26, 48, 200, 16), "Your courtyard", small);
            if (hudScrap != Model.Scrap || hudFood != Model.Food)
            {
                hudScrap = Model.Scrap;
                hudFood = Model.Food;
                resourcesText = $"{hudScrap} scrap  •  {hudFood} food";
            }
            if (hudCrew != Model.CrewCount || hudCapacity != Model.NestCapacity)
            {
                hudCrew = Model.CrewCount;
                hudCapacity = Model.NestCapacity;
                crewText = $"{hudCrew}/{hudCapacity} crew";
            }
            GUI.Label(new Rect(190, 23, 144, 18), resourcesText, centered);
            GUI.Label(new Rect(190, 44, 144, 18), crewText, centered);
        }

        private void DrawPlacementGrid()
        {
            Vector2 origin = CourtyardGrid.Origin;
            int cell = CourtyardGrid.CellSize;
            int width = CourtyardGrid.Columns * cell, height = CourtyardGrid.Rows * cell;
            var kind = Model.Placement == CourtyardModel.PlacementMode.BuildSort
                ? CourtyardGrid.Building.Sort : CourtyardGrid.Building.Stash;
            if (repainting)
            {
                for (int x = 0; x <= CourtyardGrid.Columns; x++)
                    Box(origin.x + x * cell - 0.5f, origin.y, 1, height, GridLine);
                for (int y = 0; y <= CourtyardGrid.Rows; y++)
                    Box(origin.x, origin.y + y * cell - 0.5f, width, 1, GridLine);

                if (selectedAnchor >= 0)
                {
                    Rect preview = CourtyardGrid.WorldFootprint(selectedAnchor, kind);
                    bool valid = Model.CanTargetPlot(selectedAnchor);
                    var fill = valid ? GridOkay : GridBlocked;
                    Box(preview.x, preview.y, preview.width, preview.height, fill);
                    Box(preview.x, preview.y, preview.width, 2, valid ? DeepLeaf : GridInvalidInk);
                    Box(preview.x, preview.yMax - 2, preview.width, 2, valid ? DeepLeaf : GridInvalidInk);
                    Box(preview.x, preview.y, 2, preview.height, valid ? DeepLeaf : GridInvalidInk);
                    Box(preview.xMax - 2, preview.y, 2, preview.height, valid ? DeepLeaf : GridInvalidInk);
                }
            }

            for (int anchor = 0; anchor < CourtyardGrid.AnchorCount; anchor++)
            {
                if (!CourtyardGrid.Fits(anchor, kind)) continue;
                // The hit target is one nonoverlapping cell around each snap point.
                Vector2 point = CourtyardGrid.WorldFootprint(anchor, kind).center;
                var hit = new Rect(point.x - cell / 2f, point.y - cell / 2f, cell, cell);
                if (GUI.Button(hit, GUIContent.none, GUIStyle.none))
                    selectedAnchor = anchor;
            }
        }

        private void DrawFurnishing(Rect cell, Rect destination)
        {
            if (furnishings == null) return;
            var uv = new Rect(cell.x / 256f, (128f - cell.yMax) / 128f,
                cell.width / 256f, cell.height / 128f);
            GUI.DrawTextureWithTexCoords(destination, furnishings, uv);
        }

        private static Rect CenteredArt(Vector2 center, float width, float height) =>
            new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);

        private static Vector2 CookieTrayPosition(Vector2 plot) => plot + new Vector2(8, 0);

        private void DrawCookie(Vector2 center, float size, bool bitten = false)
        {
            if (furnishings != null)
                DrawFurnishing(bitten ? BittenCookie : WholeCookie, CenteredArt(center, size, size));
            else
                Box(center.x - size / 2, center.y - size / 2, size, size, Cookie);
        }

        private void DrawPlots()
        {
            if (!repainting) return;
            if (Model.StashPlot >= 0)
            {
                Vector2 pos = CourtyardModel.PlotCenter(Model.StashPlot);
                if (furnishings == null)
                    Box(pos.x - 29, pos.y - 18, 58, 42, Wood);
                else
                    DrawFurnishing(StashCrate, new Rect(pos.x - 32, pos.y - 26, 64, 52));

                // Leave one cookie in the stash as a material cue. The front cookie is drawn
                // only here OR in the resident's hand, so it visibly leaves the tray once.
                Vector2 tray = CookieTrayPosition(pos);
                DrawCookie(tray + new Vector2(9, -3), 9);
                if (Model.SnackVisitor?.HasCookie != true)
                    DrawCookie(tray, 12);
            }
            if (Model.SortPlot >= 0)
            {
                Vector2 pos = CourtyardModel.PlotCenter(Model.SortPlot, CourtyardGrid.Building.Sort);
                if (furnishings == null)
                {
                    Box(pos.x - 30, pos.y - 15, 60, 30, Wood);
                }
                else
                {
                    DrawFurnishing(SortBench, new Rect(pos.x - 32, pos.y - 27, 64, 51));
                    var visitor = Model.SortVisitor;
                    float moved = visitor?.Activity == CourtyardModel.Activity.Sort
                        ? Mathf.SmoothStep(0, 1, visitor.ActivityAge / SortTransferSeconds) : 0f;
                    var prop = new Vector2(pos.x - 13 + 27 * moved, pos.y + 2 - 8 * Mathf.Sin(moved * Mathf.PI));
                    DrawFurnishing(Washer, CenteredArt(prop, 10, 10));
                }
            }
            if (Model.IsPlacing) DrawSnapPoints();
        }

        private void DrawSnapPoints()
        {
            var kind = Model.Placement == CourtyardModel.PlacementMode.BuildSort
                ? CourtyardGrid.Building.Sort : CourtyardGrid.Building.Stash;
            for (int anchor = 0; anchor < CourtyardGrid.AnchorCount; anchor++)
            {
                if (!CourtyardGrid.Fits(anchor, kind)) continue;
                // Keep invalid choices tappable for an explanation, but do not clutter the buildings.
                bool valid = Model.CanTargetPlot(anchor);
                if (!valid && anchor != selectedAnchor) continue;
                Vector2 point = CourtyardGrid.WorldFootprint(anchor, kind).center;
                int size = anchor == selectedAnchor ? 9 : 5;
                Box(point.x - size / 2f, point.y - size / 2f, size, size, valid ? DeepLeaf : GridInvalidInk);
            }
        }

        private void DrawNest()
        {
            DrawFurnishing(NestDrawer, new Rect(NestLeft, NestTop + 5, 162, 54));
            if (Model.IsNestUpgraded)
            {
                // Shallow salvaged awning reads as an upgrade without covering the beds or crew.
                Box(NestLeft + 17, NestTop + 11, 3, 17, Wood);
                Box(NestLeft + 143, NestTop + 11, 3, 17, Wood);
                DrawFurnishing(NestCanopy, new Rect(NestLeft, NestTop - 3, 162, 20));
            }
            // These are bed centers, not station assignments. Keep the mats offset in depth
            // and larger than the current pose so a future curled ground-sleep pose fits.
            for (int i = 0; i < Model.NestCapacity; i++)
            {
                Vector2 spot = CourtyardModel.NestSpot(i);
                if (furnishings != null)
                    DrawFurnishing(NestMats[i], new Rect(spot.x - 28, spot.y - 14, 56, 29));
                else
                    Box(spot.x - 24, spot.y - 12, 48, 19, Muted);
            }
        }

        private void DrawRaccoon(CourtyardModel.Raccoon raccoon, Texture2D portrait, bool traveling = false)
        {
            Vector2 pos = raccoon.Position;
            bool sleeping = !traveling && raccoon.Activity == CourtyardModel.Activity.Rest;
            bool seated = !traveling && raccoon.Activity == CourtyardModel.Activity.EatCookie;
            bool pickingUp = !traveling && raccoon.Activity == CourtyardModel.Activity.PickUpCookie;
            float bob = sleeping || seated || pickingUp ? 0f : Mathf.Sin(Time.time * 8) * 2f;
            Box(pos.x - 15, pos.y - 4, 30, 6, Shadow);
            if (portrait != null)
            {
                // Use a lower silhouette to mark a pose without replacing the approved portrait art.
                float height = sleeping ? 36 : seated || pickingUp ? 45 : 54;
                GUI.DrawTexture(new Rect(pos.x - 25, pos.y - height + bob, 50, height),
                    portrait, ScaleMode.ScaleToFit, true);
            }
            if (sleeping)
            {
                Box(pos.x + 15, pos.y - 38, 23, 17, Cream);
                GUI.Label(new Rect(pos.x + 15, pos.y - 39, 23, 18), "z z", centered);
            }
            if (!traveling && raccoon.HasCookie)
            {
                var hand = pos + new Vector2(14, -16 + bob);
                if (pickingUp)
                {
                    // Use the same art-space anchor as the stocked crate for a continuous handoff.
                    Vector2 tray = CookieTrayPosition(CourtyardModel.PlotCenter(Model.StashPlot));
                    hand = Vector2.Lerp(tray, hand, Mathf.SmoothStep(0, 1, raccoon.ActivityAge / 0.45f));
                }
                bool bitten = seated && raccoon.ActivityAge > 1.05f;
                DrawCookie(hand, 12, bitten);
            }
        }

        private void DrawScavenge()
        {
            var stage = Model.ScavengeStage;
            if (stage == CourtyardModel.TripStage.None)
            {
                if (Model.LastTripScrap == 0) return;
                bool withFind = Model.LastTripFoundBottleCap;
                Box(159, 78, 187, withFind ? 49 : 32, Cream);
                if (hudTripScrap != Model.LastTripScrap || hudFoundCap != withFind)
                {
                    hudTripScrap = Model.LastTripScrap;
                    hudFoundCap = withFind;
                    tripText = $"+{hudTripScrap} scrap" + (withFind ? "  •  +1 food" : " from Back Alley");
                }
                GUI.Label(new Rect(169, 79, 169, 25), tripText, small);
                if (withFind)
                {
                    DrawBottleCap(180, 105, 13);
                    GUI.Label(new Rect(199, 101, 138, 22), CourtyardModel.FirstFindName, small);
                }
                return;
            }

            int seconds = Mathf.CeilToInt(Model.ScavengeRemaining);
            if (stage == CourtyardModel.TripStage.Scavenging && hudSeconds != seconds)
            {
                hudSeconds = seconds;
                countdownText = $"0:{hudSeconds:00}";
            }
            string status = stage == CourtyardModel.TripStage.Departing ? "Heading out" :
                stage == CourtyardModel.TripStage.Returning ? "Coming home" : countdownText;
            Box(208, 78, 138, 51, Cream);
            var portrait = PortraitFor(Model.TravelSlot);
            if (portrait != null)
                GUI.DrawTexture(new Rect(213, 84, 29, 30), portrait, ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(215, 111, 26, 15), "×1", centered);
            GUI.Label(new Rect(247, 80, 94, 23), "Scavenging", small);
            GUI.Label(new Rect(247, 103, 94, 20), status, small);
        }

        private static void DrawBottleCap(float x, float y, float size)
        {
            Box(x + 2, y, size - 4, size, Wood);
            Box(x, y + 2, size, size - 4, Wood);
            Box(x + 2, y + 2, size - 4, size - 4, CapBlue);
            Box(x + size * 0.4f, y + size * 0.3f, size * 0.22f, size * 0.22f, Sky);
        }

        private void DrawCollection()
        {
            // The build panel sits below this compact card and the gate remains visible.
            Box(174, 415, 172, 79, Cream);
            GUI.Label(new Rect(186, 420, 150, 24), "FOUND IN BACK ALLEY", small);
            DrawBottleCap(188, 452, 23);
            GUI.Label(new Rect(222, 446, 119, 34), CourtyardModel.FirstFindName, small);
        }

        private enum YardIcon { None, Gate, Build, Find, Stash, Sort }

        // These fixed-rect controls do not rely on layout events. Keep the hit boxes and control
        // order on every event pass, and draw the icon and labels only when Unity repaints.
        private bool YardButton(Rect rect, string label, YardIcon icon = YardIcon.None,
            bool primary = false, bool enabled = true, string detail = null)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = previous;
            if (!repainting) return clicked;
            Box(rect.x, rect.y, rect.width, rect.height, primary && enabled ? DeepLeaf : Wood);
            Color fill = !enabled ? Muted : primary ? Leaf : Cream;
            if (enabled && rect.Contains(Event.current.mousePosition))
                fill = Color.Lerp(fill, primary ? DeepLeaf : PaleLeaf, 0.35f);
            Box(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4, fill);

            if (detail != null)
            {
                DrawIcon(icon, rect.x + 9, rect.y + 13, 21, primary && enabled ? Cream : Ink);
                float textX = rect.x + 37;
                GUI.Label(new Rect(textX, rect.y + 5, rect.width - 41, 21), label, tileTitle);
                GUI.Label(new Rect(textX, rect.y + 26, rect.width - 41, 17), detail, small);
            }
            else if (icon != YardIcon.None)
            {
                DrawIcon(icon, rect.center.x - 9, rect.y + 6, 18, primary && enabled ? Cream : Ink);
                GUI.Label(new Rect(rect.x, rect.y + 25, rect.width, rect.height - 27),
                    label, primary && enabled ? lightCentered : centered);
            }
            else
            {
                GUI.Label(rect, label, primary && enabled ? lightCentered : centered);
            }
            return clicked;
        }

        private static void DrawIcon(YardIcon icon, float x, float y, float w, Color ink)
        {
            float u = w / 10f;
            switch (icon)
            {
                case YardIcon.Gate:
                    Box(x + u, y + 2 * u, 8 * u, u, ink);
                    Box(x + u, y + 2 * u, u, 7 * u, ink);
                    Box(x + 8 * u, y + 2 * u, u, 7 * u, ink);
                    Box(x + 4.5f * u, y + 4 * u, u, 5 * u, ink);
                    Box(x + 2 * u, y + 7 * u, 6 * u, u, ink);
                    break;
                case YardIcon.Build:
                    Box(x + 3 * u, y + 2 * u, 6 * u, 2.5f * u, ink);
                    Box(x + 4 * u, y + 4 * u, 2 * u, 5 * u, ink);
                    Box(x + 2 * u, y + 1.5f * u, 1.5f * u, 3.5f * u, ink);
                    break;
                case YardIcon.Find:
                    DrawBottleCap(x + u, y + u, 8 * u);
                    break;
                case YardIcon.Stash:
                    Box(x + u, y + 4 * u, 8 * u, 5 * u, ink);
                    Box(x + 2 * u, y + 5 * u, 6 * u, 2 * u, LightWood);
                    Box(x + 3 * u, y + u, 4 * u, 4 * u, Cookie);
                    break;
                case YardIcon.Sort:
                    Box(x + u, y + 4 * u, 8 * u, 2 * u, ink);
                    Box(x + 2 * u, y + 6 * u, u, 3 * u, ink);
                    Box(x + 7 * u, y + 6 * u, u, 3 * u, ink);
                    Box(x + 2 * u, y + 2 * u, 2 * u, 2 * u, Leaf);
                    Box(x + 6 * u, y + 2 * u, 2 * u, 2 * u, CapBlue);
                    break;
            }
        }

        private void DrawActions()
        {
            Box(14, 558, 332, 66, Cream);
            var scavenge = new Rect(23, 565, 101, 51);
            var build = new Rect(129, 565, 102, 51);
            var collection = new Rect(236, 565, 101, 51);

            bool tripActive = Model.ScavengeStage != CourtyardModel.TripStage.None;
            bool canScavenge = Model.CanStartScavenge;
            bool placing = Model.IsPlacing;
            if (YardButton(scavenge, tripActive ? "Away" : canScavenge ? "Scavenge" : "Busy", YardIcon.Gate, enabled: canScavenge))
                Model.TryStartScavenge();

            if (YardButton(build, placing ? "Cancel" : buildOpen ? "Close" : "Build",
                placing ? YardIcon.None : YardIcon.Build, primary: !placing && !collectionOpen))
            {
                if (placing)
                {
                    Model.CancelPlacement();
                    selectedAnchor = -1;
                }
                else
                {
                    buildOpen = !buildOpen;
                    collectionOpen = false;
                }
                return;
            }

            if (YardButton(collection, collectionOpen ? "Close" : "Collection", YardIcon.Find,
                primary: collectionOpen, enabled: !placing && Model.HasBottleCap))
            {
                collectionOpen = !collectionOpen;
                buildOpen = false;
                return;
            }

            if (placing)
            {
                var mode = Model.Placement;
                bool moving = mode == CourtyardModel.PlacementMode.MoveStash;
                string action = moving ? "Moving the Snack Stash" :
                    mode == CourtyardModel.PlacementMode.BuildSort ? "Place your Sort Station" : "Place your Snack Stash";
                var check = Model.CheckPlacement(selectedAnchor);
                bool valid = check == CourtyardModel.PlacementCheck.Valid;
                Box(14, 504, 332, 49, Cream);
                if (repainting)
                {
                    string hint = check == CourtyardModel.PlacementCheck.TooClose ? "Leave one cell between buildings" :
                        check == CourtyardModel.PlacementCheck.Unchanged ? "Choose a different spot" :
                        valid ? "Ready to confirm" : "Tap a green snap point";
                    GUI.Label(new Rect(23, 508, 219, 20), action, tileTitle);
                    GUI.Label(new Rect(23, 528, 219, 18), hint, small);
                }
                if (YardButton(new Rect(248, 507, 92, 44), moving ? "Move" : "Place",
                    primary: true, enabled: valid) && Model.TryTargetPlot(selectedAnchor))
                    selectedAnchor = -1;
                return;
            }

            if (buildOpen)
            {
                DrawBuildTray();
                return;
            }

            var contextual = new Rect(23, 505, 314, 47);
            if (Model.CanUpgradeNest)
            {
                if (YardButton(contextual, UpgradeAction)) Model.TryUpgradeNest();
            }
            else if (Model.CanRecruit)
            {
                if (YardButton(contextual, RecruitAction)) Model.TryRecruit();
            }
            else if (repainting)
            {
                string status = "Build a Snack Stash for your crew";
                if (Model.SortPlot >= 0)
                {
                    if (hudSorted != Model.ItemsSorted)
                    {
                        hudSorted = Model.ItemsSorted;
                        sortedText = $"Items sorted: {hudSorted}";
                    }
                    status = sortedText;
                }
                else if (Model.Recruit != null)
                    status = Model.CanBuildSort ? "Build a Sort Station for your crew" : RecruitedStatus;
                else if (Model.StashPlot >= 0)
                    status = Model.SnacksTaken == 0 ? "Watch your crew find the Snack Stash" : "A raccoon found a snack!";
                Box(14, 507, 332, 40, Cream);
                GUI.Label(new Rect(24, 508, 312, 38), status, centered);
            }
        }

        private void DrawBuildTray()
        {
            // Keep the gate, raccoons, and interaction spots visible above the tray.
            Box(14, 499, 332, 54, Cream);
            bool hasStash = Model.StashPlot >= 0;
            bool showSort = Model.Recruit != null;
            var stash = new Rect(21, 503, showSort ? 156 : 318, 46);
            if (YardButton(stash, "Snack Stash", YardIcon.Stash,
                enabled: hasStash || Model.Scrap >= CourtyardModel.SnackCost,
                detail: hasStash ? "Move for free" : StashCostText))
            {
                if (hasStash ? Model.BeginMoveStash() : Model.BeginPlacement())
                {
                    selectedAnchor = -1;
                    buildOpen = false;
                }
                return;
            }
            if (!showSort) return;
            if (YardButton(new Rect(183, 503, 156, 46), "Sort Station", YardIcon.Sort,
                enabled: Model.CanBuildSort, detail: Model.SortPlot >= 0 ? "Placed" : SortCostText) &&
                Model.BeginBuildSort())
            {
                selectedAnchor = -1;
                buildOpen = false;
            }
        }
    }
}
