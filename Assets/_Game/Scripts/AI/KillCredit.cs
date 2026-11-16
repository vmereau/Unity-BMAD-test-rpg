using UnityEngine;

namespace Game.AI
{
    /// <summary>Why the player is credited with a kill / knockout (spec-kill-xp-credit-assist). None = no XP.</summary>
    public enum KillCreditKind { None, Player, Friendly, Assist }

    /// <summary>
    /// Player credit for a kill or knockout, decided by the victim from the killing blow's source: the player's own
    /// blow, a blow from an NPC Friendly to the player (<see cref="PlayerRelationship"/>), or an assist (the player
    /// landed damage on the victim earlier). Unknown source → assist only.
    /// </summary>
    public static class KillCredit
    {
        /// <summary>Credit for a blow dealt by <paramref name="killer"/> (null = unknown source).</summary>
        public static KillCreditKind Resolve(GameObject killer, bool playerAssisted)
        {
            FactionMember member = FactionMember.Of(killer);
            bool isPlayer = member != null && member.IsPlayer;
            bool isFriendly = !isPlayer && member != null && PlayerRelationship.Of(member) == RelationshipTier.Friendly;
            return Evaluate(isPlayer, isFriendly, playerAssisted);
        }

        /// <summary>Pure rule (KillCreditTests): first match of player → friendly → assist.</summary>
        public static KillCreditKind Evaluate(bool killerIsPlayer, bool killerIsFriendly, bool playerAssisted)
        {
            if (killerIsPlayer) return KillCreditKind.Player;
            if (killerIsFriendly) return KillCreditKind.Friendly;
            return playerAssisted ? KillCreditKind.Assist : KillCreditKind.None;
        }
    }
}
