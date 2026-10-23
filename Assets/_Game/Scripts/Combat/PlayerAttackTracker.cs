namespace Game.Combat
{
    /// <summary>
    /// Pure-logic core of the player attack lifecycle used by <see cref="PlayerCombat"/>: follows what the
    /// Animator actually does instead of what input requested. No MonoBehaviour — EditMode-testable.
    ///
    /// Unity fires <c>OnStateEnter(next)</c> at transition start and <c>OnStateExit(prev)</c> at transition end,
    /// so a chain keeps <see cref="ActiveStates"/> ≥ 1 and the attack only ends when it returns to 0.
    /// A press whose trigger the Animator never takes leaves no lasting state: the attack still ends on the
    /// outgoing state's exit, or through <see cref="IsStalled"/> when no attack state was ever entered.
    /// </summary>
    public sealed class PlayerAttackTracker
    {
        /// <summary>Attack states currently entered and not yet exited (2 during a combo crossfade).</summary>
        public int ActiveStates { get; private set; }

        /// <summary>True from a combo press until the Animator enters the next attack state.</summary>
        public bool IsStepQueued { get; private set; }

        private float _lastActivityTime;

        /// <summary>
        /// An attack trigger was fired. <paramref name="isChainStep"/> = a combo follow-up, not Attack_1.
        /// A fresh Attack_1 press only happens once the previous attack ended, so no attack state can still be
        /// active: the count is resynced to 0, repairing any drift from an exit Unity never delivered.
        /// </summary>
        public void Press(float now, bool isChainStep)
        {
            if (isChainStep) IsStepQueued = true;
            else ActiveStates = 0;
            _lastActivityTime = now;
        }

        /// <summary>SMB OnStateEnter of an attack state.</summary>
        public void Enter(float now)
        {
            ActiveStates++;
            IsStepQueued = false;
            _lastActivityTime = now;
        }

        /// <summary>SMB OnStateExit of an attack state. Returns true when no attack state is left — the attack ended.</summary>
        public bool Exit(float now)
        {
            if (ActiveStates > 0) ActiveStates--;
            _lastActivityTime = now;
            return ActiveStates == 0;
        }

        /// <summary>Any attack clip event (hitbox / combo window) — proves the attack is still playing.</summary>
        public void Event(float now) => _lastActivityTime = now;

        /// <summary>
        /// A ComboWindowClose must not reset the combo while the next step is queued or while the outgoing
        /// clip fires its close event during the crossfade (two attack states active).
        /// </summary>
        public bool ShouldIgnoreWindowClose => IsStepQueued || ActiveStates > 1;

        /// <summary>True when an attack is flagged but no attack callback or event arrived for <paramref name="timeout"/> seconds.</summary>
        public bool IsStalled(bool isAttacking, float now, float timeout) =>
            isAttacking && now - _lastActivityTime > timeout;

        /// <summary>Forgets every queued step. Active state count is kept — the Animator still owns those states.</summary>
        public void ClearQueue() => IsStepQueued = false;

        /// <summary>Full reset (forced end) — the Animator is assumed to have left every attack state.</summary>
        public void Reset()
        {
            ActiveStates = 0;
            IsStepQueued = false;
        }
    }
}
