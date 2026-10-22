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
        [Tooltip("Faded lines of the current topic shown above the NPC line (Text state only).")]
        [SerializeField] private TMP_Text _historyText;
        [Tooltip("Label of _nextNodeButton: Continue / End / Trade.")]
        [SerializeField] private TMP_Text _nextNodeLabel;
        [Tooltip("History rows kept above the current NPC line.")]
        [SerializeField, Min(1)] private int _historyCapacity = 3;
        [Tooltip("History lines longer than this are cropped with an ellipsis (keeps each row to ~1 line). 0 = no limit.")]
        [SerializeField, Min(0)] private int _historyLineMaxChars = 90;
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

        private DialogueTranscript _transcript;
        private string _npcName = string.Empty;
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
            _transcript = new DialogueTranscript(_historyCapacity, _historyLineMaxChars);

            if (_nextNodeButton != null)
            {
                _nextNodeButton.onClick.AddListener(TryAdvance);
                // Not selectable by navigation: once clicked it would stay selected and the UI module's Submit (Enter)
                // would fire it again in the same frame as DialogueAdvance.
                var nav = _nextNodeButton.navigation;
                nav.mode = Navigation.Mode.None;
                _nextNodeButton.navigation = nav;
            }
            if (_historyText == null)
                GameLog.Warn(TAG, "_historyText not assigned — dialogue history will not be shown");
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
            _npcName = npcName ?? string.Empty;

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
            _npcName = npcName ?? string.Empty;
            _panel.SetActive(true);

            if (_npcNameText != null)
                _npcNameText.text = npcName;

            ClearContents();
            _transcript.Clear();
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
                AddChoiceButton(choice, slot++);
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
            _transcript.Clear();
            RefreshHistory();

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

        /// <summary>
        /// Text state: renders the history (lines before this one), starts revealing <paramref name="line"/>,
        /// then records it so it becomes history for the next line.
        /// </summary>
        private void ShowNpcLine(string line, string advanceLabel)
        {
            RefreshHistory();
            if (_nextNodeLabel != null)
                _nextNodeLabel.text = advanceLabel;

            SetState(DisplayState.Text);
            StartReveal(line);
            _transcript.Add(_npcName, line, false);
        }

        /// <summary>Choices state: NPC prompt above the options (no history). Recorded for the next Text state.</summary>
        private void ShowChoicePrompt(string text)
        {
            if (_choiceNodeText != null)
            {
                _choiceNodeText.text = text;
                _choiceNodeText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
            _transcript.Add(_npcName, text, false);
        }

        private void RecordPlayerLine(string text) => _transcript.Add(DialogueTranscript.PLAYER_SPEAKER, text, true);

        private void RefreshHistory()
        {
            if (_historyText == null) return;
            bool hasHistory = _transcript.Count > 0;
            _historyText.text = hasHistory ? _transcript.BuildRichText() : string.Empty;
            _historyText.gameObject.SetActive(hasHistory);
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
            _transcript.Clear();
            RefreshHistory();
            _pendingNextNode = null;

            // Re-query to pick up played-state changes that occurred within this session
            if (_dialogueSystem != null)
                _cachedStartNodes = _dialogueSystem.GetCurrentStartNodes();

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

        /// <summary>
        /// Instantiates an option button labelled with its number key (none past slot 10), wires the click and,
        /// when a key exists, the slot. Null <paramref name="action"/> = disabled button.
        /// </summary>
        private void AddOptionButton(string text, int slot, System.Action action)
        {
            if (_topicsContainer == null || _topicButtonPrefab == null) return;

            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = slot <= MAX_SLOTS ? $"{SlotLabel(slot)}. {text}" : text;

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
            AddOptionButton(node.text, slot, () =>
            {
                if (captured.nextNode == null)
                {
                    GameLog.Warn(TAG, $"StartDialogueNode '{captured.text}' has no nextNode — ignoring click");
                    return;
                }
                RecordPlayerLine(captured.text);
                _dialogueSystem.StartTopic(captured);
            });
        }

        // Always added (even past slot 10, then without a number key) so the player can never be trapped.
        private void AddFarewellButton(int slot)
        {
            AddOptionButton("[Farewell]", slot, () =>
            {
                if (_dialogueSystem != null)
                    _dialogueSystem.Close();
            });
        }

        private void AddChoiceButton(ChoiceOption choice, int slot)
        {
            var captured = choice;
            AddOptionButton(choice.text, slot, () =>
            {
                RecordPlayerLine(captured.text);
                _dialogueSystem.SelectChoice(captured);
                if (captured.nextNode == null)
                    RestoreTopics();
            });
        }

        private void AddTeachChoiceButton(TeachChoiceOption choice, int slot)
        {
            bool canAfford = _dialogueSystem != null && _dialogueSystem.CanAffordTeachChoice(choice);

            // Unaffordable choices are shown disabled and have no slot callback (pressing their key does nothing).
            System.Action action = null;
            if (canAfford)
            {
                var captured = choice;
                action = () =>
                {
                    if (_dialogueSystem == null) return;
                    RecordPlayerLine(captured.text);
                    _dialogueSystem.ApplyTeachChoice(captured);
                };
            }
            AddOptionButton($"{choice.text}{BuildCostLabel(choice)}", slot, action);
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

        private static string BuildCostLabel(TeachChoiceOption choice)
        {
            int effectiveLpCost = choice.skill != null ? choice.skill.lpCost : choice.statPoints;
            int gold = choice.goldCost;
            bool hasStatReqs = choice.skill != null && choice.skill.statsRequirements.Count > 0;

            if (effectiveLpCost <= 0 && gold <= 0 && !hasStatReqs) return string.Empty;

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
                first = false;
            }
            if (hasStatReqs)
            {
                foreach (var req in choice.skill.statsRequirements)
                {
                    if (!first) sb.Append(", ");
                    sb.Append($"{req.statType}: {req.value}");
                    first = false;
                }
            }
            sb.Append(')');
            return sb.ToString();
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
