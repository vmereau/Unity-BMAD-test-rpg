namespace Game.Audio
{
    /// <summary>
    /// Pure distance accumulator for <see cref="FootstepEmitter"/>: one step every <c>stride</c> metres of
    /// grounded horizontal travel. Airborne (past the emitter's grace time) and teleports reset it.
    /// </summary>
    public class FootstepStride
    {
        public const float TELEPORT_DISTANCE = 2f;

        private float _distance;

        public float Accumulated => _distance;

        /// <summary>
        /// Adds this frame's horizontal travel. Returns true when a step is due. Speed below
        /// <paramref name="minSpeed"/> only pauses accumulation (a one-frame stall keeps the cadence).
        /// </summary>
        public bool Advance(float distance, float deltaTime, bool grounded, float stride, float minSpeed)
        {
            if (!grounded || distance > TELEPORT_DISTANCE || deltaTime <= 0f || stride <= 0f)
            {
                _distance = 0f;
                return false;
            }
            if (distance / deltaTime < minSpeed) return false;
            _distance += distance;
            if (_distance < stride) return false;
            _distance -= stride;
            if (_distance >= stride) _distance = 0f; // never owe more than one step
            return true;
        }

        public void Reset() => _distance = 0f;

        /// <summary>True for the run cue / stride.</summary>
        public static bool IsRunning(float speed, float runThreshold) => speed >= runThreshold;
    }
}
