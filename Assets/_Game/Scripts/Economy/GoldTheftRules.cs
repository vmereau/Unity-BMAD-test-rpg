using UnityEngine;

namespace Game.Economy
{
    /// <summary>
    /// Pure gold-theft rule (spec-knockout-gold-looting): a robber takes a base amount plus a fraction of the victim's
    /// gold, never more than the victim has. Unit-tested in <c>GoldTheftRulesTests</c>.
    /// </summary>
    public static class GoldTheftRules
    {
        /// <summary><c>min(gold, baseAmount + round(gold × fraction))</c>, never negative.</summary>
        public static int Amount(int gold, int baseAmount, float fraction)
        {
            if (gold <= 0) return 0;
            int wanted = Mathf.Max(0, baseAmount) + Mathf.RoundToInt(gold * Mathf.Max(0f, fraction));
            return Mathf.Min(gold, wanted);
        }
    }
}
