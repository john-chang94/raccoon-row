using UnityEngine;

namespace DumpsterDynasty
{
    /// <summary>The intentionally small, session-only loop. No reference to scene objects or input.</summary>
    public sealed class CourtyardModel
    {
        public const int SnackCost = 2;
        public const int NestUpgradeCost = 2;
        public const int SortCost = 2;
        public const int RecruitCost = 1;
        public const string RecruitName = "Pip";
        public const float ScavengeSeconds = 12f;
        public const string ZoneName = "Back Alley";
        public const string FirstFindName = "Blue Bottle Cap";
        public const float EatSeconds = 2.1f;
        private static readonly Vector2[] NestSpots =
        {
            new Vector2(67, 488), new Vector2(116, 467), new Vector2(170, 489)
        };
        private static readonly Vector2[] GateRoute =
        {
            new Vector2(330, CourtyardGrid.ApproachLaneY), new Vector2(330, 265),
            new Vector2(180, 265), new Vector2(180, 230)
        };

        public enum TripStage { None, Departing, Scavenging, Returning }
        public TripStage ScavengeStage { get; private set; }
        public float ScavengeRemaining { get; private set; }
        public int LastTripScrap { get; private set; }
        public int CompletedTrips { get; private set; }
        public int ProgressRevision { get; private set; }
        public int AwayIncome { get; private set; }
        public bool TripCompletedWhileAway { get; private set; }
        public bool HasBottleCap { get; private set; }
        public bool LastTripFoundBottleCap { get; private set; }
        public bool GateOpen =>
            (ScavengeStage == TripStage.Departing || ScavengeStage == TripStage.Returning) && routeIndex == GateRoute.Length - 1;
        private int routeIndex;
        private int pendingScrap;
        private readonly System.Func<float> roll;

        public CourtyardModel(System.Func<float> roll = null) => this.roll = roll ?? (() => Random.value);

        public bool TryStartScavenge()
        {
            if (ScavengeStage != TripStage.None) return false;
            ScavengeStage = TripStage.Departing;
            ScavengeRemaining = ScavengeSeconds;
            LastTripScrap = 0;
            LastTripFoundBottleCap = false;
            TripCompletedWhileAway = false;
            // Lock the zone roll at departure so closing the game cannot reroll this trip.
            pendingScrap = roll() < 0.8f ? 2 : 4;
            routeIndex = 0;
            ProgressRevision++;
            return true;
        }

        public int Scrap { get; private set; } = SnackCost;
        public int Food { get; private set; }
        public bool IsNestUpgraded { get; private set; }
        public int NestCapacity => IsNestUpgraded ? 3 : 2;
        public int CrewCount => Recruit == null ? 2 : 3;
        public bool CanUpgradeNest => CompletedTrips > 0 && !IsNestUpgraded && Scrap >= NestUpgradeCost;
        public bool CanRecruit => CrewCount < NestCapacity && Food >= RecruitCost;

        public bool TryUpgradeNest()
        {
            if (!CanUpgradeNest) return false;
            Scrap -= NestUpgradeCost;
            IsNestUpgraded = true;
            ProgressRevision++;
            return true;
        }

        public bool TryRecruit()
        {
            if (!CanRecruit) return false;
            Food -= RecruitCost;
            SpawnRecruit();
            ProgressRevision++;
            return true;
        }
        private void SpawnRecruit() =>
            crew[2] = new Raccoon { Position = new Vector2(204, 451), Activity = Activity.WalkToNest };

        public CourtyardSnapshot Capture(long utcTicks) => new CourtyardSnapshot
        {
            version = CourtyardSaveStore.CurrentVersion, savedUtcTicks = utcTicks, scrap = Scrap, food = Food, stashPlot = StashPlot,
            sortPlot = SortPlot, nestUpgraded = IsNestUpgraded, recruited = Recruit != null,
            trips = CompletedTrips, tripScrap = pendingScrap, tripSeconds = ScavengeRemaining
        };

