using Game.AI;
using Game.NPC;
using NUnit.Framework;

public class PlayerRelationshipTests
{
    [TestCase(true, false, PlayerRelationshipOverride.Friendly, true, RelationshipTier.Hostile)]   // hostile beats an override
    [TestCase(true, true, PlayerRelationshipOverride.Faction, false, RelationshipTier.Hostile)]
    [TestCase(true, false, PlayerRelationshipOverride.Neutral, true, RelationshipTier.Hostile)]    // can't calm a hostile faction
    [TestCase(false, false, PlayerRelationshipOverride.Faction, false, RelationshipTier.Neutral)]
    [TestCase(false, true, PlayerRelationshipOverride.Faction, false, RelationshipTier.Friendly)]  // allied faction
    [TestCase(false, false, PlayerRelationshipOverride.Friendly, true, RelationshipTier.Friendly)] // Tam fed
    [TestCase(false, false, PlayerRelationshipOverride.Friendly, false, RelationshipTier.Neutral)] // Tam not fed yet
    [TestCase(false, true, PlayerRelationshipOverride.Friendly, false, RelationshipTier.Friendly)] // falls back to faction
    [TestCase(false, true, PlayerRelationshipOverride.Neutral, true, RelationshipTier.Neutral)]    // override beats alliance
    [TestCase(false, true, PlayerRelationshipOverride.Neutral, false, RelationshipTier.Friendly)]
    public void Resolve_Matrix(bool hostile, bool allied, PlayerRelationshipOverride ov, bool conditionMet,
        RelationshipTier expected)
    {
        Assert.AreEqual(expected, PlayerRelationship.Resolve(hostile, allied, ov, conditionMet));
    }

    [Test]
    public void Of_NullMember_IsNeutral()
    {
        Assert.AreEqual(RelationshipTier.Neutral, PlayerRelationship.Of(null));
    }
}
