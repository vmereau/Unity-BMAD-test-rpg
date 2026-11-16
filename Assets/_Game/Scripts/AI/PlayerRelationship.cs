using System.Collections.Generic;
using Game.Core;
using Game.Factions;
using Game.NPC;
using _Game.ScriptableObjects.Entities;

namespace Game.AI
{
    /// <summary>How an NPC stands towards the player (spec-player-relationship-tiers).</summary>
    public enum RelationshipTier { Hostile, Neutral, Friendly }

    /// <summary>
    /// The single readable source of an NPC's relationship tier with the player. Hostile = the factions are hostile
    /// (faction-only); otherwise an active <see cref="NPCEntity"/> override (Neutral / Friendly, gated by facts) wins;
    /// otherwise Friendly when either faction lists the other as allied, else Neutral. Read live — never cache it.
    /// </summary>
    public static class PlayerRelationship
    {
        /// <summary>Tier of <paramref name="npc"/> towards the registered player. No player / faction → Neutral.</summary>
        public static RelationshipTier Of(FactionMember npc)
        {
            FactionMember player = TargetRegistry.Player;
            if (npc == null || player == null || npc == player || npc.Faction == null || player.Faction == null)
                return RelationshipTier.Neutral;

            bool hostile = FactionSO.AreHostile(npc.Faction, player.Faction);
            bool allied = npc.Faction.IsAlliedWith(player.Faction) || player.Faction.IsAlliedWith(npc.Faction);
            PlayerRelationshipOverride ov = PlayerRelationshipOverride.Faction;
            bool conditionMet = false;
            if (!hostile && npc.Entity is NPCEntity entity)
            {
                ov = entity.PlayerRelationship;
                if (ov != PlayerRelationshipOverride.Faction) conditionMet = AnyFactTrue(entity.RelationshipConditions);
            }
            return Resolve(hostile, allied, ov, conditionMet);
        }

        /// <summary>Pure tier rule (tested by PlayerRelationshipTests).</summary>
        public static RelationshipTier Resolve(bool hostile, bool allied, PlayerRelationshipOverride ov, bool conditionMet)
        {
            if (hostile) return RelationshipTier.Hostile;
            if (conditionMet && ov == PlayerRelationshipOverride.Friendly) return RelationshipTier.Friendly;
            if (conditionMet && ov == PlayerRelationshipOverride.Neutral) return RelationshipTier.Neutral;
            return allied ? RelationshipTier.Friendly : RelationshipTier.Neutral;
        }

        // Empty list = always; no WorldStateManager = no condition can be read → false.
        private static bool AnyFactTrue(IReadOnlyList<Fact> conditions)
        {
            if (conditions == null || conditions.Count == 0) return true;
            WorldStateManager wsm = WorldStateManager.Instance;
            if (wsm == null) return false;
            for (int i = 0; i < conditions.Count; i++)
                if (conditions[i] != null && wsm.GetFact(conditions[i])) return true;
            return false;
        }
    }
}
