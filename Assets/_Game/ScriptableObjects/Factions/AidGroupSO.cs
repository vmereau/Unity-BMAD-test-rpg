using UnityEngine;

namespace Game.Factions
{
    /// <summary>
    /// A faction-independent mutual-aid group (spec-faction-mutual-defense): entities sharing one defend each other
    /// whatever their factions say, e.g. a hidden crew whose members are workers on paper. Assigned on the Entity SO.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Aid Group", fileName = "AidGroup_")]
    public class AidGroupSO : ScriptableObject
    {
        [Tooltip("Display name shown in debug tools.")]
        public string displayName;
    }
}
