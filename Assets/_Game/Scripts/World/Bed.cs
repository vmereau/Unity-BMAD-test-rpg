using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// World bed. Behaviour only — the look is the prefab's <c>Model</c> child. <see cref="Interact"/> raises the sleep
    /// request; the player-side <see cref="SleepSystem"/> gates it and opens the sleep panel.
    /// NPC beds (an <c>NpcRoutine</c> Sleep block) also need <see cref="NpcApproach"/> (on the NavMesh, where the NPC
    /// stands before lying down / after getting up) and <see cref="NpcSleepAnchor"/> (where its root lies). While an NPC
    /// sleeps in it (<see cref="Occupant"/>) the player can't use it.
    /// </summary>
    public class Bed : MonoBehaviour, IInteractable
    {
        private const string TAG = "[Bed]";

        [SerializeField] private string _interactPrompt = "Sleep";
        [SerializeField] private string _nameTag = "Bed";
        [SerializeField] private GameEventSO_Void _onSleepRequested;
        [Tooltip("NPC beds only: a point on the NavMesh beside the bed (position + facing) where the NPC lies down from / gets up to.")]
        [SerializeField] private Transform _npcApproach;
        [Tooltip("NPC beds only: where a sleeping NPC's root is placed (position + rotation), tuned to the sleeping clip.")]
        [SerializeField] private Transform _npcSleepAnchor;

        public string InteractPrompt => _interactPrompt;
        public string NameTag => _nameTag;
        public bool CanInteract => Occupant == null;

        public Transform NpcApproach => _npcApproach;
        public Transform NpcSleepAnchor => _npcSleepAnchor;
        /// <summary>True when an NPC can sleep in this bed (both NPC points set).</summary>
        public bool IsNpcBed => _npcApproach != null && _npcSleepAnchor != null;
        /// <summary>The NPC sleeping in it, or null.</summary>
        public GameObject Occupant { get; private set; }

        private void Awake()
        {
            if (_onSleepRequested == null)
                GameLog.Warn(TAG, $"{gameObject.name}: no sleep event assigned — Interact() will do nothing");
        }

        /// <summary>Claims the bed for <paramref name="sleeper"/>; false when another NPC already lies in it.</summary>
        public bool TryOccupy(GameObject sleeper)
        {
            if (sleeper == null) return false;
            if (Occupant != null && Occupant != sleeper) return false;
            Occupant = sleeper;
            return true;
        }

        /// <summary>Frees the bed if <paramref name="sleeper"/> holds it.</summary>
        public void Release(GameObject sleeper)
        {
            if (Occupant == sleeper) Occupant = null;
        }

        public void Interact()
        {
            if (Occupant != null) return;
            _onSleepRequested?.Raise(true);
        }
    }
}
