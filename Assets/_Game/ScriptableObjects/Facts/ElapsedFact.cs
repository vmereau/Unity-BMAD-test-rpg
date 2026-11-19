using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Computed fact: true once <see cref="Source"/> is true and <see cref="Hours"/> game hours have passed since it
    /// turned true (time recorded by <see cref="WorldStateManager"/> when a stored fact flips false → true). Use it in
    /// any condition (memory topics, talk openers, quest parts) to delay a dialogue — e.g. "Hobb brings up his rummaged
    /// box 2 h after the take". NOT stored in <c>_worldFacts</c>; Prefix intentionally not set.
    /// The source must be a stored fact (Killed / Dialogue / World / Recipe) — computed facts have no "became true" time.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Facts/Elapsed Fact", fileName = "ElapsedFact_")]
    public class ElapsedFact : Fact
    {
        [Tooltip("Stored fact (Killed / Dialogue / World / Recipe) whose \"became true\" time starts the delay.")]
        [SerializeField] private Fact _source;
        [Tooltip("Game hours (decimals allowed) after the source turned true before this fact is true. 0 = same as the source.")]
        [Min(0f)]
        [SerializeField] private float _hours = 1f;

        public Fact Source => _source;
        public float Hours => _hours;

        /// <summary>Runtime/test initialiser.</summary>
        public ElapsedFact Init(Fact source, float hours)
        {
            _source = source;
            _hours = hours;
            return this;
        }

        /// <summary>The source when this is an <see cref="ElapsedFact"/>, otherwise the fact itself — ignores any delay.</summary>
        public static Fact WithoutDelay(Fact fact) => fact is ElapsedFact ef ? ef.Source : fact;

        /// <summary>For debugging only — not used as a world-fact dictionary key.</summary>
        public override string ToString() =>
            $"Elapsed.{(_source != null ? _source.ToString() : "null")}.{_hours.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}h";
    }

    /// <summary>Pure delay rule for <see cref="ElapsedFact"/>. Tested by <c>ElapsedFactTests</c>.</summary>
    public static class ElapsedFactRules
    {
        /// <summary>
        /// True when the source is true and <paramref name="hours"/> have passed since <paramref name="setAt"/>.
        /// No recorded time (older save, set without a clock), no clock now, or a time after now (a corrupt save's clock
        /// reset to the start) → elapsed. Hours ≤ 0 → the source alone.
        /// </summary>
        public static bool IsElapsed(bool sourceTrue, double? setAt, double? now, double hours)
        {
            if (!sourceTrue) return false;
            if (hours <= 0d || setAt == null || now == null || setAt.Value > now.Value) return true;
            return now.Value - setAt.Value >= hours * GameTime.SECONDS_PER_HOUR;
        }
    }
}
