using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DumpsterDynasty
{
    [Serializable]
    public sealed class CourtyardSnapshot
    {
        public int version;
        public long savedUtcTicks;
        public int scrap;
        public int food;
        public int stashPlot = -1;
        public int sortPlot = -1;
        public bool nestUpgraded;
        public bool recruited;
        public int trips;
        public int tripScrap;
        public float tripSeconds;
    }

    public sealed class CourtyardSaveStore
    {
        public const int CurrentVersion = 2;
        private readonly string primary, backup, temp;

        public CourtyardSaveStore(string directory)
        {
            primary = Path.Combine(directory, "courtyard.json");
            backup = primary + ".bak";
            temp = primary + ".tmp";
        }

        public CourtyardSnapshot Load(out bool recoveredBackup)
        {
            var state = Read(primary);
            recoveredBackup = false;
            if (state != null) return state;
            state = Read(backup);
            recoveredBackup = state != null;
            return state;
        }

        public void Write(CourtyardSnapshot state)
        {
            if (!IsValid(state)) throw new ArgumentException("Invalid courtyard snapshot.");
            Directory.CreateDirectory(Path.GetDirectoryName(primary));
            // The temp file is never a load candidate. A failed write leaves the old save available.
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(state));
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(primary))
                File.Replace(temp, primary, Read(primary) != null ? backup : null);
            else File.Move(temp, primary);
        }

        private static CourtyardSnapshot Read(string path)
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 4096) return null;
                var state = JsonUtility.FromJson<CourtyardSnapshot>(File.ReadAllText(path));
                return IsValid(state) ? state : null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (ArgumentException) { return null; }
        }

        public static bool IsValid(CourtyardSnapshot state)
        {
            if (state == null || (state.version != 1 && state.version != CurrentVersion) ||
                state.savedUtcTicks <= 0 || state.savedUtcTicks > DateTime.MaxValue.Ticks ||
                state.scrap < 0 || state.food < 0 || state.food > 1 || state.trips < 0 ||
                state.stashPlot < -1 || state.sortPlot < -1 ||
                (state.sortPlot >= 0 && !state.recruited) ||
                (state.recruited && (!state.nestUpgraded || state.food != 0)) ||
                (state.nestUpgraded && state.trips == 0) || (state.food > 0 && state.trips == 0) ||
                float.IsNaN(state.tripSeconds) || state.tripSeconds < 0 ||
                state.tripSeconds > CourtyardModel.ScavengeSeconds)
                return false;
            if (state.version == 1 &&
                (state.stashPlot >= CourtyardGrid.LegacyPlotCount || state.sortPlot >= CourtyardGrid.LegacyPlotCount))
                return false;

            int stash = state.version == 1 ? CourtyardGrid.FromLegacyPlot(state.stashPlot) : state.stashPlot;
            int sort = state.version == 1 ? CourtyardGrid.FromLegacyPlot(state.sortPlot) : state.sortPlot;
            if ((stash >= 0 && !CourtyardGrid.Fits(stash, CourtyardGrid.Building.Stash)) ||
                (sort >= 0 && !CourtyardGrid.Fits(sort, CourtyardGrid.Building.Sort)) ||
                (stash >= 0 && !CourtyardGrid.HasClearance(stash, CourtyardGrid.Building.Stash, sort, CourtyardGrid.Building.Sort)))
                return false;

            return state.tripScrap == 0 ? state.tripSeconds == 0 : state.tripScrap == 2 || state.tripScrap == 4;
        }
    }
}
