using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// How an entity fights once engaged (spec-ai-combat-tactics-foundation): read by <see cref="EntityCombatTactics"/>,
    /// which picks Approach / Circle / BackOff / Attack every <see cref="decisionInterval"/> through
    /// <see cref="CombatTacticsRules.Decide"/>. Assigned on the <c>Entity</c> SO; none = the plain chase-and-swing.
    /// Shared between entities — never write to it at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/AI/Combat Style", fileName = "CombatStyle_")]
    public class CombatStyleSO : ScriptableObject
    {
        [Header("Decisions")]
        [Tooltip("Seconds between tactical decisions. Lower = twitchier.")]
        [Min(0.05f)] public float decisionInterval = 0.4f;

        [Header("Spacing")]
        [Tooltip("Circling ring radius = the entity's AttackRange + this margin (m).")]
        [Min(0f)] public float ringMargin = 1.2f;
        [Tooltip("Beyond ring radius + this margin the entity runs in (Approach) instead of circling (m).")]
        [Min(0f)] public float approachMargin = 1.5f;
        [Tooltip("Closer than AttackRange × this, a circling entity steps back out to the ring.")]
        [Range(0f, 1f)] public float backOffFraction = 0.6f;

        [Header("Circling")]
        [Tooltip("Strafe speed while circling (m/s).")]
        [Min(0.1f)] public float circleSpeed = 1.6f;
        [Tooltip("Minimum seconds circling before an attack is considered.")]
        [Min(0f)] public float circleTimeMin = 1.5f;
        [Tooltip("Maximum seconds circling before an attack is considered.")]
        [Min(0f)] public float circleTimeMax = 3f;
        [Tooltip("Chance, per decision while circling, to switch the circling direction.")]
        [Range(0f, 1f)] public float directionChangeChance = 0.1f;

        [Header("Attacking")]
        [Tooltip("Chance, per decision once the circle time elapsed, to go for an attack (an attack token must be free).")]
        [Range(0f, 1f)] public float aggression = 0.5f;
        [Tooltip("Closing in to attack, the agent stops at AttackRange × this, so the brain's range check passes.")]
        [Range(0.1f, 1f)] public float attackApproachFraction = 0.8f;
        [Tooltip("Seconds a held attack token may go without a swing (target unreachable / kiting) before it is given back.")]
        [Min(0.5f)] public float attackCommitTimeout = 2.5f;

        [Header("NavMesh")]
        [Tooltip("Search radius when snapping circle / back-off points onto the NavMesh (m).")]
        [Min(0.1f)] public float navMeshSampleRadius = 1f;

        [Header("Backing off")]
        [Tooltip("Speed while stepping back out to the ring (m/s).")]
        [Min(0.1f)] public float backOffSpeed = 1.8f;

        [Header("Blocking (spec-ai-npc-blocking)")]
        [Tooltip("Chance, per attacker swing step read, to raise (or keep) the guard. 0 = never blocks.")]
        [Range(0f, 1f)] public float blockChance = 0f;
        [Tooltip("Chance a blocked hit is a perfect block (parry: the attacker's stance is hit).")]
        [Range(0f, 1f)] public float perfectBlockChance = 0f;
        [Tooltip("Minimum seconds between the attacker's wind-up and the guard going up.")]
        [Min(0f)] public float reactionDelayMin = 0.15f;
        [Tooltip("Maximum seconds between the attacker's wind-up and the guard going up.")]
        [Min(0f)] public float reactionDelayMax = 0.4f;
        [Tooltip("Seconds the guard stays up after the attacker's swing ends.")]
        [Min(0f)] public float guardHoldTime = 0.3f;
        [Tooltip("Half-angle (°) of the front arc: only attackers inside it are read and blocked.")]
        [Range(0f, 180f)] public float guardArcAngle = 60f;
        [Tooltip("Only an attacker within this distance (m) is read as a threat.")]
        [Min(0f)] public float guardThreatRange = 3.5f;
        [Tooltip("Stance damage of a blocked hit = the hit's stance × this (the guard budget).")]
        [Min(0f)] public float guardStanceMultiplier = 0.7f;

        [Header("Potions (spec-potion-drink-animation)")]
        [Tooltip("Health fraction at or below which the entity backs off and drinks a potion it carries. 0 = never drinks.")]
        [Range(0f, 1f)] public float drinkHealthThreshold = 0f;
        [Tooltip("Potions drunk at most per fight (reset when the entity leaves combat).")]
        [Min(0)] public int maxDrinksPerFight = 1;
        [Tooltip("Seconds after a drink (finished or interrupted) before the next one may start.")]
        [Min(0f)] public float drinkCooldown = 10f;
        [Tooltip("Distance (m) from the target at which a backing-off entity starts drinking.")]
        [Min(0f)] public float drinkSafeDistance = 4.5f;
        [Tooltip("Backing off to drink aims this far (m) past drinkSafeDistance, so the agent actually crosses it.")]
        [Min(0f)] public float drinkBackOffMargin = 0.5f;
        [Tooltip("Seconds backing off before drinking anyway (cornered, or the target follows).")]
        [Min(0f)] public float drinkBackOffMaxTime = 2.5f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (circleTimeMax < circleTimeMin) circleTimeMax = circleTimeMin;
            if (reactionDelayMax < reactionDelayMin) reactionDelayMax = reactionDelayMin;
        }
#endif
    }
}
