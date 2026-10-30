namespace Game.Combat
{
    /// <summary>Stance tuning of one owner (AI: <c>Entity</c>, player: <c>CombatConfigSO</c>).</summary>
    public readonly struct StanceSettings
    {
        public readonly float Max;
        public readonly float StaggerThreshold;
        public readonly float RegenDelay;
        public readonly float RegenRate;
        public readonly float StaggerDuration;
        public readonly float KnockdownDuration;

        public StanceSettings(float max, float staggerThreshold, float regenDelay, float regenRate,
            float staggerDuration, float knockdownDuration)
        {
            Max = max;
            StaggerThreshold = staggerThreshold;
            RegenDelay = regenDelay;
            RegenRate = regenRate;
            StaggerDuration = staggerDuration;
            KnockdownDuration = knockdownDuration;
        }
    }

    /// <summary>
    /// Stance meter state shared by <c>EntityStance</c> and <c>PlayerStance</c> (spec-player-stance-stagger-knockdown):
    /// drain through <see cref="StanceMath.Apply"/>, regen after <c>RegenDelay</c> without stance damage, reaction
    /// timers. No regen while reacting (a stagger can't re-arm mid-stagger); a knockdown refills on recovery. No
    /// stance damage while knocked down. Owners handle hyper-armor, death and side effects. Covered by StanceMeterTests.
    /// </summary>
    public class StanceMeter
    {
        public float Current { get; private set; }
        public bool IsStaggered { get; private set; }
        public bool IsKnockedDown { get; private set; }
        public bool IsIncapacitated => IsStaggered || IsKnockedDown;

        private float _reactionTimer;
        private float _timeSinceDamage;

        /// <summary>Full stance, no reaction.</summary>
        public void Reset(in StanceSettings settings)
        {
            Current = settings.Max;
            ClearReaction();
            _timeSinceDamage = 0f;
        }

        /// <summary>Drops the reaction without recovering (death).</summary>
        public void ClearReaction()
        {
            IsStaggered = false;
            IsKnockedDown = false;
            _reactionTimer = 0f;
        }

        /// <summary>
        /// Applies stance damage. Returns the reaction it starts; <paramref name="applied"/> is false when nothing
        /// changed (knocked down, no damage, or a no-knockdown hit already at/below the threshold) — regen keeps running.
        /// </summary>
        public StanceReaction Apply(float amount, bool canKnockDown, in StanceSettings settings, out bool applied)
        {
            applied = false;
            if (IsKnockedDown || amount <= 0f) return StanceReaction.None;

            StanceResult result = StanceMath.Apply(Current, settings.Max, settings.StaggerThreshold, amount, canKnockDown);
            if (result.Stance >= Current) return StanceReaction.None;
            applied = true;
            Current = result.Stance;
            _timeSinceDamage = 0f;

            switch (result.Reaction)
            {
                case StanceReaction.Stagger:
                    IsStaggered = true;
                    _reactionTimer = settings.StaggerDuration;
                    break;
                case StanceReaction.KnockDown:
                    IsStaggered = false;
                    IsKnockedDown = true;
                    _reactionTimer = settings.KnockdownDuration;
                    break;
            }
            return result.Reaction;
        }

        /// <summary>Advances timers and regen. Returns true on the frame a reaction ends (knockdown refills to max).</summary>
        public bool Tick(float deltaTime, in StanceSettings settings)
        {
            if (IsIncapacitated)
            {
                _reactionTimer -= deltaTime;
                if (_reactionTimer > 0f) return false;
                if (IsKnockedDown) Current = settings.Max;
                ClearReaction();
                _timeSinceDamage = 0f;
                return true;
            }

            _timeSinceDamage += deltaTime;
            if (_timeSinceDamage >= settings.RegenDelay && Current < settings.Max)
                Current = StanceMath.Regen(Current, settings.Max, settings.RegenRate, deltaTime);
            return false;
        }
    }
}
