namespace Game.Combat
{
    /// <summary>What one <see cref="DrinkTimeline.Tick"/> produced.</summary>
    public enum DrinkTick { None, Sip, Done }

    /// <summary>
    /// Timing of one potion drink, shared by the player and NPCs (spec-potion-drink-animation). Pure — no Unity
    /// objects, EditMode-tested. The heal lands once at <c>healTime</c> (the sip); the drink ends at <c>duration</c>.
    /// A drink interrupted before the sip never heals.
    /// </summary>
    public struct DrinkTimeline
    {
        private float _elapsed;
        private float _healTime;
        private float _duration;

        public bool IsActive { get; private set; }
        public bool HasSipped { get; private set; }

        public void Begin(float healTime, float duration)
        {
            _healTime = healTime < 0f ? 0f : healTime;
            _duration = duration < _healTime ? _healTime : duration;
            _elapsed = 0f;
            HasSipped = false;
            IsActive = true;
        }

        /// <summary>
        /// Advances by <paramref name="dt"/>. Returns <see cref="DrinkTick.Sip"/> the frame the sip time is reached (once),
        /// <see cref="DrinkTick.Done"/> the frame the drink ends (a frame reaching both returns Sip, Done on the next).
        /// </summary>
        public DrinkTick Tick(float dt)
        {
            if (!IsActive) return DrinkTick.None;
            _elapsed += dt;
            if (!HasSipped && _elapsed >= _healTime)
            {
                HasSipped = true;
                return DrinkTick.Sip;
            }
            if (HasSipped && _elapsed >= _duration)
            {
                IsActive = false;
                return DrinkTick.Done;
            }
            return DrinkTick.None;
        }

        /// <summary>Stops the drink. Returns true when it was running and had not reached the sip (heal lost).</summary>
        public bool Interrupt()
        {
            if (!IsActive) return false;
            IsActive = false;
            return !HasSipped;
        }
    }
}
