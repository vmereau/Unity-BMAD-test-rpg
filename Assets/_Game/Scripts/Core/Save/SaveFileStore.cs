using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Core
{
    /// <summary>One row of the slot list: the slot's file state and, when valid, its header.</summary>
    public struct SlotInfo
    {
        public string slotId;
        public bool exists;
        public bool isValid;
        public SaveMeta meta;
        public string error;
    }

    /// <summary>
    /// Slot files under <c>persistentDataPath/Saves/</c>: <c>save_{slotId}.json</c>. Writes go to a
    /// <c>.tmp</c> file first and then replace the target, so a crash mid-write never corrupts an existing
    /// save. Synchronous on purpose (files are a few KB). Never throws — every failure is logged and
    /// reported through the <c>error</c> out parameter.
    /// </summary>
    public static class SaveFileStore
    {
        private const string TAG = "[Save]";
        private const string TEMP_SUFFIX = ".tmp";

        public const string ERROR_NO_SAVE = "No save";
        public const string ERROR_CORRUPTED = "Save file is corrupted";
        public const string ERROR_INCOMPATIBLE = "Save was made with an incompatible version";

        /// <summary>Test hook: when set, replaces the persistent-data save folder.</summary>
        internal static string FolderOverride;

        public static string FolderPath => FolderOverride ??
            Path.Combine(Application.persistentDataPath, GameConstants.SAVE_FOLDER_NAME);

        public static string PathFor(string slotId) =>
            Path.Combine(FolderPath, GameConstants.SAVE_FILE_PREFIX + slotId + GameConstants.SAVE_FILE_EXTENSION);

        /// <summary>Quicksave, autosave, then manual slots 1..N — the slot-list order.</summary>
        public static IEnumerable<string> AllSlotIds()
        {
            yield return GameConstants.SAVE_SLOT_QUICK;
            yield return GameConstants.SAVE_SLOT_AUTO;
            for (int i = 1; i <= GameConstants.SAVE_MANUAL_SLOT_COUNT; i++)
                yield return GameConstants.SAVE_MANUAL_SLOT_PREFIX + i;
        }

        public static bool TryWrite(SaveData data, out string error)
        {
            error = null;
            if (data == null || data.meta == null || string.IsNullOrEmpty(data.meta.slotId))
            {
                error = "Nothing to save (no data or slot id)";
                GameLog.Error(TAG, error);
                return false;
            }

            string path = PathFor(data.meta.slotId);
            string tmp = path + TEMP_SUFFIX;
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                Directory.CreateDirectory(FolderPath);
                File.WriteAllText(tmp, json);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                GameLog.Info(TAG, $"Saved slot '{data.meta.slotId}' → {path}");
                return true;
            }
            catch (Exception e)
            {
                error = $"Could not write save: {e.Message}";
                GameLog.Error(TAG, $"{error} ({path})");
                TryDeleteFile(tmp);
                return false;
            }
        }

        public static bool TryRead(string slotId, out SaveData data, out string error) =>
            TryRead(slotId, out data, out error, logErrors: true);

        // logErrors: false for listings — the error is shown in the slot row instead, and listing runs every
        // time the Load panel / death screen opens, so logging there would repeat the same errors.
        private static bool TryRead(string slotId, out SaveData data, out string error, bool logErrors)
        {
            data = null;
            error = null;
            string path = PathFor(slotId);

            string json;
            try
            {
                if (!File.Exists(path))
                {
                    error = ERROR_NO_SAVE;
                    return false;
                }
                json = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                error = $"Could not read save: {e.Message}";
                if (logErrors) GameLog.Error(TAG, $"{error} ({path})");
                return false;
            }

            try
            {
                // Read the version before mapping to SaveData so a format change reports "incompatible",
                // not "corrupted".
                // DateParseHandling.None keeps savedAtUtc a verbatim string (default parsing reformats it).
                JObject root;
                using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                    root = JObject.Load(reader);
                var versionToken = root["version"];
                if (versionToken == null || versionToken.Type != JTokenType.Integer)
                {
                    error = ERROR_CORRUPTED;
                    if (logErrors) GameLog.Error(TAG, $"{error}: no version ({path})");
                    return false;
                }
                if (versionToken.Value<int>() != GameConstants.SAVE_FORMAT_VERSION)
                {
                    error = ERROR_INCOMPATIBLE;
                    if (logErrors) GameLog.Error(TAG, $"{error}: v{versionToken.Value<int>()}, expected v{GameConstants.SAVE_FORMAT_VERSION} ({path})");
                    return false;
                }

                var parsed = root.ToObject<SaveData>();
                if (parsed == null || parsed.meta == null || parsed.player == null || parsed.worldFacts == null)
                {
                    error = ERROR_CORRUPTED;
                    if (logErrors) GameLog.Error(TAG, $"{error}: missing meta / player / worldFacts ({path})");
                    return false;
                }

                if (parsed.meta.slotId != slotId)
                {
                    // Copied / renamed file: the file name is the slot — never let a later save target another slot.
                    GameLog.Warn(TAG, $"Save in slot '{slotId}' says slot '{parsed.meta.slotId}' — using '{slotId}'.");
                    parsed.meta.slotId = slotId;
                }

                data = parsed;
                return true;
            }
            catch (Exception e)
            {
                error = ERROR_CORRUPTED;
                if (logErrors) GameLog.Error(TAG, $"{error}: {e.Message} ({path})");
                return false;
            }
        }

        public static List<SlotInfo> ListSlots()
        {
            var result = new List<SlotInfo>();
            foreach (var slotId in AllSlotIds())
            {
                bool exists = FileExists(PathFor(slotId));
                var info = new SlotInfo { slotId = slotId, exists = exists };
                if (exists)
                {
                    info.isValid = TryRead(slotId, out var data, out info.error, logErrors: false);
                    if (info.isValid) info.meta = data.meta;
                }
                result.Add(info);
            }
            return result;
        }

        /// <summary>Newest valid save across all slots by <see cref="SaveMeta.savedAtUtc"/>, or null.</summary>
        public static SlotInfo? MostRecentValid()
        {
            SlotInfo? best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (var info in ListSlots())
            {
                if (!info.isValid) continue;
                if (!DateTime.TryParse(info.meta.savedAtUtc, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var time))
                    continue;
                time = time.ToUniversalTime();
                if (best != null && time <= bestTime) continue;
                best = info;
                bestTime = time;
            }
            return best;
        }

        public static bool TryDelete(string slotId, out string error)
        {
            error = null;
            string path = PathFor(slotId);
            try
            {
                if (File.Exists(path)) File.Delete(path);
                GameLog.Info(TAG, $"Deleted slot '{slotId}'");
                return true;
            }
            catch (Exception e)
            {
                error = $"Could not delete save: {e.Message}";
                GameLog.Error(TAG, $"{error} ({path})");
                return false;
            }
        }

        private static bool FileExists(string path)
        {
            try { return File.Exists(path); }
            catch (Exception e)
            {
                GameLog.Error(TAG, $"Could not check {path}: {e.Message}");
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) { GameLog.Error(TAG, $"Could not remove temp file {path}: {e.Message}"); }
        }
    }
}
