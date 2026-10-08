using System;
using Game.Core;
using Game.UI;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class SaveSlotListFormatTests
    {
        [Test]
        public void FormatSlotLabel_NamesQuickAutoAndManualSlots()
        {
            Assert.AreEqual("Quicksave", SaveSlotListUI.FormatSlotLabel(GameConstants.SAVE_SLOT_QUICK));
            Assert.AreEqual("Autosave", SaveSlotListUI.FormatSlotLabel(GameConstants.SAVE_SLOT_AUTO));
            Assert.AreEqual("Slot 3", SaveSlotListUI.FormatSlotLabel(GameConstants.SAVE_MANUAL_SLOT_PREFIX + "3"));
        }

        [Test]
        public void FormatDetails_MissingFile_IsEmpty()
        {
            var slot = new SlotInfo { slotId = "slot_1", exists = false };
            Assert.AreEqual("Empty", SaveSlotListUI.FormatDetails(slot));
        }

        [Test]
        public void FormatDetails_InvalidFile_ShowsCorruptedWithError()
        {
            var slot = new SlotInfo { slotId = "slot_1", exists = true, isValid = false, error = SaveFileStore.ERROR_CORRUPTED };
            Assert.AreEqual($"Corrupted — {SaveFileStore.ERROR_CORRUPTED}", SaveSlotListUI.FormatDetails(slot));
        }

        [Test]
        public void FormatDetails_ValidFile_ShowsRegionLevelDateAndPlaytime()
        {
            var savedAt = new DateTime(2026, 10, 8, 12, 30, 0, DateTimeKind.Utc);
            var slot = new SlotInfo
            {
                slotId = "slot_1",
                exists = true,
                isValid = true,
                meta = new SaveMeta
                {
                    slotId = "slot_1",
                    region = "StartingTown",
                    playerLevel = 4,
                    savedAtUtc = savedAt.ToString("o"),
                    playtimeSeconds = 3725f
                }
            };

            string expected = $"StartingTown — Level 4 — {SaveSlotListUI.FormatTimestamp(savedAt.ToString("o"))} — 1:02";
            Assert.AreEqual(expected, SaveSlotListUI.FormatDetails(slot));
        }

        [Test]
        public void FormatTimestamp_ConvertsUtcToLocalTime()
        {
            var savedAt = new DateTime(2026, 10, 8, 12, 30, 0, DateTimeKind.Utc);
            Assert.AreEqual(savedAt.ToLocalTime().ToString("g"), SaveSlotListUI.FormatTimestamp(savedAt.ToString("o")));
        }

        [Test]
        public void FormatTimestamp_Unparseable_ReturnsPlaceholder()
        {
            Assert.AreEqual("?", SaveSlotListUI.FormatTimestamp("not a date"));
        }

        [TestCase(0f, "0:00")]
        [TestCase(59f, "0:00")]
        [TestCase(60f, "0:01")]
        [TestCase(3725f, "1:02")]
        [TestCase(-5f, "0:00")]
        public void FormatPlaytime_FormatsHoursAndMinutes(float seconds, string expected)
        {
            Assert.AreEqual(expected, SaveSlotListUI.FormatPlaytime(seconds));
        }
    }
}
