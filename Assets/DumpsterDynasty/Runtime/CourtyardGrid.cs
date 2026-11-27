using UnityEngine;

namespace DumpsterDynasty
{
    /// <summary>Placement geometry in courtyard coordinates, independent of rendering and input.</summary>
    public static class CourtyardGrid
    {
        public enum Building { Stash, Sort }
        public const int Columns = 8, Rows = 4, CellSize = 32;
        public const int AnchorCount = Columns * Rows;
        public const int ClearanceCells = 1;
        public const int LegacyPlotCount = 3;
        public const float ApproachLaneY = 425f;
        private static readonly Vector2Int StashSize = new Vector2Int(2, 2);
        private static readonly Vector2Int SortSize = new Vector2Int(2, 2);
        public static readonly Vector2 Origin = new Vector2(52f, 274f);

        public static int Anchor(int column, int row) => row * Columns + column;
        public static int FromLegacyPlot(int index) => index < 0 ? -1 : Anchor(index * 3, 1);

        public static RectInt Cells(int anchor, Building building)
        {
            var size = Footprint(building);
            return new RectInt(anchor % Columns, anchor / Columns, size.x, size.y);
        }

        // Both slice furnishings currently use 2x2 cells. Keep the dimensions per building
        // so art and animation can independently inform later footprint adjustments.
        public static Vector2Int Footprint(Building building) =>
            building == Building.Stash ? StashSize : SortSize;

        public static bool Fits(int anchor, Building building)
        {
            if (anchor < 0 || anchor >= AnchorCount) return false;
            RectInt cells = Cells(anchor, building);
            return cells.xMax <= Columns && cells.yMax <= Rows;
        }

        public static bool HasClearance(int anchor, Building building, int other, Building otherBuilding)
        {
            if (other < 0) return true;
            RectInt a = Cells(anchor, building), b = Cells(other, otherBuilding);
            return a.xMax + ClearanceCells <= b.xMin || b.xMax + ClearanceCells <= a.xMin ||
                a.yMax + ClearanceCells <= b.yMin || b.yMax + ClearanceCells <= a.yMin;
        }

        public static Rect WorldFootprint(int anchor, Building building)
        {
            RectInt cells = Cells(anchor, building);
            Vector2 start = Origin + new Vector2(cells.x, cells.y) * CellSize;
            return new Rect(start, new Vector2(cells.width, cells.height) * CellSize);
        }
    }
}
