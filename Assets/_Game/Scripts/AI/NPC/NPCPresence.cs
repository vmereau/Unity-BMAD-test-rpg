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

        private FactionMember _factionMember;
        private static bool s_noPlayerWarned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => s_noPlayerWarned = false;

        /// <summary>
        /// True when this NPC's faction and the player's are hostile (either direction). The player's faction is
        /// read live from <see cref="TargetRegistry.Player"/> — no player gone → not hostile.
        /// </summary>
        private bool IsHostileToPlayer
        {
            get
            {
                FactionMember player = TargetRegistry.Player;
                if (player == null)
                {
                    if (!s_noPlayerWarned)
                    {
                        s_noPlayerWarned = true;
                        GameLog.Warn(TAG, "No player FactionMember registered (TargetRegistry.Player) — hostile NPCs will offer dialogue");
                    }
                    return false;
                }
                return _factionMember != null && FactionSO.AreHostile(_factionMember.Faction, player.Faction);
            }
        }

        // Dead / unconscious with loot → "Loot"; empty → no prompt; alive & hostile → no prompt; alive → "Talk"
        // (CanInteract still hides "Talk" while in combat or staggered).
        public override string InteractPrompt =>
            IsDown ? (IsLootable ? "Loot" : string.Empty) : (IsHostileToPlayer ? string.Empty : "Talk");

        // Interactable when a non-empty corpse (loot) OR alive, out of combat and not hostile (dialogue).
        // In combat, hostile & alive, or an emptied corpse → no interaction.
        public override bool CanInteract => IsLootable || (IsAliveAndOutOfCombat && !IsHostileToPlayer);

        protected override void Awake()
        {
            base.Awake();
            _factionMember = GetComponent<FactionMember>();
        }

        public override void Interact()
        {
            if (Data == null) return;

            if (IsDown)
            {
                base.Interact(); // loot the corpse / unconscious body via the shared container pipeline
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
