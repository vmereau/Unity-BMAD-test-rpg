using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Pure-logic core of <see cref="WeaponHitbox"/>: per-window hit dedupe, owner exclusion,
    /// swept sub-step count and query-shape inflation math. No MonoBehaviour — EditMode-testable.
    /// </summary>
    public sealed class HitSweepTracker
    {
        private readonly HashSet<IDamageable> _hitThisWindow = new();

        public bool IsWindowOpen { get; private set; }

        /// <summary>Opens a new hit window — every target can be hit once again.</summary>
        public void BeginWindow()
        {
            _hitThisWindow.Clear();
            IsWindowOpen = true;
        }

        /// <summary>Closes the hit window — no hit registers until the next BeginWindow.</summary>
        public void EndWindow()
        {
            _hitThisWindow.Clear();
            IsWindowOpen = false;
        }

        /// <summary>
        /// Returns true exactly once per target per window. Rejects: window closed, null target,
        /// dead target, target == owner.
        /// </summary>
        public bool TryRegisterHit(IDamageable target, IDamageable owner)
        {
            if (!IsWindowOpen || target == null || target.IsDead) return false;
            if (owner != null && ReferenceEquals(target, owner)) return false;
            return _hitThisWindow.Add(target);
        }

        /// <summary>
        /// Number of pose samples for a frame's travel: ceil(distance / maxStep), clamped [1, maxSubSteps].
        /// maxStepDistance &lt;= 0 or maxSubSteps &lt; 1 → 1.
        /// </summary>
        public static int ComputeSubSteps(float travelDistance, float maxStepDistance, int maxSubSteps)
        {
            if (maxStepDistance <= 0f || maxSubSteps < 1 || travelDistance <= 0f) return 1;
            int steps = Mathf.CeilToInt(travelDistance / maxStepDistance);
            return Mathf.Clamp(steps, 1, maxSubSteps);
        }

        /// <summary>World half-extents of a BoxCollider: |size * lossyScale| * 0.5 + padding on every axis.</summary>
        public static Vector3 ComputeBoxHalfExtents(Vector3 size, Vector3 lossyScale, float padding)
        {
            Vector3 scaled = Vector3.Scale(size, lossyScale);
            return new Vector3(
                Mathf.Abs(scaled.x) * 0.5f + padding,
                Mathf.Abs(scaled.y) * 0.5f + padding,
                Mathf.Abs(scaled.z) * 0.5f + padding);
        }

        /// <summary>World radius: radius * max(|lossyScale.x|, |lossyScale.y|, |lossyScale.z|) + padding.</summary>
        public static float ComputeScaledRadius(float radius, Vector3 lossyScale, float padding)
        {
            float maxScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
            return radius * maxScale + padding;
        }
    }
}
