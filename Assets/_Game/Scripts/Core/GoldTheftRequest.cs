using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Synchronous gold-theft request (spec-knockout-gold-looting): a robber asks the <see cref="Victim"/>'s gold holder
    /// to give up <c>BaseAmount + Fraction × gold</c>. The holder fills the result; an absent receiver leaves it unhandled.
    /// </summary>
    public sealed class GoldTheftRequest
    {
        public GameObject Victim { get; }
        public int BaseAmount { get; }
        public float Fraction { get; }

        public bool Handled { get; private set; }
        /// <summary>Victim's gold before the theft.</summary>
        public int GoldBefore { get; private set; }
        /// <summary>Gold actually removed from the victim.</summary>
        public int Taken { get; private set; }

        public GoldTheftRequest(GameObject victim, int baseAmount, float fraction)
        {
            Victim = victim;
            BaseAmount = baseAmount;
            Fraction = fraction;
        }

        /// <summary>Fills the result once; later calls (duplicate listeners) are refused.</summary>
        public bool TryHandle(int goldBefore, int taken)
        {
            if (Handled || Victim == null) return false;
            Handled = true;
            GoldBefore = goldBefore;
            Taken = Mathf.Max(0, taken);
            return true;
        }
    }
}
