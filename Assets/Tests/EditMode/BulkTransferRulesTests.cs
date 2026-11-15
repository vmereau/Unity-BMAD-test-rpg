using Game.UI;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class BulkTransferRulesTests
    {
        [TestCase(false, 25, 3, 100, 1, TestName = "NoShift_AlwaysOne")]
        [TestCase(true, 25, 3, 100, 10, TestName = "Shift_CappedAtTen")]
        [TestCase(true, 10, 15, 60, 4, TestName = "Shift_CappedByGold")]
        [TestCase(true, 1, 3, 100, 1, TestName = "Shift_SmallStackShowsOne")]
        [TestCase(true, 7, 0, 0, 7, TestName = "Shift_NoPrice_WholeSmallStack")]
        [TestCase(true, 15, 0, 0, 10, TestName = "Shift_NoPrice_CappedAtTen")]
        [TestCase(true, 10, 50, 20, 1, TestName = "Shift_Unaffordable_StillOne")]
        [TestCase(true, 12, 5, 30, 6, TestName = "Shift_SellCappedByNpcGold")]
        [TestCase(true, 0, 5, 30, 0, TestName = "EmptySlot_Zero")]
        [TestCase(false, 0, 5, 30, 0, TestName = "EmptySlot_NoShift_Zero")]
        public void Quantity_FollowsBulkRules(bool bulk, int slotCount, int unitPrice, int budget, int expected)
        {
            Assert.AreEqual(expected, BulkTransferRules.Quantity(bulk, slotCount, unitPrice, budget));
        }

        [TestCase("Buy", 1, "Buy")]
        [TestCase("Buy", 0, "Buy")]
        [TestCase("Buy", 7, "Buy ×7")]
        [TestCase("Take", 10, "Take ×10")]
        public void ActionLabel_ShowsCountAboveOne(string verb, int qty, string expected)
        {
            Assert.AreEqual(expected, BulkTransferRules.ActionLabel(verb, qty));
        }
    }
}
