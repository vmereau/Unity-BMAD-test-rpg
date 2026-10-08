using UnityEngine;

namespace Game.Stealth
{
    /// <summary>
    /// Global stealth / perception tuning. Per-entity values (view angle, eye height, proximity radius,
    /// fill time, lose-sight time, search duration) live on the Entity SO; sight range is
    /// <c>Entity.DetectionRange</c>. Assign StealthConfig.asset in Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Config/Stealth", fileName = "StealthConfig")]
    public class StealthConfigSO : ScriptableObject
    {
        [Header("Sneak Modifiers")]
        [Tooltip("Sight range multiplier against a sneaking target (8 m → 4.8 m at 0.6).")]
        public float sneakSightRangeMultiplier = 0.6f;
        [Tooltip("360° proximity radius multiplier against a sneaking target.")]
        public float sneakProximityMultiplier = 0.35f;
        [Tooltip("Awareness fill rate multiplier against a sneaking target.")]
        public float sneakFillRateMultiplier = 0.5f;

        [Header("Visibility Falloff")]
        [Tooltip("Visibility factor at the far edge of the sight range (1 at distance 0).")]
        [Range(0f, 1f)] public float edgeDistanceFactor = 0.2f;
        [Tooltip("Visibility factor at the side edge of the view cone (1 on the cone axis).")]
        [Range(0f, 1f)] public float peripheralAngleFactor = 0.5f;

        [Header("Awareness")]
        [Tooltip("Awareness lost per second while the target is not visible.")]
        public float awarenessDrainPerSecond = 0.25f;
        [Tooltip("Awareness at or above which an idle/patrolling entity stops and turns toward the last seen point. Must be > 0.")]
        [Range(0.01f, 1f)] public float suspicionThreshold = 0.5f;
        [Tooltip("Awareness kept when an alerted entity loses the target and starts searching.")]
        [Range(0f, 1f)] public float searchStartAwareness = 0.5f;

        [Header("Sneak Attack")]
        [Tooltip("Seconds after taking damage during which an idle entity no longer counts as unaware (no repeated sneak-attack bonus on passive or neutral entities that don't fight back).")]
        [Min(0f)] public float damageAlertDuration = 10f;

        [Header("Sensing")]
        [Tooltip("Layers that block line of sight (level geometry).")]
        public LayerMask lineOfSightMask = 1; // Default
        [Tooltip("Seconds between line-of-sight raycasts.")]
        public float losCheckInterval = 0.1f;
        [Tooltip("Seconds between stealth-target scans in TargetRegistry.")]
        public float targetScanInterval = 0.25f;

        [Header("Target Points")]
        [Tooltip("Height above the target's feet the line-of-sight ray aims at while standing.")]
        public float standingVisibilityHeight = 1.4f;
        [Tooltip("Height above the target's feet the line-of-sight ray aims at while sneaking.")]
        public float sneakingVisibilityHeight = 0.9f;

        [Header("Behaviour")]
        [Tooltip("Degrees/second a suspicious entity turns toward the last seen point.")]
        public float suspiciousTurnSpeed = 180f;
        [Tooltip("Degrees/second a searching entity rotates in place at the last seen point.")]
        public float searchTurnSpeed = 90f;

        [Header("Witness (non-hostile NPCs)")]
        [Tooltip("Degrees/second a watching witness turns to face the player.")]
        public float witnessTurnSpeed = 240f;
        [Tooltip("Seconds without line of sight before a watching witness gives up and resumes its routine.")]
        public float witnessLoseSightTime = 1.5f;
        [Tooltip("Seconds between two warnings (speech bubbles) from the same NPC. Detections inside it watch silently.")]
        public float witnessWarnCooldown = 20f;
        [Tooltip("Seconds the warning bubble stays fully visible.")]
        public float witnessBubbleDuration = 3f;
        [Tooltip("Speech bubble priority of witness warnings (higher replaces lower).")]
        public int witnessBubblePriority = 0;

        [Header("Theft Chase")]
        [Tooltip("Flat distance (m) at which a chasing witness catches the thief and confronts them.")]
        public float theftCatchDistance = 1.8f;
        [Tooltip("Seconds without line of sight before a chasing witness gives up.")]
        public float theftChaseLoseSightTime = 4f;
        [Tooltip("Flat distance (m) to the thief beyond which a chasing witness gives up.")]
        public float theftChaseMaxDistance = 20f;
        [Tooltip("Seconds after which a chasing witness gives up.")]
        public float theftChaseMaxDuration = 30f;
        [Tooltip("Seconds a confronting NPC waits for the scold dialogue to open before resuming its routine.")]
        public float theftConfrontOpenTimeout = 0.5f;
        [Tooltip("Seconds the theft alert bubble stays fully visible.")]
        public float theftBubbleDuration = 2.5f;
        [Tooltip("Speech bubble priority of theft alerts (above witness warnings).")]
        public int theftAlertBubblePriority = 1;

#if UNITY_EDITOR
        private void OnValidate()
        {
            suspicionThreshold      = Mathf.Clamp(suspicionThreshold, 0.01f, 1f); // 0 → Idle/Suspicious flip-flop
            awarenessDrainPerSecond = Mathf.Max(0f, awarenessDrainPerSecond);
            losCheckInterval        = Mathf.Max(0.01f, losCheckInterval);
            targetScanInterval      = Mathf.Max(0.01f, targetScanInterval);
            sneakSightRangeMultiplier = Mathf.Max(0f, sneakSightRangeMultiplier);
            sneakProximityMultiplier  = Mathf.Max(0f, sneakProximityMultiplier);
            sneakFillRateMultiplier   = Mathf.Max(0f, sneakFillRateMultiplier);
            witnessTurnSpeed          = Mathf.Max(0f, witnessTurnSpeed);
            witnessLoseSightTime      = Mathf.Max(0f, witnessLoseSightTime);
            witnessWarnCooldown       = Mathf.Max(0f, witnessWarnCooldown);
            witnessBubbleDuration     = Mathf.Max(0f, witnessBubbleDuration);
            theftCatchDistance        = Mathf.Max(0.5f, theftCatchDistance);
            theftChaseLoseSightTime   = Mathf.Max(0f, theftChaseLoseSightTime);
            theftChaseMaxDistance     = Mathf.Max(0f, theftChaseMaxDistance);
            theftChaseMaxDuration     = Mathf.Max(0f, theftChaseMaxDuration);
            theftConfrontOpenTimeout  = Mathf.Max(0f, theftConfrontOpenTimeout);
            theftBubbleDuration       = Mathf.Max(0f, theftBubbleDuration);
        }
#endif
    }
}
