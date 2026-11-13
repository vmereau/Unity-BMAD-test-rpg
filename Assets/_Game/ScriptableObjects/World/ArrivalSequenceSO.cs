using Game.Core;
using Game.Dialogue;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Data for the arrival event (event-001, spec-area-1-arrival-escort-beating / -hut-briefing): the completion fact, the
    /// Warden's forced speech, the tent mate's briefing, the escort / Warden lines and the escort tunables. Scene objects (guards, routes, spots) stay on
    /// <c>ArrivalSequence</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/World/Arrival Sequence", fileName = "ArrivalSequence_")]
    public class ArrivalSequenceSO : ScriptableObject
    {
        [Header("State")]
        [Tooltip("Set when the arrival is over. Never set = the sequence plays on region load.")]
        public WorldFact completedFact;

        [Header("Lines")]
        [Tooltip("The Warden's forced topic at the stage, before the beating.")]
        public StartDialogueNode wardenSpeech;
        [Tooltip("The tent mate's forced topic at the hut (part 2). Ends on his normal topic list.")]
        public StartDialogueNode hutBriefing;
        [Tooltip("A guard, when the escort sets off.")]
        public BarkSetSO moveBarks;
        [Tooltip("A guard, when the escort stops for a lagging player.")]
        public BarkSetSO waitBarks;
        [Tooltip("The nearest guard, when the player wanders off (before the brawl).")]
        public BarkSetSO strayBarks;
        [Tooltip("The Warden, once the worker is down.")]
        public BarkSetSO wardenClosingBarks;
        [Tooltip("The tent mate, calling over a player who stops short of him at the hut (falls back to the wait lines).")]
        public BarkSetSO tentMateCallBarks;
        [Tooltip("Seconds a scripted bubble stays up.")]
        [Min(0.5f)] public float bubbleDuration = 3.5f;

        [Header("Escort")]
        [Tooltip("A load without the fact warps the player to the start spot when they stand farther than this from it.")]
        [Min(0f)] public float playerStartTolerance = 2f;
        [Tooltip("Flat distance at which a guard counts as arrived at its lane point.")]
        [Min(0.2f)] public float arriveDistance = 1.2f;
        [Tooltip("The guards only move on while the player is within this flat distance of them.")]
        [Min(1f)] public float followDistance = 7f;
        [Tooltip("Seconds between two 'keep up' lines.")]
        [Min(0f)] public float waitBarkCooldown = 8f;
        [Tooltip("Beyond this flat distance from the escort's lane (current or previous lane point) the player is straying: a warning, then a guard comes for them.")]
        [Min(2f)] public float strayDistance = 14f;
        [Tooltip("Seconds the player may stay away after the warning before a guard brawls them.")]
        [Min(0f)] public float strayGraceSeconds = 4f;
        [Tooltip("A guard walks up to a straying player and starts the brawl within this flat distance.")]
        [Min(0.5f)] public float chaseReachDistance = 2f;
        [Tooltip("Seconds a guard walks after a straying player before giving up (the stray rules then start over).")]
        [Min(1f)] public float chaseTimeout = 15f;

        [Header("Stage")]
        [Tooltip("Seconds to wait for the Warden's speech to open before skipping it.")]
        [Min(0.5f)] public float speechOpenTimeout = 5f;
        [Tooltip("Seconds the beating may last before the sequence ends anyway.")]
        [Min(1f)] public float beatingTimeout = 25f;
        [Tooltip("Seconds between the Warden's closing line and releasing the player.")]
        [Min(0f)] public float closingDelay = 3f;

        [Header("Hut")]
        [Tooltip("The tent mate opens his briefing once the player is within this flat distance of him (escort arrived).")]
        [Min(1f)] public float briefingDistance = 3f;
        [Tooltip("Seconds the briefing may fail to open (counted only while it could) before the arrival completes without it.")]
        [Min(0.5f)] public float briefingOpenTimeout = 8f;
    }
}
