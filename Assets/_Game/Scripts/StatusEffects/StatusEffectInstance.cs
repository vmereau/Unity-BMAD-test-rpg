namespace Game.StatusEffects
{
    /// <summary>
    /// One application of a <see cref="StatusEffectSO"/>. Times are absolute <c>GameClock.Now</c> values (game seconds),
    /// never countdowns, so a time skip or a load resolves it from the clock alone.
    /// </summary>
    public class StatusEffectInstance
    {
        public StatusEffectSO Effect { get; }
        public double AppliedAt { get; }
        public double ExpiresAt { get; }
        public float TotalAmount { get; }
        /// <summary>Amount already delivered (0 … <see cref="TotalAmount"/>).</summary>
        public float PaidAmount { get; set; }

        public StatusEffectInstance(StatusEffectSO effect, double appliedAt, double expiresAt, float totalAmount, float paidAmount = 0f)
        {
            Effect = effect;
            AppliedAt = appliedAt;
            ExpiresAt = expiresAt;
            TotalAmount = totalAmount;
            PaidAmount = paidAmount;
        }

        public float RemainingAmount => TotalAmount > PaidAmount ? TotalAmount - PaidAmount : 0f;
    }
}
