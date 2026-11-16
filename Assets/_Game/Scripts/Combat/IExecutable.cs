using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Something the player can finish off with the execute stomp (spec-execute-unconscious-npc). Implemented by
    /// <c>EntityHealth</c> — keeps <c>Game.Combat</c> free of AI types.
    /// </summary>
    public interface IExecutable
    {
        /// <summary>True while the target lies unconscious (not getting up, not dead) and its data allows it.</summary>
        bool CanBeExecuted { get; }

        /// <summary>World point the stomping foot must land on (the body's chest).</summary>
        Vector3 ExecutePoint { get; }

        /// <summary>Kills the target if it can still be executed. Returns false when the kill was refused.</summary>
        bool Execute(GameObject executor);
    }
}
