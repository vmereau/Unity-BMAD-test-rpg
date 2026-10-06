namespace Game.AI
{
    /// <summary>
    /// Pure combo bookkeeping for an AI melee attack (owned by <see cref="EntityMeleeAttacker"/>).
    /// Step 1 is started by TriggerAttack; steps 2..N are requested on ComboWindowOpen.
    /// </summary>
    public sealed class AttackComboPlan
    {
        /// <summary>Hits in the current combo; 0 when idle.</summary>
        public int TotalHits { get; private set; }

        /// <summary>Step being played: 0 when idle, 1..<see cref="TotalHits"/> during an attack.</summary>
        public int CurrentStep { get; private set; }

        public bool IsActive => TotalHits > 0;

        /// <summary>Starts a combo at step 1. <paramref name="maxSteps"/> &lt; 1 → 1; hits clamped to [1, maxSteps].</summary>
        public void Begin(int hits, int maxSteps)
        {
            if (maxSteps < 1) maxSteps = 1;
            if (hits < 1) hits = 1;
            if (hits > maxSteps) hits = maxSteps;
            TotalHits = hits;
            CurrentStep = 1;
        }

        /// <summary>Advances to the next step while one remains; <paramref name="nextStep"/> = new step, else 0.</summary>
        public bool TryAdvance(out int nextStep)
        {
            if (!IsActive || CurrentStep >= TotalHits)
            {
                nextStep = 0;
                return false;
            }
            CurrentStep++;
            nextStep = CurrentStep;
            return true;
        }

        public void Reset()
        {
            TotalHits = 0;
            CurrentStep = 0;
        }
    }
}
