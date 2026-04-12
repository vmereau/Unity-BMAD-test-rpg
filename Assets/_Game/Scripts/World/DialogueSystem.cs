using Game.AI;
using Game.Core;
using Game.Dialogue;
using Game.Economy;
using Game.NPC;
using Game.Player;
using Game.Progression;
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

        public bool IsOpen { get; private set; }

        private NPCMemoryComponent _currentNPCMemory;
        private NPCDialogueGraphComponent _currentGraph;
        private StartDialogueNode _currentStartNode;

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

            _currentNPCMemory = data.memories;
            _currentGraph = data.graph;

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
        /// Records the choice's dialogueFact as played (if assigned), then advances the chain.
        /// </summary>
        public void SelectChoice(ChoiceOption choice)
        {
            if (choice == null) return;

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
        public bool CanAffordTeachChoice(TeachChoiceOption choice)
        {
            if (choice == null) return false;

            if (choice.goldCost > 0 && (_goldSystem == null || _goldSystem.Gold < choice.goldCost))
                return false;

            int effectiveLpCost = choice.skill != null ? choice.skill.lpCost : choice.statPoints;
            if (effectiveLpCost > 0 && (_lpSystem == null || _lpSystem.CurrentLP < effectiveLpCost))
                return false;

            // Stat requirements only apply to skill choices (stat path has no prerequisites).
            if (choice.skill != null && choice.skill.statsRequirements.Count > 0)
            {
                if (_playerStats == null || !_playerStats.ValidateStats(choice.skill.statsRequirements))
                    return false;
            }

            return true;
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
            _currentStartNode = null;
            CursorManager.Lock();
            GameLog.Info(TAG, "Closed dialogue");
        }
    }
}
