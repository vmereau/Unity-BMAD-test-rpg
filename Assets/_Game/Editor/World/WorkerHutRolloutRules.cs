using System.Collections.Generic;

namespace Game.Editor.World
{
    /// <summary>
    /// Which shell a graybox hut plot gets, from plain measurements (no scene access): see
    /// <see cref="WorkerHutRollout"/> for how a plot is measured.
    /// </summary>
    public static class WorkerHutRolloutRules
    {
        /// <summary>Ground higher than this above the floor line shows as a mound in a hut without a floor.</summary>
        public const float MAX_RISE = 0.15f;
        /// <summary>The corner posts reach this far below the floor line; lower ground leaves them in the air.</summary>
        public const float MAX_DROP = 0.3f;

        public const string HOME = "WorkerHut_Home";
        public const string HOME_B = "WorkerHut_Home_B";
        public const string HOME_MIRRORED = "WorkerHut_Home_C";
        public const string TIGHT = "WorkerHut_Tight";
        public const string TIGHT_MIRRORED = "WorkerHut_Tight_B";
        public const string TIGHT_C = "WorkerHut_Tight_C";
        public const string TIGHT_MIRRORED_D = "WorkerHut_Tight_D";
        public const string LONG = "WorkerHut_Long";

        /// <summary>What was measured on one graybox plot.</summary>
        public readonly struct Plot
        {
            public readonly string Name;
            /// <summary>Highest ground in the room above the floor line, and lowest ground under the walls below it.</summary>
            public readonly float Rise, Drop;
            /// <summary>Nothing solid stands in front of the doorway on the default (+x) or the mirrored (−x) half.</summary>
            public readonly bool DefaultDoorFree, MirroredDoorFree;
            /// <summary>The normal roof would cross a neighbour's roof or another building.</summary>
            public readonly bool Crowded;
            /// <summary>The 4 m hut fits: its extra metre is free of colliders, lanes and the walking gap.</summary>
            public readonly bool LongFits;

            public Plot(string name, float rise, float drop, bool defaultDoorFree, bool mirroredDoorFree, bool crowded, bool longFits)
            {
                Name = name;
                Rise = rise;
                Drop = drop;
                DefaultDoorFree = defaultDoorFree;
                MirroredDoorFree = mirroredDoorFree;
                Crowded = crowded;
                LongFits = longFits;
            }
        }

        /// <summary>
        /// The shells this plot may get, in the order of preference its name gives (so a rerun on the same camp makes
        /// the same choices). Empty when the plot stays graybox: <paramref name="skipReason"/> then says why.
        /// </summary>
        public static string[] Candidates(Plot plot, out string skipReason)
        {
            skipReason = null;
            if (plot.Rise > MAX_RISE || plot.Drop > MAX_DROP)
            {
                skipReason = $"bank in the footprint (ground {plot.Rise:F2} m above and {plot.Drop:F2} m below the floor line; limits {MAX_RISE:F2} / {MAX_DROP:F2})";
                return System.Array.Empty<string>();
            }
            if (!plot.DefaultDoorFree && !plot.MirroredDoorFree)
            {
                skipReason = "both halves of the doorway are blocked";
                return System.Array.Empty<string>();
            }

            var pool = new List<string>(4);
            if (plot.Crowded)
            {
                // Default and mirrored doorways alternate in the pool, so neighbours differ in both seed and door side.
                if (plot.DefaultDoorFree) pool.Add(TIGHT);
                if (plot.MirroredDoorFree) pool.Add(TIGHT_MIRRORED);
                if (plot.DefaultDoorFree) pool.Add(TIGHT_C);
                if (plot.MirroredDoorFree) pool.Add(TIGHT_MIRRORED_D);
            }
            else if (plot.LongFits && plot.DefaultDoorFree)
            {
                pool.Add(LONG);
            }
            else
            {
                if (plot.DefaultDoorFree)
                {
                    pool.Add(HOME);
                    pool.Add(HOME_B);
                }
                if (plot.MirroredDoorFree) pool.Add(HOME_MIRRORED);
            }

            // Rotate the pool by the name's hash: the same plot always starts from the same shell.
            int start = (int)(Hash(plot.Name) % (uint)pool.Count);
            var ordered = new string[pool.Count];
            for (int i = 0; i < pool.Count; i++) ordered[i] = pool[(start + i) % pool.Count];
            return ordered;
        }

        /// <summary>
        /// The candidate the fewest neighbours already show; the earlier one on a tie. Null without candidates.
        /// </summary>
        public static string Pick(IReadOnlyList<string> candidates, IReadOnlyList<string> neighbours)
        {
            string best = null;
            int bestCount = int.MaxValue;
            foreach (string candidate in candidates)
            {
                int count = 0;
                foreach (string neighbour in neighbours)
                    if (neighbour == candidate) count++;
                if (count < bestCount)
                {
                    best = candidate;
                    bestCount = count;
                }
            }
            return best;
        }

        /// <summary>FNV-1a: the same on every machine and run, unlike <c>string.GetHashCode</c>.</summary>
        public static uint Hash(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text ?? string.Empty)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }
}
