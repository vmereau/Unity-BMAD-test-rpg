using Game.Core;
using Game.Inventory;
using Game.NPC;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    public class NPCPresence : MonoBehaviour, IInteractable
    {
        private const string TAG = "[NPC]";

        
        [SerializeField] private PersistentID _persistentID;
        private NPCEntity _data => (NPCEntity) _persistentID.Entity;
        [SerializeField] private GameEventSO_NPCDialogueRequest _onDialogueRequested;

        public string InteractPrompt => "Talk";
        
        public string NameTag => _data.entityName;

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
            var graphComponent = GetComponent<NPCDialogueGraphComponent>(); // may be null — handled by DialogueSystem
            var invComponent = GetComponent<InventorySystem>(); // may be null — handled by Shop system
            
            _onDialogueRequested.Raise(new NPCDialogueRequestData
            {
                npcName = _data.entityName,
                memories = memComponent,
                graph = graphComponent,
                npcInventory = invComponent
            });
        }
    }
}
