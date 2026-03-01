using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Project-wide logging wrapper. Info and Warn are stripped in Release builds.
    /// Error always writes to console and appends to game_log.txt in persistentDataPath.
    /// All game scripts must use this class — never call Debug.Log directly.
    /// </summary>
    public static class GameLog
    {
        public static void Info(string tag, string msg)
        {
            if (Debug.isDebugBuild)
                Debug.Log($"{tag} {msg}");
        }

        public static void Warn(string tag, string msg)
        {
            if (Debug.isDebugBuild)
                Debug.LogWarning($"{tag} {msg}");
        }

        public static void Error(string tag, string msg)
        {
            Debug.LogError($"{tag} {msg}");
            // TODO (Epic 8): also append to Application.persistentDataPath/game_log.txt
        }
    }
}
