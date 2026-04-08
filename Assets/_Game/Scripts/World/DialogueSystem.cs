using Game.AI;
using Game.Core;
using Game.Dialogue;
using Game.NPC;
using Game.UI;
using UnityEngine;

namespace Game.World
{
    public class DialogueSystem : MonoBehaviour
    {
        private const string TAG = "[Dialogue]";

        [SerializeField] private GameEventSO_NPCDialogueRequest _onDialogueRequested;
        [SerializeField] private DialogueUI _dialogueUI;

        public bool IsOpen { get; private set; }

        private NPCMemoryComponent _currentNPCMemory;
        private NPCDialogueGraphComponent _currentGraph;

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

        public void Close()
        {
            if (_dialogueUI == null) return;
            _dialogueUI.Close();
            IsOpen = false;
            _currentNPCMemory = null;
            _currentGraph = null;
            CursorManager.Lock();
            GameLog.Info(TAG, "Closed dialogue");
        }
    }
}
