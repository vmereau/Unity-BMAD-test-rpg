using Game.AI;
using Game.Core;
using Game.Dialogue;
using Game.Economy;
using Game.Inventory;
using Game.NPC;
using Game.Player;
using Game.Progression;
using Game.Quest;
using Game.UI;
using UnityEngine;

namespace Game.World
{
    public class DialogueSystem : MonoBehaviour
    {
        private const string TAG = "[Dialogue]";

        [SerializeField] private GameEventSO_NPCDialogueRequest _onDialogueRequested;
        [SerializeField] private DialogueUI _dialogueUI;
        [SerializeField] private PlayerStateManager _playerStateManager;
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private GoldSystem _goldSystem;
        [SerializeField] private LearningPointSystem _lpSystem;
        [SerializeField] private PlayerSkills _playerSkills;
        [SerializeField] private NPCTradeUI _tradeUI;

        public bool IsOpen { get; private set; }

        private NPCMemoryComponent _currentNPCMemory;
        private NPCDialogueGraphComponent _currentGraph;
        private InventorySystem _currentNPCInventory;
        private GoldSystem _currentNPCGoldSystem;
        private StartDialogueNode _currentStartNode;
        private readonly System.Collections.Generic.HashSet<DialogueNode> _warnedNoId = new();

        private void OnEnable()
        {
            if (_onDialogueRequested == null)
            {
                GameLog.Warn(TAG, "No dialogue event assigned — DialogueSystem will not respond to NPC interactions");
                return;
            }
            _onDialogueRequested.AddListener(HandleDialogueRequested);
        }

        private void OnDisable()
        {
            if (_onDialogueRequested == null) return;
            _onDialogueRequested.RemoveListener(HandleDialogueRequested);
        }

        private void HandleDialogueRequested(NPCDialogueRequestData data)
        {
            if (_dialogueUI == null)
            {
                GameLog.Error(TAG, "DialogueUI not assigned — cannot open dialogue");
                return;
            }

            if (IsOpen)
            {
                GameLog.Warn(TAG, $"Dialogue request from {data.npcName} ignored — a dialogue is already open");
                return;
            }

            // NPC-initiated (e.g. a witness scolding a thief): one line, no topics, the player closes it.
            if (!string.IsNullOrEmpty(data.forcedLine))
            {
                _currentNPCMemory = null;
                _currentGraph = null;
                _currentNPCInventory = null;
                _currentNPCGoldSystem = null;
                _currentStartNode = null;
                _dialogueUI.OpenLine(data.npcName, data.forcedLine);
                IsOpen = true;
                if (_playerStateManager != null)
                    _playerStateManager.SetInDialogue(true);
                CursorManager.Unlock();
                GameLog.Info(TAG, $"{data.npcName} confronts the player");
                return;
            }

            _currentNPCMemory = data.memories;
            _currentGraph = data.graph;
            _currentNPCInventory = data.npcInventory;
            _currentNPCGoldSystem = data.npcGoldSystem;

            StartDialogueNode[] startNodes = _currentGraph != null
                ? _currentGraph.GetAvailableStartNodes(_currentNPCMemory)
                : System.Array.Empty<StartDialogueNode>();

            _dialogueUI.Open(data.npcName, startNodes);
            IsOpen = true;
            if (_playerStateManager != null)
                _playerStateManager.SetInDialogue(true);
            CursorManager.Unlock();
            GameLog.Info(TAG, $"Opened dialogue with {data.npcName} — {startNodes.Length} topic(s) available");
        }

