using UnityEngine;

namespace DumpsterDynasty
{
    /// <summary>Portrait blockout. Logical coordinates keep the touch targets and map aligned.</summary>
    public sealed class CourtyardView : MonoBehaviour
    {
        [SerializeField] private Texture2D scoutPortrait;
        [SerializeField] private Texture2D haulerPortrait;
        [SerializeField] private Texture2D recruitPortrait;
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
        private static readonly Color Bedding = new Color32(220, 168, 120, 255);
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
            AddActor(ref count, Model.Scout, scoutPortrait, false);
            if (Model.ScavengeStage != CourtyardModel.TripStage.Scavenging)
                AddActor(ref count, Model.Hauler, haulerPortrait, Model.ScavengeStage != CourtyardModel.TripStage.None);
            if (Model.Recruit != null) AddActor(ref count, Model.Recruit, recruitPortrait, false);
            for (int i = 0; i < count; i++)
                DrawRaccoon(drawOrder[i].Raccoon, drawOrder[i].Portrait, drawOrder[i].Traveling);
        }

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

            Box(0, 191, 360, 66, Fence);
            for (int x = 0; x < 360; x += 24) Box(x, 192, 2, 65, Wood);
            Box(0, 244, 360, 6, LightWood);
            Box(144, 169, 72, 91, Wood);
            if (Model.GateOpen)
            {
                Box(151, 179, 58, 81, Shadow);
                Box(151, 179, 10, 81, LightWood);
                Box(199, 179, 10, 81, LightWood);
            }
            else
            {
                Box(151, 179, 58, 81, LightWood);
                Box(178, 182, 4, 77, Wood);
                Box(162, 214, 10, 5, Wood);
                Box(188, 214, 10, 5, Wood);
            }
            Box(137, 166, 86, 13, Wood);
            Box(0, 256, 360, 5, Shadow);

            Box(20, 280, 44, 18, SoilPatch);
            Box(292, 275, 43, 17, SoilPatch);
            Box(45, 472, 33, 19, SoilPatch);
            Box(293, 492, 26, 11, SoilPatch);
            Weed(29, 289); Weed(323, 280); Weed(63, 480); Weed(308, 500);
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

        private void DrawPlots()
        {
            if (!repainting) return;
            if (Model.StashPlot >= 0)
            {
                Vector2 pos = CourtyardModel.PlotCenter(Model.StashPlot);
                Box(pos.x - 34, pos.y + 17, 68, 10, Shadow);
                Box(pos.x - 29, pos.y - 18, 58, 42, Wood);
                Box(pos.x - 25, pos.y - 15, 50, 17, LightWood);
                if (!Model.Scout.HasCookie)
                    Box(pos.x - 8, pos.y - 12, 16, 10, Cookie);
                Box(pos.x - 25, pos.y + 5, 50, 13, Fence);
            }
            if (Model.SortPlot >= 0)
            {
                Vector2 pos = CourtyardModel.PlotCenter(Model.SortPlot, CourtyardGrid.Building.Sort);
                Box(pos.x - 35, pos.y + 13, 70, 11, Shadow);
                Box(pos.x - 31, pos.y - 21, 62, 30, Wood);
                Box(pos.x - 28, pos.y - 18, 56, 22, LightWood);
                Box(pos.x - 24, pos.y + 7, 8, 17, Wood);
                Box(pos.x + 16, pos.y + 7, 8, 17, Wood);
                Box(pos.x - 17, pos.y - 13, 14, 12, Leaf);
                Box(pos.x + 5, pos.y - 13, 14, 12, CapBlue);
                float moved = Model.Recruit?.Activity == CourtyardModel.Activity.Sort
                    ? Mathf.Clamp01(Model.Recruit.ActivityAge / 1.65f) : 0f;
                Box(pos.x - 13 + 22 * moved, pos.y - 13 - 8 * Mathf.Sin(moved * Mathf.PI), 7, 7, Cream);
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
            Box(29, 462, Model.IsNestUpgraded ? 164 : 112, 33, Shadow);
            if (Model.IsNestUpgraded)
            {
                Box(139, 461, 55, 12, Wood);
                Box(142, 463, 49, 6, Leaf);
            }
            for (int i = 0; i < Model.NestCapacity; i++)
            {
                Vector2 spot = CourtyardModel.NestSpot(i);
                Box(spot.x - 25, spot.y - 20, 50, 26, Wood);
                Box(spot.x - 22, spot.y - 17, 44, 20, Bedding);
                Box(spot.x - 19, spot.y - 13, 14, 12, Cream);
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
                float x = pos.x + 10;
                float y = pos.y - 20 + bob;
                if (pickingUp)
                {
                    // Prop handoff: slide out of the stash and into the Scout's carry position.
                    Vector2 stash = CourtyardModel.PlotCenter(Model.StashPlot);
                    float amount = Mathf.Clamp01(raccoon.ActivityAge / 0.45f);
                    x = Mathf.Lerp(stash.x - 3, x, amount);
                    y = Mathf.Lerp(stash.y - 6, y, amount);
                }
                var bite = seated && raccoon.ActivityAge > 1.05f ? 6 : 9;
                Box(x, y, bite, bite, Wood);
                Box(x + 1, y + 1, bite - 2, bite - 2, Cookie);
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
            if (haulerPortrait != null)
                GUI.DrawTexture(new Rect(213, 84, 29, 30), haulerPortrait, ScaleMode.ScaleToFit, true);
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

            bool canScavenge = Model.ScavengeStage == CourtyardModel.TripStage.None;
            bool placing = Model.IsPlacing;
            if (YardButton(scavenge, canScavenge ? "Scavenge" : "Away", YardIcon.Gate, enabled: canScavenge))
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
                    status = Model.CanBuildSort ? "A Sort Station gives Pip something to do" : RecruitedStatus;
                else if (Model.StashPlot >= 0)
                    status = Model.SnacksTaken == 0 ? "Watch Scout find the Snack Stash" : "Scout found a snack!";
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
