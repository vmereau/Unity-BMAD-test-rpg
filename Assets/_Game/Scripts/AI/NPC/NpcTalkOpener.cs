using System;
using Game.Core;
using Game.Dialogue;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Something an NPC brings up once, the next time the player talks to it: while <see cref="condition"/> is true and
    /// <see cref="played"/> is not, "Talk" opens <see cref="topic"/> first (then the normal topic list) and sets
    /// <see cref="played"/>. Both facts are saved, so it plays once per game. Played by <see cref="NPCPresence"/>.
    /// </summary>
    [Serializable]
    public class NpcTalkOpener
    {
        [Tooltip("The opener is pending while this fact is true (and Played is not). Use an ElapsedFact to bring it up " +
                 "only some game hours after its source fact turned true.")]
        public Fact condition;
        [Tooltip("Topic the NPC opens with; the player then gets the normal topic list.")]
        public StartDialogueNode topic;
        [Tooltip("Set when the opener plays (or is cancelled): it never plays again.")]
        public WorldFact played;
        [Tooltip("Cancelled (Played set) when this NPC saw the player steal its own goods: the chase over that theft " +
                 "ended while Condition was already true (an ElapsedFact's source, ignoring its delay). For \"someone went " +
                 "through my things\" lines.")]
        public bool cancelWhenWitnessed;

        public bool IsComplete => condition != null && topic != null && played != null;
    }

    /// <summary>Pure opener rules for <see cref="NPCPresence"/>. Tested by <c>TalkOpenerRulesTests</c>.</summary>
    public static class TalkOpenerRules
    {
        /// <summary>Plays on Talk: set up, condition true, not yet played, and the NPC isn't chasing / scolding a thief.</summary>
        public static bool IsPending(bool complete, bool conditionMet, bool played, bool chasingThief) =>
            complete && conditionMet && !played && !chasingThief;

        /// <summary>
        /// Spent by a witnessed theft of the NPC's goods: flagged, set up, not yet played, and its condition already true
        /// (a witnessed opening where nothing was taken leaves the opener for a later unseen theft).
        /// </summary>
        public static bool CancelsOnWitness(bool complete, bool cancelWhenWitnessed, bool conditionMet, bool played) =>
            complete && cancelWhenWitnessed && conditionMet && !played;
    }
}
