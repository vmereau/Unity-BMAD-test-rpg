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

        private const string LABEL_CONTINUE = "Continue";
        private const string LABEL_END = "End";
        private const string LABEL_TRADE = "Trade";
        private const int MAX_SLOTS = 10;

        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _npcNameText;
        [SerializeField] private TMP_Text _responseText;
        [SerializeField] private TMP_Text _choiceNodeText;
        [SerializeField] private GameObject _topicsScrollView;
        [SerializeField] private Transform _topicsContainer;
        [SerializeField] private GameObject _topicButtonPrefab;
        [SerializeField] private DialogueSystem _dialogueSystem;
        [SerializeField] private GameObject _responseWrapper;
        [SerializeField] private Button _nextNodeButton;

        [Header("Readability")]
        [Tooltip("Label of _nextNodeButton: Continue / End / Trade.")]
        [SerializeField] private TMP_Text _nextNodeLabel;
        [Tooltip("Typewriter speed for NPC lines (unscaled time). 0 = instant.")]
        [SerializeField, Min(0f)] private float _charsPerSecond = 60f;
        [Tooltip("Seconds after new content appears during which keys / clicks are ignored (Escape excepted).")]
        [SerializeField, Min(0f)] private float _inputGrace = 0.2f;

        private InputSystem_Actions _input;
        private InputAction[] _slotActions; // [0..9] = keys 1-9, [9] = key 0

        // _slotCallbacks[1..9] = keys 1-9, [10] = key 0. Index 0 unused. In Text state [1] is the advance action.
        private readonly System.Action[] _slotCallbacks = new System.Action[MAX_SLOTS + 1];

        private enum DisplayState { Topics, Text, Choices }
        private DisplayState _state = DisplayState.Topics;
        private DialogueNode _pendingNextNode;
        private StartDialogueNode[] _cachedStartNodes = System.Array.Empty<StartDialogueNode>();
        private bool _forcedLine; // NPC-initiated single line: any advance / Escape closes the dialogue

        private float _shownAt;
        private bool _revealing;
        private float _revealStart;
        private int _revealTotal;

        private void Awake()
        {
            _input = new InputSystem_Actions();
            _slotActions = new[]
            {
                _input.UI.DialogueOption1, _input.UI.DialogueOption2, _input.UI.DialogueOption3,
                _input.UI.DialogueOption4, _input.UI.DialogueOption5, _input.UI.DialogueOption6,
                _input.UI.DialogueOption7, _input.UI.DialogueOption8, _input.UI.DialogueOption9,
                _input.UI.DialogueOption0,
            };

            if (_nextNodeButton != null)
            {
                _nextNodeButton.onClick.AddListener(TryAdvance);
                // Not selectable by navigation: once clicked it would stay selected and the UI module's Submit (Enter)
                // would fire it again in the same frame as DialogueAdvance.
                var nav = _nextNodeButton.navigation;
                nav.mode = Navigation.Mode.None;
                _nextNodeButton.navigation = nav;
            }
            if (_panel != null)
                _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.UI.Enable();
            _input.UI.Cancel.performed += HandleCancel;
            _input.UI.DialogueAdvance.performed += HandleAdvance;
            foreach (var action in _slotActions)
                action.performed += HandleSlot;
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.DialogueAdvance.performed -= HandleAdvance;
            foreach (var action in _slotActions)
                action.performed -= HandleSlot;
            _input.UI.Disable();
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        private void Update()
        {
            if (!_revealing || _responseText == null) return;
            int visible = DialogueReveal.VisibleCount(Time.unscaledTime - _revealStart, _charsPerSecond, _revealTotal);
            _responseText.maxVisibleCharacters = visible;
            if (visible >= _revealTotal)
                CompleteReveal();
        }

        // ── Input handlers ──────────────────────────────────────────────────────

        private void HandleSlot(InputAction.CallbackContext ctx)
        {
            int index = System.Array.IndexOf(_slotActions, ctx.action);
            if (index < 0) return;
            int slot = index + 1;

            if (_state == DisplayState.Text)
            {
                if (slot == 1) TryAdvance();
                return;
            }
            if (!IsInputAllowed()) return;
            _slotCallbacks[slot]?.Invoke();
        }

        private void HandleAdvance(InputAction.CallbackContext _)
        {
            if (_state == DisplayState.Text)
                TryAdvance();
        }

        private void HandleCancel(InputAction.CallbackContext ctx)
        {
            if (_state != DisplayState.Topics && !_forcedLine) return;
            if (_dialogueSystem != null)
                _dialogueSystem.Close();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_state != DisplayState.Text) return;
            TryAdvance();
        }

        /// <summary>
        /// Single advance entry point (click, key 1, DialogueAdvance, next button): finishes a running reveal,
        /// otherwise runs the Text-state advance action. Ignored during the input grace.
        /// </summary>
        private void TryAdvance()
        {
            if (_state != DisplayState.Text) return;
            if (!IsInputAllowed()) return;
            if (_revealing)
            {
                CompleteReveal();
                MarkShown(); // a second press in the same burst (Enter + Submit, double-click) must not skip the line
                return;
            }
            _slotCallbacks[1]?.Invoke();
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

            _forcedLine = false;
            _cachedStartNodes = startNodes;

            _panel.SetActive(true);

            if (_npcNameText != null)
                _npcNameText.text = npcName;

            RestoreTopics();
            GameLog.Info(TAG, $"DialogueUI opened with {startNodes.Length} topic(s)");
        }

        /// <summary>
        /// Opens the panel on a single NPC-initiated line (no topic list). Click, slot 1, the advance keys, the next
        /// button or Escape close the dialogue.
        /// </summary>
        public void OpenLine(string npcName, string line)
        {
            if (_panel == null)
            {
                GameLog.Error(TAG, "Panel not assigned — cannot open dialogue UI");
                return;
            }

            _forcedLine = true;
            _cachedStartNodes = System.Array.Empty<StartDialogueNode>();
            _panel.SetActive(true);

            if (_npcNameText != null)
                _npcNameText.text = npcName;

            ClearContents();
            _pendingNextNode = null;

            _slotCallbacks[1] = () =>
            {
                if (_dialogueSystem != null)
                    _dialogueSystem.Close();
            };

            ShowNpcLine(line, LABEL_END);
            GameLog.Info(TAG, "DialogueUI opened on a forced line");
        }

        /// <summary>Displays a TextDialogueNode: shows text and waits for key/click to advance.</summary>
        public void ShowTextNode(DialogueNode node)
        {
            ClearContents();
            _pendingNextNode = node.nextNode;

            _slotCallbacks[1] = () =>
            {
                if (_pendingNextNode != null)
                    _dialogueSystem.AdvanceToNode(_pendingNextNode);
                else
                {
                    _dialogueSystem.NotifyTopicCompleted();
                    RestoreTopics();
                }
            };

            ShowNpcLine(node.text, _pendingNextNode != null ? LABEL_CONTINUE : LABEL_END);
        }

        /// <summary>Displays a ShopDialogueNode: shows text and opens the trade UI on advance.</summary>
        public void ShowShopNode(ShopDialogueNode node, System.Action onShopOpened)
        {
            ClearContents();
            _pendingNextNode = null;
            _slotCallbacks[1] = () => onShopOpened?.Invoke();
            ShowNpcLine(node.text, LABEL_TRADE);
        }

        /// <summary>Displays a ChoiceDialogueNode: shows NPC text and the provided (pre-filtered) choices.</summary>
        public void ShowChoiceNode(ChoiceDialogueNode node, ChoiceOption[] availableChoices)
        {
            ClearContents();
            ShowChoicePrompt(node.text);

            int slot = 1;
            foreach (var choice in availableChoices)
            {
                if (choice == null) continue;
                AddChoiceButton(node, choice, slot++);
            }
            // Every option filtered out (memory gates): without a way out the player would be stuck (Escape only
            // works on the topic list).
            if (slot == 1)
            {
                GameLog.Warn(TAG, $"Choice node '{node.name}' has no available choice — adding [Leave]");
                AddLeaveButton(null, slot);
            }

            SetState(DisplayState.Choices);
        }

        /// <summary>Displays a TeachChoiceDialogueNode: NPC text above teaching choices with cost labels, followed by a Leave button.</summary>
        public void ShowTeachChoiceNode(TeachChoiceDialogueNode node, TeachChoiceOption[] availableChoices)
        {
            ClearContents();
            ShowChoicePrompt(node.text);

            int slot = 1;
            foreach (var choice in availableChoices)
            {
                if (choice == null) continue;
                AddTeachChoiceButton(choice, slot++);
            }
            AddLeaveButton(node.nextNode, slot);

            SetState(DisplayState.Choices);
        }

        public void Close()
        {
            if (_panel != null)
                _panel.SetActive(false);

            ClearContents();

            _pendingNextNode = null;
            _cachedStartNodes = System.Array.Empty<StartDialogueNode>();
            _forcedLine = false;
            SetState(DisplayState.Topics);

            GameLog.Info(TAG, "DialogueUI closed");
        }

        // ── Private Helpers ─────────────────────────────────────────────────────

        private bool IsInputAllowed() => DialogueReveal.IsInputAllowed(Time.unscaledTime - _shownAt, _inputGrace);

        private void MarkShown() => _shownAt = Time.unscaledTime;

        private void SetState(DisplayState state)
        {
            _state = state;
            bool showResponse = state == DisplayState.Text;
            bool showTopics   = state is DisplayState.Topics or DisplayState.Choices;
            if (_responseWrapper)
                _responseWrapper.SetActive(showResponse);
            if (_topicsScrollView)
                _topicsScrollView.SetActive(showTopics);
            MarkShown();
        }

        /// <summary>Text state: shows only this node's <paramref name="line"/> and starts revealing it.</summary>
        private void ShowNpcLine(string line, string advanceLabel)
        {
            if (_nextNodeLabel != null)
                _nextNodeLabel.text = advanceLabel;

            SetState(DisplayState.Text);
            StartReveal(line);
        }

        /// <summary>Choices state: NPC prompt above the options.</summary>
        private void ShowChoicePrompt(string text)
        {
            if (_choiceNodeText == null) return;
            _choiceNodeText.text = text;
            _choiceNodeText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private void StartReveal(string line)
        {
            if (_responseText == null) return;
            _responseText.text = line ?? string.Empty;
            _responseText.maxVisibleCharacters = 0;
            _responseText.ForceMeshUpdate(true);
            _revealTotal = _responseText.textInfo.characterCount;
            _revealStart = Time.unscaledTime;
            _revealing = _revealTotal > 0 && _charsPerSecond > 0f;
            if (!_revealing)
                CompleteReveal();
        }

        private void CompleteReveal()
        {
            _revealing = false;
            if (_responseText != null)
                _responseText.maxVisibleCharacters = int.MaxValue;
        }

        private void RestoreTopics()
        {
            ClearContents();
            _pendingNextNode = null;

            // Re-query to pick up played-state changes that occurred within this session
            if (_dialogueSystem != null)
                _cachedStartNodes = _dialogueSystem.GetCurrentStartNodes();

            // Unseen topics first, then the ones already picked (dimmed); numbered in display order.
            int nextSlot = 1;
            if (_cachedStartNodes != null)
            {
                foreach (var node in DialogueOptionFormat.OrderUnseenFirst(_cachedStartNodes, IsTopicDimmed))
                {
                    if (node == null) continue;
                    AddStartNodeButton(node, nextSlot++);
                }
            }
            AddFarewellButton(nextSlot);
            SetState(DisplayState.Topics);
        }

        /// <summary>
        /// Instantiates an option button labelled with its number key (none past slot 10), its Trade / Teach tag and,
        /// when <paramref name="seen"/>, dimmed text; wires the click and, when a key exists, the slot.
        /// Null <paramref name="action"/> = disabled button. <paramref name="richSuffix"/> is appended unescaped.
        /// </summary>
        private void AddOptionButton(string text, int slot, System.Action action,
            DialogueOptionTag tag = DialogueOptionTag.None, bool seen = false, string richSuffix = null)
        {
            if (_topicsContainer == null || _topicButtonPrefab == null) return;

            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = DialogueOptionFormat.Label(slot <= MAX_SLOTS ? SlotLabel(slot) : null, text, tag, seen, richSuffix);

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = action != null;
                if (action != null)
                    btn.onClick.AddListener(() =>
                    {
                        if (IsInputAllowed()) action();
                    });
            }

            if (slot <= MAX_SLOTS)
                _slotCallbacks[slot] = action;
        }

        private void AddStartNodeButton(StartDialogueNode node, int slot)
        {
            var captured = node;
            DialogueOptionTag tag = DialogueOptionFormat.Classify(node);
            AddOptionButton(node.text, slot, () =>
            {
                if (captured.nextNode == null)
                {
                    GameLog.Warn(TAG, $"StartDialogueNode '{captured.text}' has no nextNode — ignoring click");
                    return;
                }
                _dialogueSystem.StartTopic(captured);
            }, tag, IsTopicDimmed(node));
        }

        // Trade / Teach topics are services the player comes back for: never dimmed or sorted down.
        private bool IsTopicDimmed(StartDialogueNode node) =>
            node != null && _dialogueSystem != null
            && DialogueOptionFormat.Classify(node) == DialogueOptionTag.None
            && _dialogueSystem.IsTopicSeen(node);

        // Always added (even past slot 10, then without a number key) so the player can never be trapped.
        private void AddFarewellButton(int slot)
        {
            AddOptionButton("[Farewell]", slot, () =>
            {
                if (_dialogueSystem != null)
                    _dialogueSystem.Close();
            });
        }

        private void AddChoiceButton(ChoiceDialogueNode node, ChoiceOption choice, int slot)
        {
            var captured = choice;
            DialogueOptionTag tag = DialogueOptionFormat.Classify(choice.nextNode);
            bool seen = tag == DialogueOptionTag.None && _dialogueSystem != null && _dialogueSystem.IsChoiceSeen(node, choice);
            AddOptionButton(choice.text, slot, () =>
            {
                _dialogueSystem.SelectChoice(captured, node);
                if (captured.nextNode == null)
                    RestoreTopics();
            }, tag, seen);
        }

        private void AddTeachChoiceButton(TeachChoiceOption choice, int slot)
        {
            TeachCostShortfall shortfall = _dialogueSystem != null
                ? _dialogueSystem.GetTeachCostShortfall(choice)
                : TeachCostShortfall.Gold | TeachCostShortfall.LearningPoints | TeachCostShortfall.Stats;
            bool canAfford = shortfall == TeachCostShortfall.None;

            // Unaffordable choices are shown disabled and have no slot callback (pressing their key does nothing).
            System.Action action = null;
            if (canAfford)
            {
                var captured = choice;
                action = () =>
                {
                    if (_dialogueSystem == null) return;
                    _dialogueSystem.ApplyTeachChoice(captured);
                };
            }
            // Teach choices are repeatable purchases: never dimmed as seen. An unaffordable one is drawn dimmed (the
            // row has no disabled tint) with the unpayable cost parts in red.
            AddOptionButton(choice.text, slot, action, seen: !canAfford,
                richSuffix: DialogueOptionFormat.CostLabel(choice, shortfall));
        }

        // Teach nodes (always, even past slot 10) and choice nodes left with no option. Plays exitNode — the teach
        // node's own nextNode, e.g. "Come back when you're ready" — when set, otherwise ends the topic.
        private void AddLeaveButton(DialogueNode exitNode, int slot)
        {
            AddOptionButton("[Leave]", slot, () =>
            {
                if (_dialogueSystem == null) return;
                if (exitNode != null)
                {
                    _dialogueSystem.AdvanceToNode(exitNode);
                    return;
                }
                _dialogueSystem.NotifyTopicCompleted();
                RestoreTopics();
            });
        }

        /// <summary>
        /// Clears buttons, slot callbacks and texts. Hides <c>_choiceNodeText</c> — every Choices-state method must
        /// re-show it (<see cref="ShowChoicePrompt"/>).
        /// </summary>
        private void ClearContents()
        {
            ClearTopicButtons();
            CompleteReveal();
            if (_responseText != null)
                _responseText.text = string.Empty;
            if (_choiceNodeText != null)
            {
                _choiceNodeText.text = string.Empty;
                _choiceNodeText.gameObject.SetActive(false);
            }
        }

        private void ClearTopicButtons()
        {
            System.Array.Clear(_slotCallbacks, 0, _slotCallbacks.Length);
            if (_topicsContainer == null) return;
            for (int i = _topicsContainer.childCount - 1; i >= 0; i--)
                Destroy(_topicsContainer.GetChild(i).gameObject);
        }

        private static string SlotLabel(int slot) => slot == MAX_SLOTS ? "0" : slot.ToString();
    }
}
