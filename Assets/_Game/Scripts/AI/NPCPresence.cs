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
        [SerializeField] private GameEventSO_NPCDialogueRequest _onDialogueRequested;

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
            if (_onDialogueRequested == null)
            {
                GameLog.Warn(TAG, $"No dialogue event assigned on {gameObject.name} — cannot open dialogue");
                return;
            }
            var memComponent = GetComponent<NPCMemoryComponent>(); // may be null — handled by DialogueSystem
            _onDialogueRequested.Raise(new NPCDialogueRequestData
            {
                npcName = _data.npcName,
                memories = memComponent
            });
        }
    }
}
