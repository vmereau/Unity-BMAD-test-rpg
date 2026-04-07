using Game.Core;
using Game.NPC;
using Game.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    public class DialogueUI : MonoBehaviour
    {
        private const string TAG = "[DialogueUI]";

        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _npcNameText;
        [SerializeField] private TMP_Text _responseText;
        [SerializeField] private Transform _topicsContainer;
        [SerializeField] private GameObject _topicButtonPrefab;
        [SerializeField] private DialogueSystem _dialogueSystem;

        private InputSystem_Actions _input;

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

        public void Open(string npcName, NPCMemoryEntrySO[] topics)
        {
            if (_panel == null)
            {
                GameLog.Error(TAG, "Panel not assigned — cannot open dialogue UI");
                return;
            }

            _panel.SetActive(true);

            if (_npcNameText != null)
                _npcNameText.text = npcName;

            if (_responseText != null)
                _responseText.text = string.Empty;

            PopulateTopics(topics);
            GameLog.Info(TAG, $"DialogueUI opened for {npcName} with {topics.Length} topics");
        }

        public void Close()
        {
            if (_panel != null)
                _panel.SetActive(false);

            ClearTopicButtons();

            if (_responseText != null)
                _responseText.text = string.Empty;

            GameLog.Info(TAG, "DialogueUI closed");
        }

        private void PopulateTopics(NPCMemoryEntrySO[] topics)
        {
            ClearTopicButtons();

            if (_topicsContainer == null || _topicButtonPrefab == null)
            {
                GameLog.Error(TAG, "TopicsContainer or TopicButtonPrefab not assigned");
                return;
            }

            foreach (var memory in topics)
            {
                if (memory == null) continue;
                AddTopicButton(memory);
            }

            AddFarewellButton();
        }

        private void AddTopicButton(NPCMemoryEntrySO memory)
        {
            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = memory.memoryId;

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
            {
                var captured = memory;
                btn.onClick.AddListener(() => ShowResponse(captured));
            }
        }

        private void AddFarewellButton()
        {
            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = "Farewell.";

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnFarewell);
        }

        private void ShowResponse(NPCMemoryEntrySO memory)
        {
            if (_responseText == null) return;
            _responseText.text = memory.HasDialogue() ? memory.effects.dialogueLines[0] : "...";
        }

        private void OnFarewell()
        {
            if (_dialogueSystem != null)
                _dialogueSystem.Close();
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
