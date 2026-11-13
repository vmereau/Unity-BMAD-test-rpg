using Game.Core;
using Game.Dialogue;
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
        [Tooltip("Optional: the player can't talk to this NPC until this fact is true (e.g. Tam before the arrival event). " +
                 "Loot and NPC-forced topics are unaffected.")]
        [SerializeField] private WorldFact _availableAfter;

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
            IsDown ? (IsLootable ? "Loot" : string.Empty) : (IsHostileToPlayer || !IsAvailable ? string.Empty : "Talk");

        // Interactable when a non-empty corpse (loot) OR alive, out of combat and not hostile (dialogue).
        // In combat, hostile & alive, or an emptied corpse → no interaction.
        public override bool CanInteract => IsLootable || (IsAliveAndOutOfCombat && !IsHostileToPlayer && IsAvailable);

        // No gate, or its fact is set. No WorldStateManager (scene played alone) → available.
        private bool IsAvailable
        {
            get
            {
                if (_availableAfter == null) return true;
                WorldStateManager wsm = WorldStateManager.Instance;
                return wsm == null || wsm.GetFact(_availableAfter);
            }
        }

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
            if (!IsAvailable)
            {
                GameLog.Info(TAG, $"{gameObject.name} is not available until {_availableAfter.name} — dialogue blocked");
                return;
            }
            RaiseDialogueRequest(null);
        }

        /// <summary>
        /// NPC-initiated topic (e.g. a toll collector stopping the player): opens the dialogue straight into
        /// <paramref name="startNode"/>, with this NPC's memories / graph / inventory as context. False when it can't be
        /// raised (no data, down, no channel). The caller checks the dialogue actually opened (cursor unlocked).
        /// <paramref name="thenTopics"/>: the chain's end shows this NPC's normal topic list (the player leaves with
        /// Farewell) instead of closing the dialogue.
        /// </summary>
        public bool RequestForcedTopic(StartDialogueNode startNode, bool thenTopics = false)
        {
            if (Data == null || startNode == null || IsDown) return false;
            return RaiseDialogueRequest(startNode, thenTopics);
        }

        private bool RaiseDialogueRequest(StartDialogueNode forcedStartNode, bool thenTopics = false)
        {
            if (_onDialogueRequested == null)
            {
                GameLog.Warn(TAG, $"No dialogue event assigned on {gameObject.name} — cannot open dialogue");
                return false;
            }
            var memComponent   = GetComponent<NPCMemoryComponent>();
            var graphComponent = GetComponent<NPCDialogueGraphComponent>();
            var invComponent   = GetComponent<InventorySystem>();
            var goldComponent  = GetComponent<GoldSystem>();

            _onDialogueRequested.Raise(new NPCDialogueRequestData
            {
                npcName         = Data.entityName,
                memories        = memComponent,
                graph           = graphComponent,
                npcInventory    = invComponent,
                npcGoldSystem   = goldComponent,
                forcedStartNode = forcedStartNode,
                forcedTopicThenTopics = thenTopics,
            });
            return true;
        }
    }
}
