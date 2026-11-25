namespace Game.Combat
{
    /// <summary>
    /// Remembers a combo press made while the current swing's window is still closed, so it fires when the window
    /// opens (spec-sword-shield-take-combo). A combo press otherwise triggers the next state at once, wherever it
    /// lands, and presses before the window are dropped. Buffering makes early presses count and starts every buffered
    /// chain at the window open. Pure logic, no MonoBehaviour — EditMode-testable.
    /// </summary>
    public sealed class ComboPressBuffer
    {
        private float _pressTime;

        /// <summary>True while a press is stored and not yet consumed or cleared.</summary>
        public bool HasPress { get; private set; }

        /// <summary>Stores a press made at <paramref name="now"/>; a later press replaces an earlier one.</summary>
        public void Store(float now)
        {
            HasPress = true;
            _pressTime = now;
        }

        /// <summary>
        /// Called when the combo window opens. True when a press is stored and is at most <paramref name="maxAge"/>
        /// seconds old; the press is cleared either way.
        /// </summary>
        public bool Consume(float now, float maxAge)
        {
            if (!HasPress) return false;
            HasPress = false;
            return now - _pressTime <= maxAge;
        }

        /// <summary>Forgets a stored press (new attack state, window closed, attack ended or cancelled).</summary>
        public void Clear() => HasPress = false;
    }
}
