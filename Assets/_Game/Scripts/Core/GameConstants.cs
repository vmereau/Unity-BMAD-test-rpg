namespace Game.Core
{
    /// <summary>
    /// Project-wide compile-time constants.
    /// Tunable gameplay values belong in config SOs, not here.
    /// This is for structural constants that never change based on design.
    /// </summary>
    public static class GameConstants
    {
        // --- Save / Persistence ---
        public const string SAVE_FILE_NAME = "savegame.json";
        public const string CRASH_LOG_FILE_NAME = "crash_log.txt";

        // --- Equipment ---
        public const int MAX_EQUIPMENT_RING_SLOTS = 2;
        public const int MAX_EQUIPMENT_SLOTS = 9; // head, chest, legs, boots, gloves, weapon, shield, ring×2, necklace

        // --- Progression ---
        public const int MAX_CHARACTER_LEVEL = 50;

        // --- World ---
        public const string CORE_SCENE_NAME = "Core";
        public const string STARTING_TOWN_SCENE_NAME = "StartingTown";
        public const string WILDERNESS_SCENE_NAME = "Wilderness";
        public const string DUNGEON_SCENE_NAME = "Dungeon";
        public const string MAIN_MENU_SCENE_NAME = "MainMenu";
    }
}
