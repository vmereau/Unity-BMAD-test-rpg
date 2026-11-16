using UnityEngine;

namespace Game.Core
{
    /// <summary>What the editor's Play button starts (picked on the main toolbar, editor only).</summary>
    public enum PlayScenario
    {
        /// <summary>Today's behaviour: the open scenes play, the scene's own dev settings apply.</summary>
        OpenScenes = 0,
        /// <summary>Start in MainMenu; the arrival runs normally after New Game.</summary>
        MainMenu = 1,
        /// <summary>Start in Core as a Wretch, the arrival plays from the gate.</summary>
        Arrival = 2,
        /// <summary>Start in Core as a Wretch, the arrival is skipped (player by the hut).</summary>
        AfterArrival = 3,
    }

    /// <summary>How a play scenario overrides <c>ArrivalSequence</c>'s serialized dev start.</summary>
    public enum ArrivalStartOverride
    {
        /// <summary>No override: the scene's dev start applies.</summary>
        None,
        /// <summary>Play the whole arrival, whatever the scene says.</summary>
        Play,
        /// <summary>Skip the whole arrival.</summary>
        SkipAll,
    }

    /// <summary>
    /// Editor play scenarios: the editor toolbar (<c>Game.Editor.PlayScenarioToolbar</c>) stores the choice in
    /// EditorPrefs and sets the play-mode start scene; this class hands the choice to the running game (origin,
    /// arrival start). EditorPrefs instead of a static because domain reloads and <see cref="GameSession"/>'s
    /// SubsystemRegistration reset would wipe a static. Builds always see <see cref="PlayScenario.OpenScenes"/>.
    /// </summary>
    public static class PlayScenarios
    {
        private const string TAG = "[PlayScenario]";

        /// <summary>Origin the arrival scenarios play as (<c>OriginSO.originId</c> of <c>Origin_Wretch</c>).</summary>
        public const string WretchOriginId = "Wretch";

        // Snapshot taken once per Play in ApplyOnPlay (rewritten every play, so safe with domain reload disabled).
        private static PlayScenario s_active;

        /// <summary>
        /// EditorPrefs key. EditorPrefs are machine-wide, so the project's Assets path is part of the key: two checkouts
        /// (clones, worktrees) keep their own choice.
        /// </summary>
        public static string PrefsKey => "UnityBmadTestRpg.PlayScenario:" + Application.dataPath;

        /// <summary>The saved scenario, read in the editor only. <see cref="PlayScenario.OpenScenes"/> in builds.</summary>
        public static PlayScenario Saved
        {
            get
            {
#if UNITY_EDITOR
                int value = UnityEditor.EditorPrefs.GetInt(PrefsKey, (int)PlayScenario.OpenScenes);
                return value >= (int)PlayScenario.OpenScenes && value <= (int)PlayScenario.AfterArrival
                    ? (PlayScenario)value
                    : PlayScenario.OpenScenes;
#else
                return PlayScenario.OpenScenes;
#endif
            }
        }

        /// <summary>
        /// The scenario of the running game, fixed when Play started (a later toolbar change waits for the next Play).
        /// OpenScenes outside Play, in builds, and when the scenario's start scene couldn't be applied.
        /// </summary>
        public static PlayScenario Active => Application.isPlaying ? s_active : PlayScenario.OpenScenes;

        /// <summary>The arrival dev start a scenario forces.</summary>
        public static ArrivalStartOverride ArrivalOverrideFor(PlayScenario scenario)
        {
            switch (scenario)
            {
                case PlayScenario.MainMenu:
                case PlayScenario.Arrival:
                    return ArrivalStartOverride.Play;
                case PlayScenario.AfterArrival:
                    return ArrivalStartOverride.SkipAll;
                default:
                    return ArrivalStartOverride.None;
            }
        }

        /// <summary>The origin a scenario forces on the new-game boot (null = none forced).</summary>
        public static string OriginFor(PlayScenario scenario) =>
            scenario == PlayScenario.Arrival || scenario == PlayScenario.AfterArrival ? WretchOriginId : null;

        // After GameSession.Reset (SubsystemRegistration), before the first scene's Awake (SceneLoader reads it there).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnPlay()
        {
            s_active = PlayScenario.OpenScenes;
#if UNITY_EDITOR
            PlayScenario scenario = Saved;
            if (scenario == PlayScenario.OpenScenes) return;
            // The toolbar clears the start scene when the scenario's scene is missing: then the open scenes play, untouched.
            if (UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene == null)
            {
                GameLog.Warn(TAG, $"Play scenario {scenario} has no start scene — playing the open scenes without it");
                return;
            }
            s_active = scenario;
            string origin = OriginFor(scenario);
            if (origin != null) GameSession.SetOrigin(origin);
            GameLog.Info(TAG, $"Play scenario {scenario}" + (origin != null ? $" (origin {origin})" : ""));
#endif
        }
    }
}
