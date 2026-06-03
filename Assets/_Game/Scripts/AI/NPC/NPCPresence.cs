using Game.Core;
using Game.Economy;
using Game.Inventory;
using Game.NPC;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    public class NPCPresence : EntityPresence
    {
        private const string TAG = "[NPC]";

        [SerializeField] private GameEventSO_NPCDialogueRequest _onDialogueRequested;

        public override string InteractPrompt => "Talk";

        // NPCs are interactable while alive and not in combat (dead / in-combat → no prompt).
        public override bool CanInteract => IsAliveAndOutOfCombat;

        public override void Interact()
        {
            if (Data == null) return;
            if (_entityHealth != null && _entityHealth.IsDead)
            {
                // TODO: replace with a loot-corpse interaction unlock once the looting story lands.
                GameLog.Info(TAG, $"{gameObject.name} is dead — dialogue interaction blocked");
                return;
            }
            if (_combatState != null && _combatState.IsInCombat)
            {
                GameLog.Info(TAG, $"{gameObject.name} is in combat — dialogue interaction blocked");
                return;
            }
            if (_onDialogueRequested == null)
            {
                GameLog.Warn(TAG, $"No dialogue event assigned on {gameObject.name} — cannot open dialogue");
                return;
            }
            var memComponent   = GetComponent<NPCMemoryComponent>();
            var graphComponent = GetComponent<NPCDialogueGraphComponent>();
            var invComponent   = GetComponent<InventorySystem>();
            var goldComponent  = GetComponent<GoldSystem>();

            _onDialogueRequested.Raise(new NPCDialogueRequestData
            {
                npcName       = Data.entityName,
                memories      = memComponent,
                graph         = graphComponent,
                npcInventory  = invComponent,
                npcGoldSystem = goldComponent
            });
        }
    }
}
