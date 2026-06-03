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

        // Dead with loot → "Loot"; alive → "Talk"; dead-and-empty → no prompt.
        public override string InteractPrompt => IsDead ? (IsLootable ? "Loot" : string.Empty) : "Talk";

        // Interactable when a non-empty corpse (loot) OR alive & out of combat (dialogue).
        // In combat & alive, or an emptied corpse → no interaction.
        public override bool CanInteract => IsLootable || IsAliveAndOutOfCombat;

        public override void Interact()
        {
            if (Data == null) return;

            if (IsDead)
            {
                base.Interact(); // loot the corpse via the shared container pipeline
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