        /// <summary>
        /// Advances dialogue to the given node. null = close dialogue.
        /// Called by DialogueUI buttons (start topic, text advance, choice selection).
        /// </summary>
        public void AdvanceToNode(DialogueNode node)
        {
            if (node == null)
            {
                Close();
                return;
            }

            switch (node)
            {
                case TextDialogueNode textNode:
                    _dialogueUI.ShowTextNode(textNode);
                    break;

                case ShopDialogueNode shopNode:
                    if (_currentNPCInventory == null)
                    {
                        GameLog.Warn(TAG, "ShopDialogueNode reached but NPC has no InventorySystem — treating as regular text");
                        _dialogueUI.ShowTextNode(shopNode);
                    }
                    else
                    {
                        _dialogueUI.ShowShopNode(shopNode, () =>
                        {
                            InventorySystem inv = _currentNPCInventory;
                            GoldSystem npcGold = _currentNPCGoldSystem;
                            Close();
                            if (_tradeUI != null) _tradeUI.Open(inv, npcGold);
                        });
                    }
                    break;

                case ChoiceDialogueNode choiceNode:
                    ChoiceOption[] availableChoices = _currentGraph != null
                        ? _currentGraph.GetAvailableChoices(choiceNode, _currentNPCMemory)
                        : choiceNode.choices ?? System.Array.Empty<ChoiceOption>();
                    _dialogueUI.ShowChoiceNode(choiceNode, availableChoices);
                    break;

                case TeachChoiceDialogueNode teachNode:
                    TeachChoiceOption[] availableTeachChoices = _currentGraph != null
                        ? _currentGraph.GetAvailableTeachChoices(teachNode, _currentNPCMemory)
                        : teachNode.choices ?? System.Array.Empty<TeachChoiceOption>();
                    availableTeachChoices = FilterLearnedSkillChoices(availableTeachChoices);
                    _dialogueUI.ShowTeachChoiceNode(teachNode, availableTeachChoices);
                    break;

                case StartDialogueNode _:
                    // Author error: StartDialogueNode should not appear mid-chain
                    GameLog.Warn(TAG, $"StartDialogueNode '{node.name}' referenced mid-chain — closing dialogue");
                    Close();
                    break;

                default:
                    GameLog.Warn(TAG, $"Unknown node type '{node.GetType().Name}' — closing dialogue");
                    Close();
                    break;
            }
        }

        /// <summary>
        /// Called by DialogueUI when the player selects a topic.
        /// Tracks the active start node for played-state recording, then advances.
        /// </summary>
        public void StartTopic(StartDialogueNode startNode)
        {
            _currentStartNode = startNode;
            if (startNode != null)
                MarkSeen(startNode, startNode.Id);
            if (startNode.nextNode == null)
            {
                GameLog.Warn(TAG, $"StartDialogueNode '{startNode.name}' has no nextNode — cannot start topic");
                return;
            }
            AdvanceToNode(startNode.nextNode);
        }

        /// <summary>
        /// Called by DialogueUI when a dialogue chain reaches its end node (nextNode == null).
        /// If the active topic is non-repeatable, records it as played in WorldStateManager.
        /// Always clears the active start node reference.
        /// </summary>
        public void NotifyTopicCompleted()
        {
            if (_currentStartNode == null)
            {
                GameLog.Warn(TAG, "NotifyTopicCompleted called but no active start node — played state not recorded");
                return;
            }
            
            if (_currentStartNode.dialogueFact && WorldStateManager.Instance != null)
            {
                WorldStateManager.Instance.SetDialoguePlayed(_currentStartNode.dialogueFact);
                GameLog.Info(TAG, $"Dialogue topic '{_currentStartNode.name}' marked as played");
            }
            else
            {
                GameLog.Warn(TAG, $"WorldStateManager unavailable — dialogue topic '{_currentStartNode.name}' played state not recorded");
            }
            
            _currentStartNode = null;
        }

        /// <summary>
        /// Called by DialogueUI when the player selects a regular choice option.
        /// Marks the choice seen, records its dialogueFact as played (if assigned), then advances the chain.
        /// <paramref name="node"/> = the choice node that offered it (null = not tracked as seen).
        /// </summary>
        public void SelectChoice(ChoiceOption choice, ChoiceDialogueNode node = null)
        {
            if (choice == null) return;
            if (node != null)
                MarkSeen(node, node.ChoiceSeenId(choice));

            if (choice.dialogueFact && WorldStateManager.Instance != null)
            {
                WorldStateManager.Instance.SetDialoguePlayed(choice.dialogueFact);
                GameLog.Info(TAG, $"Choice '{choice.text}' marked as played via fact '{choice.dialogueFact}'");
            }

            if (choice.nextNode != null)
                AdvanceToNode(choice.nextNode);
            else
                NotifyTopicCompleted();
        }

