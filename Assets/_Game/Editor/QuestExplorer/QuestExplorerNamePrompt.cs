using Game.Quest;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor.QuestExplorer
{
    /// <summary>
    /// Small modal prompt used by the Quest Explorer fact helpers: either a single text value
    /// (DialogueFact node id / WorldFact event key) or a quest + quest-state pair (QuestFact).
    /// </summary>
    public class QuestExplorerNamePrompt : EditorWindow
    {
        private string _label;
        private string _text = string.Empty;
        private bool _questMode;
        private QuestSO _quest;
        private int _state;
        private bool _confirmed;

        private TextField _textField;
        private PopupField<string> _statePopup;
        private VisualElement _stateHost;
        private HelpBox _error;

        /// <summary>Blocks until closed. Returns the trimmed, path-safe value.</summary>
        public static bool PromptText(string title, string label, out string value)
        {
            var w = CreateInstance<QuestExplorerNamePrompt>();
            w.titleContent = new GUIContent(title);
            w._label = label;
            w.minSize = w.maxSize = new Vector2(380, 110);
            w.ShowModalUtility();
            value = w._confirmed ? QuestEditActions.Sanitize(w._text) : null;
            return w._confirmed && !string.IsNullOrEmpty(value);
        }

        /// <summary>Blocks until closed. <paramref name="state"/> uses QuestFact._questState encoding.</summary>
        public static bool PromptQuestState(QuestSO defaultQuest, out QuestSO quest, out int state)
        {
            var w = CreateInstance<QuestExplorerNamePrompt>();
            w.titleContent = new GUIContent("New QuestFact");
            w._questMode = true;
            w._quest = defaultQuest;
            w.minSize = w.maxSize = new Vector2(380, 120);
            w.ShowModalUtility();
            quest = w._quest;
            state = w._state;
            return w._confirmed && quest != null;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = root.style.paddingRight = 8;
            root.style.paddingTop = root.style.paddingBottom = 8;

            if (_questMode)
            {
                var questField = new ObjectField("Quest") { objectType = typeof(QuestSO), allowSceneObjects = false, value = _quest };
                questField.RegisterValueChangedCallback(evt =>
                {
                    _quest = evt.newValue as QuestSO;
                    _state = 0;
                    RebuildStatePopup();
                });
                root.Add(questField);
                _stateHost = new VisualElement();
                root.Add(_stateHost);
                RebuildStatePopup();
            }
            else
            {
                _textField = new TextField(_label) { value = _text };
                _textField.labelElement.style.minWidth = 0;
                _textField.style.flexDirection = FlexDirection.Column;
                _textField.RegisterValueChangedCallback(evt => _text = evt.newValue);
                _textField.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) Confirm();
                    else if (evt.keyCode == KeyCode.Escape) Close();
                });
                root.Add(_textField);
                _textField.schedule.Execute(() => _textField.Focus());
            }

            _error = new HelpBox(string.Empty, HelpBoxMessageType.Error) { style = { display = DisplayStyle.None } };
            root.Add(_error);

            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 6 } };
            buttons.Add(new Button(Close) { text = "Cancel" });
            buttons.Add(new Button(Confirm) { text = "OK" });
            root.Add(buttons);
        }

        private void RebuildStatePopup()
        {
            _stateHost.Clear();
            var labels = new System.Collections.Generic.List<string>(QuestEditActions.QuestStateLabels(_quest));
            _state = Mathf.Clamp(_state, 0, labels.Count - 1);
            _statePopup = new PopupField<string>("State", labels, _state);
            _statePopup.RegisterValueChangedCallback(_ => _state = _statePopup.index);
            _statePopup.SetEnabled(_quest != null);
            _stateHost.Add(_statePopup);
        }

        private void Confirm()
        {
            if (!_questMode && string.IsNullOrWhiteSpace(_text))
            {
                ShowError("Value cannot be empty.");
                return;
            }
            if (_questMode && _quest == null)
            {
                ShowError("Pick a quest.");
                return;
            }
            _confirmed = true;
            Close();
        }

        private void ShowError(string message)
        {
            _error.text = message;
            _error.style.display = DisplayStyle.Flex;
        }
    }
}
