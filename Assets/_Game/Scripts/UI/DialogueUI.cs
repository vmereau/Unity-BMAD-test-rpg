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

        private InputSystem_Actions _input;

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
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.Disable();
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

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

        /// <summary>Displays a TextDialogueNode: shows text and waits for click-anywhere to advance.</summary>
        public void ShowTextNode(TextDialogueNode node)
        {
            _pendingNextNode = node.nextNode;
            if (_responseText != null)
                _responseText.text = node.text;
            ClearTopicButtons();
            SetState(DisplayState.Text);
        }

        /// <summary>Displays a ChoiceDialogueNode: shows NPC text and the provided (pre-filtered) choices.</summary>
        public void ShowChoiceNode(ChoiceDialogueNode node, ChoiceOption[] availableChoices)
        {
            if (_responseText != null)
                _responseText.text = node.text;

            ClearTopicButtons();

            foreach (var choice in availableChoices)
            {
                if (choice == null) continue;
                AddChoiceButton(choice);
            }

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
                RestoreTopics();
        }

        // ── Private Helpers ─────────────────────────────────────────────────────

        private void SetState(DisplayState state)
        {
            _state = state;
            bool showResponse = state == DisplayState.Text || state == DisplayState.Choices;
            bool showTopics   = state == DisplayState.Topics || state == DisplayState.Choices;
            if (_responseText != null)
                _responseText.gameObject.SetActive(showResponse);
            if (_topicsScrollView != null)
                _topicsScrollView.SetActive(showTopics);
        }

        private void RestoreTopics()
        {
            if (_responseText != null)
                _responseText.text = string.Empty;
            if (_cachedStartNodes != null && _cachedStartNodes.Length > 0)
                PopulateStartNodes(_cachedStartNodes);
            else
                ClearTopicButtons();
            SetState(DisplayState.Topics);
        }

        private void PopulateStartNodes(StartDialogueNode[] startNodes)
        {
            ClearTopicButtons();

            if (_topicsContainer == null || _topicButtonPrefab == null)
            {
                GameLog.Error(TAG, "TopicsContainer or TopicButtonPrefab not assigned");
                return;
            }

            foreach (var node in startNodes)
            {
                if (node == null) continue;
                AddStartNodeButton(node);
            }
        }

        private void AddStartNodeButton(StartDialogueNode node)
        {
            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = node.text;

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
            {
                var captured = node;
                btn.onClick.AddListener(() =>
                {
                    if (captured.nextNode != null)
                        _dialogueSystem.AdvanceToNode(captured.nextNode);
                    else
                        GameLog.Warn(TAG, $"StartDialogueNode '{captured.text}' has no nextNode — ignoring click");
                });
            }
        }

        private void AddChoiceButton(ChoiceOption choice)
        {
            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = choice.text;

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
            {
                var captured = choice;
                btn.onClick.AddListener(() =>
                {
                    if (captured.nextNode != null)
                        _dialogueSystem.AdvanceToNode(captured.nextNode);
                    else
                        RestoreTopics();
                });
            }
        }

        private void ClearTopicButtons()
        {
            if (_topicsContainer == null) return;
            for (int i = _topicsContainer.childCount - 1; i >= 0; i--)
                Destroy(_topicsContainer.GetChild(i).gameObject);
        }

        private void HandleCancel(InputAction.CallbackContext ctx)
        {
            if (_dialogueSystem != null)
                _dialogueSystem.Close();
        }
    }
}
