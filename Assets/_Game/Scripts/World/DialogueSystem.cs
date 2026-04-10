using Game.AI;
using Game.Core;
using Game.Dialogue;
using Game.NPC;
using Game.Player;
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
            if (!_currentStartNode.isRepeatable)
            {
                if (WorldStateManager.Instance != null)
                    WorldStateManager.Instance.SetDialoguePlayed(_currentStartNode.name);
                else
                    GameLog.Warn(TAG, $"WorldStateManager unavailable — dialogue topic '{_currentStartNode.name}' played state not recorded");
                GameLog.Info(TAG, $"Dialogue topic '{_currentStartNode.name}' marked as played");
            }
            _currentStartNode = null;
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
