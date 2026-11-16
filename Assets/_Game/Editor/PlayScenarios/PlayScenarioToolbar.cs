using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Main-toolbar dropdown (next to Play) + <c>Tools/Play Scenario</c> menu picking what the Play button starts
    /// (spec-play-scenarios). The choice is saved in EditorPrefs (<see cref="PlayScenarios.PrefsKey"/>) and turned into
    /// <see cref="EditorSceneManager.playModeStartScene"/>, which isn't persisted, so it's reapplied on every editor load.
    /// The running game reads the same pref through <see cref="PlayScenarios"/> (origin, arrival start).
    /// </summary>
    [InitializeOnLoad]
    public static class PlayScenarioToolbar
    {
        private const string TAG = "[PlayScenario]";
        private const string ToolbarPath = "Game/Play Scenario";
        private const string MenuRoot = "Tools/Play Scenario/";
        private const string MainMenuScenePath = "Assets/_Game/Scenes/MainMenu.unity";
        private const string CoreScenePath = "Assets/_Game/Scenes/Core.unity";

        private const string MenuOpenScenes = MenuRoot + "Open Scenes";
        private const string MenuMainMenu = MenuRoot + "Main Menu";
        private const string MenuArrival = MenuRoot + "Arrival (Wretch)";
        private const string MenuAfterArrival = MenuRoot + "After Arrival (Wretch)";

        private static readonly PlayScenario[] s_scenarios =
            { PlayScenario.OpenScenes, PlayScenario.MainMenu, PlayScenario.Arrival, PlayScenario.AfterArrival };

        static PlayScenarioToolbar()
        {
            // delayCall: the AssetDatabase may not be ready inside an [InitializeOnLoad] constructor.
            EditorApplication.delayCall += () => ApplyStartScene(PlayScenarios.Saved);
            EditorApplication.playModeStateChanged += _ => MainToolbar.Refresh(ToolbarPath);
        }

        public static string Label(PlayScenario scenario)
        {
            switch (scenario)
            {
                case PlayScenario.MainMenu: return "Main Menu";
                case PlayScenario.Arrival: return "Arrival (Wretch)";
                case PlayScenario.AfterArrival: return "After Arrival (Wretch)";
                default: return "Open Scenes";
            }
        }

        [MainToolbarElement(ToolbarPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
        private static MainToolbarElement CreateDropdown()
        {
            var content = new MainToolbarContent(
                "▶ " + Label(PlayScenarios.Saved),
                "Play scenario: what the Play button starts (editor only)");
            return new MainToolbarDropdown(content, ShowMenu) { enabled = !EditorApplication.isPlayingOrWillChangePlaymode };
        }

        private static void ShowMenu(Rect rect)
        {
            var menu = new GenericMenu();
            PlayScenario current = PlayScenarios.Saved;
            foreach (PlayScenario scenario in s_scenarios)
            {
                PlayScenario captured = scenario;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    menu.AddDisabledItem(new GUIContent(Label(scenario)), scenario == current);
                else
                    menu.AddItem(new GUIContent(Label(scenario)), scenario == current, () => Select(captured));
            }
            menu.DropDown(rect);
        }

        public static void Select(PlayScenario scenario)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorPrefs.SetInt(PlayScenarios.PrefsKey, (int)scenario);
            ApplyStartScene(scenario);
            MainToolbar.Refresh(ToolbarPath);
        }

        private static void ApplyStartScene(PlayScenario scenario)
        {
            string path = scenario switch
            {
                PlayScenario.MainMenu => MainMenuScenePath,
                PlayScenario.Arrival or PlayScenario.AfterArrival => CoreScenePath,
                _ => null,
            };
            if (path == null)
            {
                // Open Scenes: only clear a start scene we set, never one another tool chose.
                string current = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene);
                if (current == MainMenuScenePath || current == CoreScenePath)
                    EditorSceneManager.playModeStartScene = null;
                return;
            }
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (scene == null)
                GameLog.Warn(TAG, $"Scene '{path}' not found — Play starts the open scenes, without the scenario");
            EditorSceneManager.playModeStartScene = scene;
        }

        [MenuItem(MenuOpenScenes, priority = 0)] private static void PickOpenScenes() => Select(PlayScenario.OpenScenes);
        [MenuItem(MenuMainMenu, priority = 1)] private static void PickMainMenu() => Select(PlayScenario.MainMenu);
        [MenuItem(MenuArrival, priority = 2)] private static void PickArrival() => Select(PlayScenario.Arrival);
        [MenuItem(MenuAfterArrival, priority = 3)] private static void PickAfterArrival() => Select(PlayScenario.AfterArrival);

        [MenuItem(MenuOpenScenes, true)] private static bool CanPickOpenScenes() => Validate(MenuOpenScenes, PlayScenario.OpenScenes);
        [MenuItem(MenuMainMenu, true)] private static bool CanPickMainMenu() => Validate(MenuMainMenu, PlayScenario.MainMenu);
        [MenuItem(MenuArrival, true)] private static bool CanPickArrival() => Validate(MenuArrival, PlayScenario.Arrival);
        [MenuItem(MenuAfterArrival, true)] private static bool CanPickAfterArrival() => Validate(MenuAfterArrival, PlayScenario.AfterArrival);

        private static bool Validate(string menuPath, PlayScenario scenario)
        {
            Menu.SetChecked(menuPath, PlayScenarios.Saved == scenario);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
