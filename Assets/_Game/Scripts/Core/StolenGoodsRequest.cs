using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Synchronous request to the <see cref="Victim"/>'s inventory and gold holder (spec-theft-return-or-fight): how many of
    /// the stolen <see cref="Items"/> / <see cref="Gold"/> it still carries, and with <see cref="Take"/> removes them.
    /// The inventory fills the item part, the gold holder the gold part; an absent receiver leaves its part unhandled
    /// (= nothing available). Results are what is available (query) or what was actually removed (take).
    /// </summary>
    public sealed class StolenGoodsRequest
    {
        public GameObject Victim { get; }
        public IReadOnlyList<StolenItemCount> Items { get; }
        public int Gold { get; }
        public bool Take { get; }

        private readonly int[] _itemResults;

        public bool ItemsHandled { get; private set; }
        public bool GoldHandled { get; private set; }
        /// <summary>Gold available (query) or removed (take).</summary>
        public int GoldResult { get; private set; }

        public StolenGoodsRequest(GameObject victim, IReadOnlyList<StolenItemCount> items, int gold, bool take)
        {
            Victim = victim;
            Items = items ?? System.Array.Empty<StolenItemCount>();
            Gold = Mathf.Max(0, gold);
            Take = take;
            _itemResults = new int[Items.Count];
        }

        /// <summary>Units of <c>Items[index]</c> available (query) or removed (take).</summary>
        public int ItemResult(int index) => index >= 0 && index < _itemResults.Length ? _itemResults[index] : 0;

        /// <summary>True when any item unit or gold is available / was removed.</summary>
        public bool HasAny
        {
            get
            {
                if (GoldResult > 0) return true;
                for (int i = 0; i < _itemResults.Length; i++)
                    if (_itemResults[i] > 0) return true;
                return false;
            }
        }

        /// <summary>Fills the item results once (clamped to [0, requested]); later calls are refused.</summary>
        public bool TryHandleItems(int[] results)
        {
            if (ItemsHandled || Victim == null || results == null || results.Length != _itemResults.Length) return false;
            ItemsHandled = true;
            for (int i = 0; i < results.Length; i++)
                _itemResults[i] = Mathf.Clamp(results[i], 0, Items[i].count);
            return true;
        }

        /// <summary>Fills the gold result once (clamped to [0, requested]); later calls are refused.</summary>
        public bool TryHandleGold(int gold)
        {
            if (GoldHandled || Victim == null) return false;
            GoldHandled = true;
            GoldResult = Mathf.Clamp(gold, 0, Gold);
            return true;
        }
    }
}
