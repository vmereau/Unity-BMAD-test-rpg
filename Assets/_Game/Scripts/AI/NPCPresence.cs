using Game.Core;
using Game.NPC;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    public class NPCPresence : MonoBehaviour, IInteractable
    {
        private const string TAG = "[NPC]";

        [SerializeField] private NPCDataSO _data;

        public string InteractPrompt => _data != null ? _data.npcName : "NPC";

        private void Awake()
        {
            if (_data == null)
            {
                GameLog.Error(TAG, $"NPCPresence on {gameObject.name} has no NPCDataSO assigned");
                enabled = false;
            }
        }

        public void Interact()
        {
            if (_data == null) return;
            GameLog.Info(TAG, $"{_data.npcName} is busy."); // Placeholder — Epic 6 adds dialogue
        }
    }
}
