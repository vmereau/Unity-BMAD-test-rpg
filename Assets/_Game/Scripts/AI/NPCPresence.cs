using Game.Core;
using Game.Economy;
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

        private EntityHealth _entityHealth;

        public string InteractPrompt => "Talk";

        public string NameTag => _data.entityName;

        private void Awake()
        {
            if (_data == null)
            {
                GameLog.Error(TAG, $"NPCPresence on {gameObject.name} has no NPCDataSO assigned");
                enabled = false;
                return;
            }
            _entityHealth = GetComponent<EntityHealth>();
        }

        public void Interact()
        {
            if (_data == null) return;
            if (_entityHealth != null && _entityHealth.IsDead)
            {
                // TODO: replace this early-return with a loot-corpse interaction unlock
                //       once the looting-system story (sprint backlog 7-6) lands. The
                //       corpse intentionally remains a valid IInteractable target so
                //       the future loot UX can plug in without re-introducing detection
                //       logic. Crosshair highlight + NameTag are unchanged on purpose.
                GameLog.Info(TAG, $"{gameObject.name} is dead — dialogue interaction blocked");
                return;
            }
            if (_onDialogueRequested == null)
            {
                GameLog.Warn(TAG, $"No dialogue event assigned on {gameObject.name} — cannot open dialogue");
                return;
            }
            var memComponent = GetComponent<NPCMemoryComponent>(); // may be null — handled by DialogueSystem
            var graphComponent = GetComponent<NPCDialogueGraphComponent>(); // may be null — handled by DialogueSystem
            var invComponent = GetComponent<InventorySystem>(); // may be null — handled by Shop system
            var goldComponent = GetComponent<GoldSystem>(); // may be null — handled by NPCTradeUI

            _onDialogueRequested.Raise(new NPCDialogueRequestData
            {
                npcName = _data.entityName,
                memories = memComponent,
                graph = graphComponent,
                npcInventory = invComponent,
                npcGoldSystem = goldComponent
            });
        }
    }
}
