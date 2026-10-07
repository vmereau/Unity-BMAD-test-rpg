using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode
{
    public class SaveFileStoreTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "SaveFileStoreTests_" + System.Guid.NewGuid().ToString("N"));
            SaveFileStore.FolderOverride = _folder;
        }

        [TearDown]
        public void TearDown()
        {
            SaveFileStore.FolderOverride = null;
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private static SaveData Make(string slotId, string savedAtUtc = "2026-10-07T10:00:00.0000000Z") => new SaveData
        {
            version = GameConstants.SAVE_FORMAT_VERSION,
            meta = new SaveMeta { slotId = slotId, savedAtUtc = savedAtUtc, region = "StartingTown", playerLevel = 2 },
            region = "StartingTown",
            worldFacts = new Dictionary<string, bool>(),
            player = new PlayerSaveData { gold = 9 }
        };

        [Test]
        public void WriteThenRead_ReturnsSameData()
        {
            Assert.IsTrue(SaveFileStore.TryWrite(Make("slot_1"), out var writeError), writeError);
            Assert.IsTrue(SaveFileStore.TryRead("slot_1", out var data, out var readError), readError);
            Assert.AreEqual(9, data.player.gold);
            Assert.AreEqual("2026-10-07T10:00:00.0000000Z", data.meta.savedAtUtc);
        }

        [Test]
        public void Overwrite_ReplacesFileAndLeavesNoTempFile()
        {
            SaveFileStore.TryWrite(Make("slot_1"), out _);
            var second = Make("slot_1");
            second.player.gold = 50;
            Assert.IsTrue(SaveFileStore.TryWrite(second, out _));

            SaveFileStore.TryRead("slot_1", out var data, out _);
            Assert.AreEqual(50, data.player.gold);
            Assert.IsEmpty(Directory.GetFiles(_folder, "*.tmp"));
        }

        [Test]
        public void Read_MissingFile_ReportsNoSave()
        {
            Assert.IsFalse(SaveFileStore.TryRead("slot_2", out _, out var error));
            Assert.AreEqual(SaveFileStore.ERROR_NO_SAVE, error);
        }

        [Test]
        public void Read_InvalidJson_ReportsCorrupted()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(SaveFileStore.PathFor("slot_3"), "{ not json");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("corrupted"));

            Assert.IsFalse(SaveFileStore.TryRead("slot_3", out _, out var error));
            Assert.AreEqual(SaveFileStore.ERROR_CORRUPTED, error);
        }

        [Test]
        public void Read_MissingPlayer_ReportsCorrupted()
        {
            var data = Make("slot_3");
            data.player = null;
            SaveFileStore.TryWrite(data, out _);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("corrupted"));

            Assert.IsFalse(SaveFileStore.TryRead("slot_3", out _, out var error));
            Assert.AreEqual(SaveFileStore.ERROR_CORRUPTED, error);
        }

        [Test]
        public void Read_WrongVersion_ReportsIncompatible()
        {
            var data = Make("slot_4");
            data.version = GameConstants.SAVE_FORMAT_VERSION + 1;
            SaveFileStore.TryWrite(data, out _);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("incompatible"));

            Assert.IsFalse(SaveFileStore.TryRead("slot_4", out _, out var error));
            Assert.AreEqual(SaveFileStore.ERROR_INCOMPATIBLE, error);
        }

        [Test]
        public void ListSlots_ReportsExistingValidAndCorrupted()
        {
            SaveFileStore.TryWrite(Make(GameConstants.SAVE_SLOT_QUICK), out _);
            File.WriteAllText(SaveFileStore.PathFor("slot_1"), "garbage");

            var slots = SaveFileStore.ListSlots();

            Assert.AreEqual(2 + GameConstants.SAVE_MANUAL_SLOT_COUNT, slots.Count);
            var quick = slots.Single(s => s.slotId == GameConstants.SAVE_SLOT_QUICK);
            Assert.IsTrue(quick.exists && quick.isValid);
            Assert.AreEqual("StartingTown", quick.meta.region);
            var bad = slots.Single(s => s.slotId == "slot_1");
            Assert.IsTrue(bad.exists);
            Assert.IsFalse(bad.isValid);
            Assert.AreEqual(SaveFileStore.ERROR_CORRUPTED, bad.error);
            Assert.IsFalse(slots.Single(s => s.slotId == "slot_2").exists);
        }

        [Test]
        public void MostRecentValid_PicksNewestTimestamp()
        {
            SaveFileStore.TryWrite(Make("slot_1", "2026-10-07T10:00:00.0000000Z"), out _);
            SaveFileStore.TryWrite(Make(GameConstants.SAVE_SLOT_AUTO, "2026-10-07T12:00:00.0000000Z"), out _);
            SaveFileStore.TryWrite(Make("slot_5", "2026-10-06T23:00:00.0000000Z"), out _);

            var best = SaveFileStore.MostRecentValid();

            Assert.IsTrue(best.HasValue);
            Assert.AreEqual(GameConstants.SAVE_SLOT_AUTO, best.Value.slotId);
        }

        [Test]
        public void ListSlots_CorruptedFile_LogsNothing()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(SaveFileStore.PathFor("slot_1"), "garbage");
            SaveFileStore.ListSlots();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Read_FileCopiedFromOtherSlot_UsesFileSlotId()
        {
            SaveFileStore.TryWrite(Make("slot_1"), out _);
            File.Copy(SaveFileStore.PathFor("slot_1"), SaveFileStore.PathFor("slot_2"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("slot_2"));

            Assert.IsTrue(SaveFileStore.TryRead("slot_2", out var data, out _));
            Assert.AreEqual("slot_2", data.meta.slotId);
        }

        [Test]
        public void MostRecentValid_NoSaves_ReturnsNull()
        {
            Assert.IsFalse(SaveFileStore.MostRecentValid().HasValue);
        }

        [Test]
        public void Delete_RemovesFile()
        {
            SaveFileStore.TryWrite(Make("slot_1"), out _);
            Assert.IsTrue(SaveFileStore.TryDelete("slot_1", out _));
            Assert.IsFalse(File.Exists(SaveFileStore.PathFor("slot_1")));
        }
    }
}
