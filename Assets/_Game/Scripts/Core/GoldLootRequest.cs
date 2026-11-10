using UnityEngine;

namespace Game.Core
{
    /// <summary>Synchronous collection request. An absent/rejecting receiver leaves it unaccepted.</summary>
    public sealed class GoldLootRequest
    {
        public GameObject Recipient { get; }
        public int Amount { get; }
        public bool Accepted { get; private set; }

        public GoldLootRequest(GameObject recipient, int amount)
        {
            Recipient = recipient;
            Amount = amount;
        }

        /// <summary>Claim before crediting, so duplicate listeners and re-entrant raises cannot pay twice.</summary>
        public bool TryAccept()
        {
            if (Accepted || Recipient == null || Amount <= 0) return false;
            Accepted = true;
            return true;
        }
    }
}