        public static CourtyardModel Restore(CourtyardSnapshot state, long utcTicks, System.Func<float> roll = null)
        {
            var yard = new CourtyardModel(roll);
            if (!CourtyardSaveStore.IsValid(state)) return yard;
            yard.Scrap = state.scrap;
            yard.Food = state.food;
            yard.StashPlot = state.version == 1 ? CourtyardGrid.FromLegacyPlot(state.stashPlot) : state.stashPlot;
            yard.SortPlot = state.version == 1 ? CourtyardGrid.FromLegacyPlot(state.sortPlot) : state.sortPlot;
            yard.IsNestUpgraded = state.nestUpgraded;
            yard.CompletedTrips = state.trips;
            yard.HasBottleCap = state.trips > 0;
            if (state.recruited) yard.SpawnRecruit();
            yard.pendingScrap = state.tripScrap;
            yard.ScavengeRemaining = state.tripSeconds;
            yard.ApplyAwayProgress(System.TimeSpan.FromTicks(System.Math.Max(0L, utcTicks - state.savedUtcTicks)).TotalSeconds);
            return yard;
        }

        public void ApplyAwayProgress(double seconds)
        {
            seconds = System.Math.Max(0, seconds);
            // Deliberately do not replay animation, bites, or sorted props while away.
            AwayIncome = (int)System.Math.Min(12, System.Math.Floor(seconds / 300));
            Scrap = (int)System.Math.Min(int.MaxValue, (long)Scrap + AwayIncome);
            if (pendingScrap <= 0) return;
            if (seconds >= ScavengeRemaining)
            {
                TripCompletedWhileAway = true;
                Hauler.Position = GateRoute[0];
                SettleTrip();
            }
            else
            {
                ScavengeRemaining -= (float)seconds;
                ScavengeStage = TripStage.Scavenging;
                routeIndex = GateRoute.Length - 1;
                Hauler.Position = GateRoute[routeIndex];
            }
        }

        public int SnacksTaken { get; private set; }
        public int StashPlot { get; private set; } = -1;
        public enum PlacementMode { None, BuildStash, MoveStash, BuildSort }
        public PlacementMode Placement { get; private set; }
        public bool IsPlacing => Placement != PlacementMode.None;
        public int SortPlot { get; private set; } = -1;
        public int ItemsSorted { get; private set; }
        public bool CanBuildSort => Recruit != null && SortPlot < 0 && Scrap >= SortCost;

        public sealed class Raccoon
        {
            public Vector2 Position { get; internal set; }
            public Activity Activity { get; internal set; }
            public float ActivityAge => Age;
            public bool HasCookie => Activity == Activity.PickUpCookie ||
                Activity == Activity.CarryCookie || Activity == Activity.EatCookie;
            internal float Age;
            internal int CookiePlot;
        }
        public enum Activity { Roam, WalkToStash, PickUpCookie, CarryCookie, EatCookie, WalkToSort, Sort, WalkToNest, Rest }

        private readonly Raccoon[] crew =
        {
            new Raccoon { Position = new Vector2(112, 420) },
            new Raccoon { Position = NestSpots[1], Activity = Activity.Rest },
            null
        };
        public Raccoon Scout => crew[0];
        public Raccoon Hauler => crew[1];
        public Raccoon Recruit => crew[2];

        public static Vector2 PlotCenter(int index, CourtyardGrid.Building building = CourtyardGrid.Building.Stash) =>
            CourtyardGrid.WorldFootprint(index, building).center;
        public static Vector2 NestSpot(int index) => NestSpots[index];
        public static Vector2 CookiePickup(int plot) => PlotCenter(plot) + new Vector2(0, 46);
        public static Vector2 CookieSeat(int plot)
        {
            Vector2 center = PlotCenter(plot);
            return new Vector2(center.x + (center.x > 210 ? -36 : 36), Mathf.Max(414f, center.y + 77));
        }

        public static Vector2 SortApproach(int plot) =>
            PlotCenter(plot, CourtyardGrid.Building.Sort) + new Vector2(0, 53);

        public bool BeginPlacement()
        {
            if (IsPlacing || StashPlot >= 0 || Scrap < SnackCost) return false;
            Placement = PlacementMode.BuildStash;
            return true;
        }

