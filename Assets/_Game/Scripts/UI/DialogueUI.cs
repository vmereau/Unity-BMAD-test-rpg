using Game.Core;
using Game.Dialogue;
using Game.World;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    public class DialogueUI : MonoBehaviour, IPointerClickHandler
    {
        private const string TAG = "[DialogueUI]";

        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _npcNameText;
        [SerializeField] private TMP_Text _responseText;
        [SerializeField] private GameObject _topicsScrollView;
        [SerializeField] private Transform _topicsContainer;
        [SerializeField] private GameObject _topicButtonPrefab;
        [SerializeField] private DialogueSystem _dialogueSystem;
        [SerializeField] private GameObject _responseWrapper;
        [SerializeField] private Button _nextNodeButton;

        private InputSystem_Actions _input;

        // _slotCallbacks[1..9] = keys 1-9, [10] = key 0. Index 0 unused.
        private readonly System.Action[] _slotCallbacks = new System.Action[11];

        private enum DisplayState { Topics, Text, Choices }
        private DisplayState _state = DisplayState.Topics;
        private DialogueNode _pendingNextNode;
        private StartDialogueNode[] _cachedStartNodes = System.Array.Empty<StartDialogueNode>();

        private void Awake()
        {
            _input = new InputSystem_Actions();
            if (_panel != null)
                _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.UI.Enable();
            _input.UI.Cancel.performed += HandleCancel;
            _input.UI.DialogueOption1.performed += HandleSlot1;
            _input.UI.DialogueOption2.performed += HandleSlot2;
            _input.UI.DialogueOption3.performed += HandleSlot3;
            _input.UI.DialogueOption4.performed += HandleSlot4;
            _input.UI.DialogueOption5.performed += HandleSlot5;
            _input.UI.DialogueOption6.performed += HandleSlot6;
            _input.UI.DialogueOption7.performed += HandleSlot7;
            _input.UI.DialogueOption8.performed += HandleSlot8;
            _input.UI.DialogueOption9.performed += HandleSlot9;
            _input.UI.DialogueOption0.performed += HandleSlot10;
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.DialogueOption1.performed -= HandleSlot1;
            _input.UI.DialogueOption2.performed -= HandleSlot2;
            _input.UI.DialogueOption3.performed -= HandleSlot3;
            _input.UI.DialogueOption4.performed -= HandleSlot4;
            _input.UI.DialogueOption5.performed -= HandleSlot5;
            _input.UI.DialogueOption6.performed -= HandleSlot6;
            _input.UI.DialogueOption7.performed -= HandleSlot7;
            _input.UI.DialogueOption8.performed -= HandleSlot8;
            _input.UI.DialogueOption9.performed -= HandleSlot9;
            _input.UI.DialogueOption0.performed -= HandleSlot10;
            _input.UI.Disable();
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        // ── Slot key handlers (stored delegates for correct unsubscription) ──
        private void HandleSlot1(InputAction.CallbackContext _) => InvokeSlot(1);
        private void HandleSlot2(InputAction.CallbackContext _) => InvokeSlot(2);
        private void HandleSlot3(InputAction.CallbackContext _) => InvokeSlot(3);
        private void HandleSlot4(InputAction.CallbackContext _) => InvokeSlot(4);
        private void HandleSlot5(InputAction.CallbackContext _) => InvokeSlot(5);
        private void HandleSlot6(InputAction.CallbackContext _) => InvokeSlot(6);
        private void HandleSlot7(InputAction.CallbackContext _) => InvokeSlot(7);
        private void HandleSlot8(InputAction.CallbackContext _) => InvokeSlot(8);
        private void HandleSlot9(InputAction.CallbackContext _) => InvokeSlot(9);
        private void HandleSlot10(InputAction.CallbackContext _) => InvokeSlot(10);

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>Opens the dialogue panel showing a list of StartDialogueNode topics.</summary>
        public void Open(string npcName, StartDialogueNode[] startNodes)
        {
            if (_panel == null)
            {
                GameLog.Error(TAG, "Panel not assigned — cannot open dialogue UI");
                return;
            }

            _cachedStartNodes = startNodes;

            _panel.SetActive(true);

            if (_npcNameText != null)
                _npcNameText.text = npcName;

            RestoreTopics();
            GameLog.Info(TAG, $"DialogueUI opened with {startNodes.Length} topic(s)");
        }

        /// <summary>Displays a TextDialogueNode: shows text and waits for key/click to advance.</summary>
        public void ShowTextNode(TextDialogueNode node)
        {
            _pendingNextNode = node.nextNode;
            if (_responseText != null)
                _responseText.text = node.text;
            ClearTopicButtons();

            // Wire NextNodeButton and slot 1 for keyboard advance
            System.Action nextAction = () =>
            {
                if (_pendingNextNode != null)
                    _dialogueSystem.AdvanceToNode(_pendingNextNode);
                else
                {
                    _dialogueSystem.NotifyTopicCompleted();
                    RestoreTopics();
                }
            };

            if (_nextNodeButton != null)
            {
                _nextNodeButton.onClick.RemoveAllListeners();
                _nextNodeButton.onClick.AddListener(() => nextAction());
            }
            _slotCallbacks[1] = nextAction;

            SetState(DisplayState.Text);
        }

        /// <summary>Displays a ChoiceDialogueNode: shows NPC text and the provided (pre-filtered) choices.</summary>
        public void ShowChoiceNode(ChoiceDialogueNode node, ChoiceOption[] availableChoices)
        {
            if (_responseText != null)
                _responseText.text = node.text;

            ClearTopicButtons();

            int slot = 1;
            foreach (var choice in availableChoices)
            {
                if (choice == null) continue;
                AddChoiceButton(choice, slot++);
            }

            SetState(DisplayState.Choices);
        }

        /// <summary>Displays a TeachChoiceDialogueNode: NPC text above teaching choices with cost labels, followed by an Exit button.</summary>
        public void ShowTeachChoiceNode(TeachChoiceDialogueNode node, TeachChoiceOption[] availableChoices)
        {
            if (_responseText != null)
                _responseText.text = node.text;

            ClearTopicButtons();

            int slot = 1;
            foreach (var choice in availableChoices)
            {
                if (choice == null) continue;
                AddTeachChoiceButton(choice, slot++);
            }
            AddTeachExitButton(node.nextNode, slot);

            SetState(DisplayState.Choices);
        }

        public void Close()
        {
            if (_panel != null)
                _panel.SetActive(false);

            ClearTopicButtons();

            if (_responseText != null)
                _responseText.text = string.Empty;

            _pendingNextNode = null;
            _cachedStartNodes = System.Array.Empty<StartDialogueNode>();
            SetState(DisplayState.Topics);

            GameLog.Info(TAG, "DialogueUI closed");
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_state != DisplayState.Text) return;
            if (_dialogueSystem == null) return;

            if (_pendingNextNode != null)
                _dialogueSystem.AdvanceToNode(_pendingNextNode);
            else
            {
                _dialogueSystem.NotifyTopicCompleted();
                RestoreTopics();
            }
        }

        // ── Private Helpers ─────────────────────────────────────────────────────

        private void SetState(DisplayState state)
        {
            _state = state;
            bool showResponse = state == DisplayState.Text;
            bool showTopics   = state is DisplayState.Topics or DisplayState.Choices;
            if (_responseWrapper)
                _responseWrapper.SetActive(showResponse);
            if (_topicsScrollView)
                _topicsScrollView.SetActive(showTopics);
        }

        private void RestoreTopics()
        {
            if (_responseText != null)
                _responseText.text = string.Empty;
            _pendingNextNode = null;

            // Re-query to pick up played-state changes that occurred within this session
            if (_dialogueSystem != null)
                _cachedStartNodes = _dialogueSystem.GetCurrentStartNodes();

            ClearTopicButtons();

            int nextSlot = 1;
            if (_cachedStartNodes != null)
            {
                foreach (var node in _cachedStartNodes)
                {
                    if (node == null) continue;
                    AddStartNodeButton(node, nextSlot++);
                }
            }
            AddFarewellButton(nextSlot);
            SetState(DisplayState.Topics);
        }

        private void AddStartNodeButton(StartDialogueNode node, int slot)
        {
            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"{SlotLabel(slot)}. {node.text}";

            var captured = node;
            System.Action action = () =>
            {
                if (captured.nextNode != null)
                    _dialogueSystem.StartTopic(captured);
                else
                    GameLog.Warn(TAG, $"StartDialogueNode '{captured.text}' has no nextNode — ignoring click");
            };

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => action());

            if (slot <= 10)
                _slotCallbacks[slot] = action;
        }

        private void AddFarewellButton(int slot)
        {
            if (_topicsContainer == null || _topicButtonPrefab == null) return;
            if (slot > 10) return; // no slot available

            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"{SlotLabel(slot)}. [Farewell]";

            System.Action action = () =>
            {
                if (_dialogueSystem != null)
                    _dialogueSystem.Close();
            };

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => action());

            _slotCallbacks[slot] = action;
        }

        private void AddChoiceButton(ChoiceOption choice, int slot)
        {
            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"{SlotLabel(slot)}. {choice.text}";

            var captured = choice;
            System.Action action = () =>
            {
                if (captured.nextNode != null)
                    _dialogueSystem.AdvanceToNode(captured.nextNode);
                else
                {
                    _dialogueSystem.NotifyTopicCompleted();
                    RestoreTopics();
                }
            };

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => action());

            if (slot <= 10)
                _slotCallbacks[slot] = action;
        }

        private void AddTeachChoiceButton(TeachChoiceOption choice, int slot)
        {
            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = $"{SlotLabel(slot)}. {choice.text}{BuildCostLabel(choice)}";

            bool canAfford = _dialogueSystem != null && _dialogueSystem.CanAffordTeachChoice(choice);

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = canAfford;
                if (canAfford)
                {
                    var captured = choice;
                    btn.onClick.AddListener(() =>
                    {
                        if (_dialogueSystem != null)
                            _dialogueSystem.ApplyTeachChoice(captured);
                    });
                }
            }

            // Only register a slot callback for affordable choices (pressing key on disabled choice does nothing)
            if (slot <= 10 && canAfford)
            {
                var captured = choice;
                _slotCallbacks[slot] = () =>
                {
                    if (_dialogueSystem != null)
                        _dialogueSystem.ApplyTeachChoice(captured);
                };
            }
        }

        private void AddTeachExitButton(DialogueNode exitNode, int slot)
        {
            if (_topicsContainer == null || _topicButtonPrefab == null) return;
            if (slot > 10) return;

            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"{SlotLabel(slot)}. [Leave]";

            var capturedExit = exitNode;
            System.Action action = () =>
            {
                _dialogueSystem.NotifyTopicCompleted();
                RestoreTopics();
            };

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => action());

            _slotCallbacks[slot] = action;
        }

        private static string BuildCostLabel(TeachChoiceOption choice)
        {
            int effectiveLpCost = choice.skill != null ? choice.skill.lpCost : choice.lpCost;
            int gold = choice.goldCost;
            if (effectiveLpCost <= 0 && gold <= 0) return string.Empty;

            var sb = new System.Text.StringBuilder(" (");
            bool first = true;
            if (effectiveLpCost > 0)
            {
                sb.Append($"{effectiveLpCost} LP");
                first = false;
            }
            if (gold > 0)
            {
                if (!first) sb.Append(", ");
                sb.Append($"{gold}g");
            }
            sb.Append(')');
            return sb.ToString();
        }

        private void ClearTopicButtons()
        {
            if (_topicsContainer == null) return;
            for (int i = _topicsContainer.childCount - 1; i >= 0; i--)
                Destroy(_topicsContainer.GetChild(i).gameObject);
            System.Array.Clear(_slotCallbacks, 0, _slotCallbacks.Length);
        }

        private void HandleCancel(InputAction.CallbackContext ctx)
        {
            if (_state != DisplayState.Topics) return;
            if (_dialogueSystem != null)
                _dialogueSystem.Close();
        }

        private void InvokeSlot(int slot)
        {
            if (slot >= 1 && slot <= 10 && _slotCallbacks[slot] != null)
                _slotCallbacks[slot].Invoke();
        }

        private static string SlotLabel(int slot) => slot == 10 ? "0" : slot.ToString();
    }
}