        /// <summary>
        /// Returns available start nodes for the current NPC, re-evaluated to pick up
        /// played-state changes that occurred during this session.
        /// Called by DialogueUI when restoring the topic list after a chain completes.
        /// </summary>
        public StartDialogueNode[] GetCurrentStartNodes()
        {
            if (_currentGraph == null) return System.Array.Empty<StartDialogueNode>();
            return _currentGraph.GetAvailableStartNodes(_currentNPCMemory);
        }

        /// <summary>
        /// Returns true if the player currently meets the gold, LP, and stat requirements for the choice.
        /// Called by DialogueUI to set button interactable state.
        /// Does NOT check whether a skill is already learned — use requiredMemory authoring for that.
        /// </summary>
        public bool CanAffordTeachChoice(TeachChoiceOption choice) =>
            choice != null && GetTeachCostShortfall(choice) == TeachCostShortfall.None;

        /// <summary>Which of the choice's costs (gold / LP / stat requirements) the player can't currently meet.</summary>
        public TeachCostShortfall GetTeachCostShortfall(TeachChoiceOption choice)
        {
            var shortfall = TeachCostShortfall.None;
            if (choice == null) return shortfall;

            if (choice.goldCost > 0 && (_goldSystem == null || _goldSystem.Gold < choice.goldCost))
                shortfall |= TeachCostShortfall.Gold;

            int effectiveLpCost = choice.skill != null ? choice.skill.lpCost : choice.statPoints;
            if (effectiveLpCost > 0 && (_lpSystem == null || _lpSystem.CurrentLP < effectiveLpCost))
                shortfall |= TeachCostShortfall.LearningPoints;

            // Stat requirements only apply to skill choices (stat path has no prerequisites).
            if (choice.skill != null && choice.skill.statsRequirements.Count > 0)
            {
                if (_playerStats == null || !_playerStats.ValidateStats(choice.skill.statsRequirements))
                    shortfall |= TeachCostShortfall.Stats;
            }

            return shortfall;
        }

        // ── Seen state (persisted, no events) ────────────────────────────────

        /// <summary>True once the player has picked this topic (in any session of this save).</summary>
        public bool IsTopicSeen(StartDialogueNode node) => node != null && IsSeen(node, node.Id);

        /// <summary>True once the player has picked this choice of <paramref name="node"/>.</summary>
        public bool IsChoiceSeen(ChoiceDialogueNode node, ChoiceOption choice) =>
            node != null && IsSeen(node, node.ChoiceSeenId(choice));

        private bool IsSeen(DialogueNode node, string id)
        {
            if (!HasId(node, id)) return false;
            return WorldStateManager.Instance != null && WorldStateManager.Instance.IsDialogueSeen(id);
        }

        private void MarkSeen(DialogueNode node, string id)
        {
            if (!HasId(node, id) || WorldStateManager.Instance == null) return;
            WorldStateManager.Instance.MarkDialogueSeen(id);
        }

        // Empty id = asset never validated in the editor (or choice not part of the node): not tracked, warned once.
        private bool HasId(DialogueNode node, string id)
        {
            if (!string.IsNullOrEmpty(id)) return true;
            if (_warnedNoId.Add(node))
                GameLog.Warn(TAG, $"Dialogue node '{node.name}' has no id — seen state not tracked (reimport / validate the asset)");
            return false;
        }

