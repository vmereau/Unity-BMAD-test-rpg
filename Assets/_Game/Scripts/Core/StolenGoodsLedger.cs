using System.Collections.Generic;
using Game.Inventory;
using UnityEngine;

namespace Game.Core
{
    /// <summary>One stolen item type and how many units of it.</summary>
    [System.Serializable]
    public struct StolenItemCount
    {
        public ItemSO item;
        public int count;

        public StolenItemCount(ItemSO item, int count)
        {
            this.item = item;
            this.count = count;
        }
    }

    /// <summary>
    /// What each theft incident took (spec-theft-return-or-fight), keyed by <c>TheftCommittedData.theftId</c>: the
    /// picked-up item, or every unit taken from an owned container after it was opened (gold kept apart, as an amount).
    /// Written by the player's <c>StolenGoodsTracker</c>, read by the catching NPC. Incidents the player refused to give
    /// back stay marked until the knockout winner recovers them or the refusal brawl ends. Runtime only (not saved).
    /// </summary>
    public static class StolenGoodsLedger
    {
        private sealed class Incident
        {
            public readonly List<StolenItemCount> Items = new List<StolenItemCount>();
            public int Gold;
        }

        private static readonly Dictionary<int, Incident> s_incidents = new Dictionary<int, Incident>();
        private static readonly HashSet<int> s_refused = new HashSet<int>();
        private static readonly List<int> s_scratch = new List<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => ClearAll();

        /// <summary>Forgets every incident and refusal (play-mode reset, tests).</summary>
        public static void ClearAll()
        {
            s_incidents.Clear();
            s_refused.Clear();
        }

        public static void Record(int theftId, ItemSO item, int count = 1)
        {
            if (item == null || count <= 0) return;
            List<StolenItemCount> items = GetOrCreate(theftId).Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].item != item) continue;
                items[i] = new StolenItemCount(item, items[i].count + count);
                return;
            }
            items.Add(new StolenItemCount(item, count));
        }

        public static void RecordGold(int theftId, int amount)
        {
            if (amount <= 0) return;
            GetOrCreate(theftId).Gold += amount;
        }

        /// <summary>True when any of <paramref name="theftIds"/> recorded an item or gold.</summary>
        public static bool HasGoods(IReadOnlyList<int> theftIds)
        {
            if (theftIds == null) return false;
            for (int i = 0; i < theftIds.Count; i++)
                if (s_incidents.TryGetValue(theftIds[i], out Incident inc) && (inc.Gold > 0 || inc.Items.Count > 0))
                    return true;
            return false;
        }

        /// <summary>Sums the goods of <paramref name="theftIds"/> into <paramref name="items"/> (cleared first, one entry per
        /// item type, in recording order); returns the summed gold. Unknown ids are skipped; a repeated id counts once.</summary>
        public static int Collect(IReadOnlyList<int> theftIds, List<StolenItemCount> items)
        {
            items?.Clear();
            if (theftIds == null) return 0;
            int gold = 0;
            for (int i = 0; i < theftIds.Count; i++)
            {
                int id = theftIds[i];
                if (IndexOf(theftIds, id) != i) continue; // duplicate
                if (!s_incidents.TryGetValue(id, out Incident inc)) continue;
                gold += inc.Gold;
                if (items == null) continue;
                foreach (StolenItemCount entry in inc.Items) Add(items, entry.item, entry.count);
            }
            return gold;
        }

        /// <summary>Forgets these incidents (goods returned, or the matter is closed).</summary>
        public static void Clear(IReadOnlyList<int> theftIds)
        {
            if (theftIds == null) return;
            for (int i = 0; i < theftIds.Count; i++)
            {
                s_incidents.Remove(theftIds[i]);
                s_refused.Remove(theftIds[i]);
            }
        }

        /// <summary>The player refused to give these back: the NPC that knocks them out recovers them.</summary>
        public static void MarkRefused(IReadOnlyList<int> theftIds)
        {
            if (theftIds == null) return;
            for (int i = 0; i < theftIds.Count; i++)
                if (s_incidents.ContainsKey(theftIds[i])) s_refused.Add(theftIds[i]);
        }

        public static bool HasRefused => s_refused.Count > 0;

        public static bool IsRefused(int theftId) => s_refused.Contains(theftId);

        /// <summary>Forgets every refused incident (no knockout winner took them: the matter is closed).</summary>
        public static void ClearRefused()
        {
            foreach (int id in s_refused) s_incidents.Remove(id);
            s_refused.Clear();
        }

        /// <summary>Sums every refused incident into <paramref name="items"/> and returns its gold, then forgets them.</summary>
        public static int TakeRefused(List<StolenItemCount> items)
        {
            s_scratch.Clear();
            s_scratch.AddRange(s_refused);
            int gold = Collect(s_scratch, items);
            Clear(s_scratch);
            s_scratch.Clear();
            return gold;
        }

        private static Incident GetOrCreate(int theftId)
        {
            if (!s_incidents.TryGetValue(theftId, out Incident inc))
            {
                inc = new Incident();
                s_incidents.Add(theftId, inc);
            }
            return inc;
        }

        private static void Add(List<StolenItemCount> items, ItemSO item, int count)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].item != item) continue;
                items[i] = new StolenItemCount(item, items[i].count + count);
                return;
            }
            items.Add(new StolenItemCount(item, count));
        }

        private static int IndexOf(IReadOnlyList<int> list, int value)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == value) return i;
            return -1;
        }
    }
}
