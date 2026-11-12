using Game.Core;
using Game.Dialogue;
using UnityEngine;

namespace Game.NPC
{
    /// <summary>
    /// One extortion racket shared by its <c>TollGate</c>s (e.g. Harl's quarry and wilderness gates): the shared facts, the
    /// forced toll topics and the signals the toll choices raise. Each gate keeps its own paid fact and hours. The fee itself
    /// is the pay choice's <c>ChoiceOption.goldCost</c> (single source).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/NPC/Toll Racket", fileName = "TollRacket_")]
    public class TollRacketSO : ScriptableObject
    {
        [Header("Shared facts")]
        [Tooltip("Set when the player says they can't pay — the excuse works once for every gate of this racket.")]
        public WorldFact deferredFact;
        [Tooltip("Set when the player knocks the collector out in a fight with him — no gate of this racket collects again.")]
        public WorldFact beatenFact;

        [Header("Forced topics")]
        [Tooltip("Opener when no gate of this racket has been paid yet.")]
        public StartDialogueNode firstStart;
        [Tooltip("Opener when another gate of this racket was already paid (mocks the player, then asks again).")]
        public StartDialogueNode otherGatePaidStart;

        [Header("Answers (ChoiceOption.signal)")]
        public DialogueSignalSO paySignal;
        public DialogueSignalSO deferSignal;
        public DialogueSignalSO refuseSignal;

        [Header("Behaviour")]
        [Tooltip("Flat distance at which the collector stops and opens the toll dialogue.")]
        [Min(0.5f)] public float talkDistance = 2.5f;
        [Tooltip("The collector only collects while standing within this flat distance of the gate's post (on duty).")]
        [Min(1f)] public float dutyRadius = 12f;
        [Tooltip("Seconds the collector walks after the player before giving up.")]
        [Min(1f)] public float interceptTimeout = 12f;
    }
}