        /// <summary>
        /// Deducts gold cost, then delegates to LearnSkill() (skill path) or TrySpendLP+UpgradeStat (stat path).
        /// Must only be called after CanAffordTeachChoice() returned true.
        /// Advances to choice.confirmNextNode on success (null = close dialogue), or denyNextNode when stat is at cap.
        /// </summary>
        public void ApplyTeachChoice(TeachChoiceOption choice)
        {
            if (choice == null) return;

            // ── Skill path ───────────────────────────────────────────────────────────
            if (choice.skill != null)
            {
                if (_playerSkills == null)
                {
                    GameLog.Warn(TAG, "ApplyTeachChoice: PlayerSkills not assigned — skill not granted");
                    AdvanceToNode(choice.confirmNextNode);
                    return;
                }

                // Guard: skill already learned (author omitted requiredMemory gate).
                // Check BEFORE spending gold so no currency is lost.
                if (_playerSkills.HasSkill(choice.skill.skillId))
                {
                    GameLog.Warn(TAG, $"ApplyTeachChoice: skill '{choice.skill.displayName}' already learned — requiredMemory gate missing on this TeachChoiceOption");
                    AdvanceToNode(choice.confirmNextNode);
                    return;
                }

                // Spend gold first, then let LearnSkill() handle LP deduction.
                if (choice.goldCost > 0)
                {
                    if (_goldSystem == null)
                        GameLog.Warn(TAG, "ApplyTeachChoice: GoldSystem not assigned — gold cost skipped");
                    else if (!_goldSystem.TrySpend(choice.goldCost))
                    {
                        GameLog.Error(TAG, $"ApplyTeachChoice: TrySpend({choice.goldCost}) failed — insufficient funds at apply time");
                        return;
                    }
                }

                if (!_playerSkills.LearnSkill(choice.skill))
                    GameLog.Error(TAG, $"ApplyTeachChoice: LearnSkill({choice.skill.displayName}) failed unexpectedly");
            }
            // ── Stat path ────────────────────────────────────────────────────────────
            else
            {
                // Stat cap check — no resources consumed if at or above limit
                if (choice.limitStat > 0)
                {
                    if (_playerStats == null)
                    {
                        GameLog.Warn(TAG, "ApplyTeachChoice: PlayerStats not assigned — cannot evaluate limitStat; proceeding without cap check");
                    }
                    else
                    {
                        int baseStat = _playerStats.GetBaseStat(choice.statToUpgrade);
                        if (baseStat >= choice.limitStat)
                        {
                            if (choice.denyNextNode == null)
                                GameLog.Warn(TAG, $"ApplyTeachChoice: {choice.statToUpgrade} at cap ({baseStat}/{choice.limitStat}) but denyNextNode is null — closing dialogue");
                            else
                                GameLog.Info(TAG, $"ApplyTeachChoice: {choice.statToUpgrade} at cap ({baseStat}/{choice.limitStat}) — routing to denyNextNode");
                            AdvanceToNode(choice.denyNextNode);
                            return;
                        }
                    }
                }

                if (choice.goldCost > 0)
                {
                    if (_goldSystem == null)
                        GameLog.Warn(TAG, "ApplyTeachChoice: GoldSystem not assigned — gold cost skipped");
                    else if (!_goldSystem.TrySpend(choice.goldCost))
                    {
                        GameLog.Error(TAG, $"ApplyTeachChoice: TrySpend({choice.goldCost}) failed — insufficient funds at apply time");
                        return;
                    }
                }

                if (!_lpSystem.TrySpendLP(choice.statPoints))
                {
                    GameLog.Error(TAG, $"ApplyTeachChoice: TrySpendLP({choice.statPoints}) failed — insufficient LP at apply time");
                    return;
                }

                if (_playerStats == null)
                    GameLog.Warn(TAG, "ApplyTeachChoice: PlayerStats not assigned — stat not upgraded");
                else
                    _playerStats.UpgradeStat(choice.statToUpgrade, choice.statPoints);
            }

            AdvanceToNode(choice.confirmNextNode);
        }

        private TeachChoiceOption[] FilterLearnedSkillChoices(TeachChoiceOption[] choices)
        {
            if (_playerSkills == null || choices.Length == 0) return choices;
            var result = new System.Collections.Generic.List<TeachChoiceOption>(choices.Length);
            foreach (var choice in choices)
            {
                if (choice == null) continue;
                if (choice.teachingType == TeachingType.SkillBased && choice.skill != null && _playerSkills.HasSkill(choice.skill.skillId))
                    continue;
                result.Add(choice);
            }
            return result.Count == choices.Length ? choices : result.ToArray();
        }

        public void Close()
        {
            if (_dialogueUI == null) return;
            _dialogueUI.Close();
            IsOpen = false;
            if (_playerStateManager != null)
                _playerStateManager.SetInDialogue(false);
            _currentNPCMemory = null;
            _currentGraph = null;
            _currentNPCInventory = null;
            _currentNPCGoldSystem = null;
            _currentStartNode = null;
            CursorManager.Lock();
            GameLog.Info(TAG, "Closed dialogue");
        }
    }
}