        public bool BeginMoveStash()
        {
            if (IsPlacing || StashPlot < 0) return false;
            Placement = PlacementMode.MoveStash;
            return true;
        }

        public bool BeginBuildSort()
        {
            if (IsPlacing || !CanBuildSort) return false;
            Placement = PlacementMode.BuildSort;
            return true;
        }

        public void CancelPlacement() => Placement = PlacementMode.None;

        public enum PlacementCheck { NotPlacing, Outside, Unchanged, TooClose, Valid }

        public PlacementCheck CheckPlacement(int plot)
        {
            if (!IsPlacing) return PlacementCheck.NotPlacing;
            bool isSort = Placement == PlacementMode.BuildSort;
            var building = isSort ? CourtyardGrid.Building.Sort : CourtyardGrid.Building.Stash;
            if (!CourtyardGrid.Fits(plot, building)) return PlacementCheck.Outside;
            if (Placement == PlacementMode.MoveStash && plot == StashPlot) return PlacementCheck.Unchanged;
            return CourtyardGrid.HasClearance(plot, building,
                isSort ? StashPlot : SortPlot, isSort ? CourtyardGrid.Building.Stash : CourtyardGrid.Building.Sort)
                ? PlacementCheck.Valid : PlacementCheck.TooClose;
        }

        public bool CanTargetPlot(int plot) => CheckPlacement(plot) == PlacementCheck.Valid;

        public bool TryPlaceStash(int plot)
        {
            if ((Placement != PlacementMode.BuildStash && Placement != PlacementMode.MoveStash) ||
                !CanTargetPlot(plot)) return false;
            if (Placement == PlacementMode.BuildStash)
            {
                if (StashPlot >= 0 || Scrap < SnackCost) return false;
                Scrap -= SnackCost;
                // First construction gets an immediate autonomous response rather than waiting
                // for the end of a long roam/rest cycle and the trip timer.
                SetActivity(Scout, Activity.WalkToStash);
            }
            else if (Scout.Activity == Activity.PickUpCookie)
            {
                // A handoff not yet complete restarts at the moved stash. A held cookie does not teleport.
                SetActivity(Scout, Activity.WalkToStash);
            }
            StashPlot = plot;
            ProgressRevision++;
            CancelPlacement();
            return true;
        }

        public bool TryTargetPlot(int plot)
        {
            if (Placement != PlacementMode.BuildSort) return TryPlaceStash(plot);
            if (!CanTargetPlot(plot) || !CanBuildSort) return false;
            Scrap -= SortCost;
            SortPlot = plot;
            ProgressRevision++;
            CancelPlacement();
            return true;
        }

