using System.Collections.Generic;
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
        [Tooltip("Raised by RequestDialogueClose (OnDialogueCloseRequested): closes the dialogue this NPC opened.")]
        [SerializeField] private GameEventSO_DialogueCloseRequest _onDialogueCloseRequested;
        [Tooltip("Optional: the player can't talk to this NPC until this fact is true (e.g. Tam before the arrival event). " +
                 "Loot and NPC-forced topics are unaffected.")]
        [SerializeField] private WorldFact _availableAfter;
        [Tooltip("Things this NPC brings up once on the player's next \"Talk\" (first pending wins), before its topic list.")]
        [SerializeField] private List<NpcTalkOpener> _talkOpeners = new List<NpcTalkOpener>();

        private FactionMember _factionMember;
        private EntityBrain _brain; // same-GO, optional: no opener while it chases / scolds a thief
        private bool _closeEventWarned;
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
            _brain = GetComponent<EntityBrain>();
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
            if (TryPlayTalkOpener()) return;
            RaiseDialogueRequest(null);
        }

        // First pending opener: opens its topic, then the topic list. Played is set only if the dialogue really opened
        // (DialogueSystem unlocks the cursor synchronously; it ignores a request while another dialogue is open).
        // No WorldStateManager → none. Not while this NPC chases / scolds a thief: its scold comes first.
        private bool TryPlayTalkOpener()
        {
            WorldStateManager wsm = WorldStateManager.Instance;
            if (wsm == null || _talkOpeners.Count == 0) return false;
            bool chasingThief = _brain != null && _brain.IsChasingThief;
            for (int i = 0; i < _talkOpeners.Count; i++)
            {
                NpcTalkOpener opener = _talkOpeners[i];
                if (opener == null) continue;
                if (!opener.IsComplete)
                {
                    GameLog.Warn(TAG, $"{gameObject.name}: talk opener {i} needs a condition, a topic and a played fact — skipped");
                    continue;
                }
                if (!TalkOpenerRules.IsPending(true, wsm.GetFact(opener.condition), wsm.GetFact(opener.played), chasingThief))
                    continue;
                if (!RaiseDialogueRequest(opener.topic, thenTopics: true)) return false;
                if (CursorManager.IsLocked)
                {
                    GameLog.Info(TAG, $"{gameObject.name}: opener '{opener.topic.name}' not shown (dialogue didn't open) — kept for later");
                    return false; // let the normal topic list try
                }
                wsm.SetFact(opener.played, true);
                GameLog.Info(TAG, $"{gameObject.name} opens with '{opener.topic.name}' ({opener.condition.name})");
                return true;
            }
            return false;
        }

        /// <summary>
        /// This NPC saw the player steal its goods (its chase over that theft just ended, however it ended): every
        /// <see cref="NpcTalkOpener.cancelWhenWitnessed"/> opener whose condition is already true is marked played, so it
        /// never brings the theft up later as if it didn't know who did it.
        /// </summary>
        public void MarkWitnessedOpenersPlayed()
        {
            WorldStateManager wsm = WorldStateManager.Instance;
            if (wsm == null) return;
            foreach (NpcTalkOpener opener in _talkOpeners)
            {
                if (opener == null || !opener.IsComplete) continue;
                if (!TalkOpenerRules.CancelsOnWitness(true, opener.cancelWhenWitnessed, wsm.GetFact(opener.condition),
                        wsm.GetFact(opener.played))) continue;
                wsm.SetFact(opener.played, true);
                GameLog.Info(TAG, $"{gameObject.name}: saw the thief — opener '{opener.topic.name}' cancelled");
            }
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

        /// <summary>
        /// Like <see cref="RequestForcedTopic(StartDialogueNode, bool)"/>, but shows <paramref name="openingLine"/> first
        /// (e.g. a random scold bark), then the topic's chain. The chain's end closes the dialogue.
        /// </summary>
        public bool RequestForcedTopic(StartDialogueNode startNode, string openingLine)
        {
            if (Data == null || startNode == null || IsDown) return false;
            return RaiseDialogueRequest(startNode, false, openingLine);
        }

        /// <summary>
        /// Closes the dialogue this NPC opened (forced topic or the player's "Talk"), e.g. an encounter aborted mid-talk or
        /// a scripted sequence interrupted. No effect when no dialogue is open or another NPC's is.
        /// </summary>
        public void RequestDialogueClose()
        {
            if (_onDialogueCloseRequested == null)
            {
                if (!_closeEventWarned)
                {
                    _closeEventWarned = true;
                    // May run on a destroyed NPC during a region unload, where name / gameObject throw.
                    string who = this != null ? name : "a destroyed NPC";
                    GameLog.Warn(TAG, $"No dialogue close event assigned on {who} — its dialogue can't be closed from AI");
                }
                return;
            }
            _onDialogueCloseRequested.Raise(this);
        }

        private bool RaiseDialogueRequest(StartDialogueNode forcedStartNode, bool thenTopics = false, string openingLine = null)
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
                forcedLine      = forcedStartNode != null ? openingLine : null,
                requester       = this,
            });
            return true;
        }
    }
}
