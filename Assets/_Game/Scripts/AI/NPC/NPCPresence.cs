using Game.Core;
using Game.Economy;
using Game.Factions;
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
        [Tooltip("The player's faction — an alive NPC hostile to it offers no dialogue (Faction_Player).")]
        [SerializeField] private FactionSO _playerFaction;

        private FactionMember _factionMember;

        /// <summary>True when this NPC's faction and the player's are hostile (either direction).</summary>
        private bool IsHostileToPlayer =>
            _factionMember != null && FactionSO.AreHostile(_factionMember.Faction, _playerFaction);

        // Dead with loot → "Loot"; alive & hostile → no prompt; alive → "Talk"; dead-and-empty → no prompt.
        public override string InteractPrompt =>
            IsDead ? (IsLootable ? "Loot" : string.Empty) : (IsHostileToPlayer ? string.Empty : "Talk");

        // Interactable when a non-empty corpse (loot) OR alive, out of combat and not hostile (dialogue).
        // In combat, hostile & alive, or an emptied corpse → no interaction.
        public override bool CanInteract => IsLootable || (IsAliveAndOutOfCombat && !IsHostileToPlayer);

        protected override void Awake()
        {
            base.Awake();
            _factionMember = GetComponent<FactionMember>();
            if (_playerFaction == null)
                GameLog.Warn(TAG, $"{gameObject.name}: _playerFaction not assigned — hostile NPCs will offer dialogue");
        }

        public override void Interact()
        {
            if (Data == null) return;

            if (IsDead)
            {
                base.Interact(); // loot the corpse via the shared container pipeline
                return;
            }
            if (IsHostileToPlayer)
            {
                GameLog.Info(TAG, $"{gameObject.name} is hostile to the player — dialogue interaction blocked");
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