        public void Tick(float delta)
        {
            if (delta <= 0f || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            // Keep movement stable in the editor after pauses.
            float step = Mathf.Min(delta, 0.1f);
            // The resident at home always continues their routine.
            TickCrew(0, step);
            if (ScavengeStage == TripStage.None) TickCrew(1, step);
            else TickTrip(step);
            if (Recruit != null) TickCrew(2, step);
        }

        private void TickTrip(float step)
        {
            switch (ScavengeStage)
            {
                case TripStage.Departing:
                    if (!WalkTripWaypoint(step)) return;
                    if (routeIndex < GateRoute.Length - 1) routeIndex++;
                    else ScavengeStage = TripStage.Scavenging;
                    break;
                case TripStage.Scavenging:
                    ScavengeRemaining = Mathf.Max(0, ScavengeRemaining - step);
                    if (ScavengeRemaining > 0) return;
                    ScavengeStage = TripStage.Returning;
                    break;
                case TripStage.Returning:
                    // The departing resident ended on the last waypoint (outside the yard).
                    Vector2 target = GateRoute[routeIndex - 1];
                    Hauler.Position = Vector2.MoveTowards(Hauler.Position, target, 90f * step);
                    if (Hauler.Position != target) return;
                    routeIndex--;
                    if (routeIndex > 0) return;
                    SettleTrip();
                    break;
            }
        }

        private void SettleTrip()
        {
            Scrap = (int)System.Math.Min(int.MaxValue, (long)Scrap + pendingScrap);
            LastTripScrap = pendingScrap;
            pendingScrap = 0;
            ScavengeRemaining = 0;
            LastTripFoundBottleCap = !HasBottleCap;
            if (LastTripFoundBottleCap) Food++;
            HasBottleCap = true;
            if (CompletedTrips < int.MaxValue) CompletedTrips++;
            SetActivity(Hauler, Activity.Roam);
            ScavengeStage = TripStage.None;
            ProgressRevision++;
        }

        private bool WalkTripWaypoint(float step)
        {
            Vector2 target = GateRoute[routeIndex];
            Hauler.Position = Vector2.MoveTowards(Hauler.Position, target, 90f * step);
            return Hauler.Position == target;
        }

        private void TickCrew(int i, float delta)
        {
            Raccoon raccoon = crew[i];
            raccoon.Age += delta;
            switch (raccoon.Activity)
            {
                case Activity.Roam:
                    Vector2 center = i == 0 ? new Vector2(106, 424) :
                        i == 1 ? new Vector2(249, 442) : new Vector2(190, 456);
                    Vector2 roam = center + new Vector2(Mathf.Cos(raccoon.Age * 1.1f) * 29, Mathf.Sin(raccoon.Age * 1.3f) * 17);
                    raccoon.Position = Vector2.MoveTowards(raccoon.Position, roam, 31 * delta);
                    if (raccoon.Age > 6)
                        SetActivity(raccoon, i == 0 && StashPlot >= 0 ? Activity.WalkToStash :
                            i == 2 && SortPlot >= 0 ? Activity.WalkToSort : Activity.WalkToNest);
                    break;
                case Activity.WalkToStash:
                    if (WalkViaYardLane(raccoon, CookiePickup(StashPlot), 52, delta))
                    {
                        raccoon.CookiePlot = StashPlot;
                        SetActivity(raccoon, Activity.PickUpCookie);
                    }
                    break;
                case Activity.PickUpCookie:
                    if (raccoon.Age >= 0.55f) SetActivity(raccoon, Activity.CarryCookie);
                    break;
                case Activity.CarryCookie:
                    if (WalkViaYardLane(raccoon, CookieSeat(raccoon.CookiePlot), 47, delta))
                        SetActivity(raccoon, Activity.EatCookie);
                    break;
                case Activity.EatCookie:
                    if (raccoon.Age >= EatSeconds)
                    {
                        SnacksTaken++;
                        SetActivity(raccoon, Activity.WalkToNest);
                    }
                    break;
                case Activity.WalkToSort:
                    if (WalkViaYardLane(raccoon, SortApproach(SortPlot), 47, delta))
                        SetActivity(raccoon, Activity.Sort);
                    break;
                case Activity.Sort:
                    if (raccoon.Age >= 1.8f)
                    {
                        ItemsSorted++;
                        SetActivity(raccoon, Activity.WalkToNest);
                    }
                    break;
                case Activity.WalkToNest:
                    if (WalkViaYardLane(raccoon, NestSpot(i), 45, delta))
                        SetActivity(raccoon, Activity.Rest);
                    break;
                case Activity.Rest:
                    if (raccoon.Age > 2.5f) SetActivity(raccoon, Activity.Roam);
                    break;
            }
        }

        private static bool WalkViaYardLane(Raccoon raccoon, Vector2 target, float speed, float delta)
        {
            Vector2 current = raccoon.Position;
            // Cross the yard below all movable footprints, then walk up to the interaction spot.
            // Align even when the furnishing moves while a raccoon is on its way.
            if (current.x != target.x)
            {
                Vector2 waypoint = current.y < CourtyardGrid.ApproachLaneY
                    ? new Vector2(current.x, CourtyardGrid.ApproachLaneY)
                    : new Vector2(target.x, current.y);
                raccoon.Position = Vector2.MoveTowards(current, waypoint, speed * delta);
                return raccoon.Position == target;
            }
            raccoon.Position = Vector2.MoveTowards(current, target, speed * delta);
            return raccoon.Position == target;
        }

        private static void SetActivity(Raccoon raccoon, Activity activity)
        {
            raccoon.Activity = activity;
            raccoon.Age = 0;
        }
    }
}
