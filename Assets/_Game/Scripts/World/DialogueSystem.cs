using Game.Core;
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

            var activeMemories = data.memories != null
                ? data.memories.GetActiveDialogueMemories()
                : System.Array.Empty<NPCMemoryEntrySO>();

            _dialogueUI.Open(data.npcName, activeMemories);
            IsOpen = true;
            CursorManager.Unlock();
            GameLog.Info(TAG, $"Opened dialogue with {data.npcName}");
        }

        public void Close()
        {
            if (_dialogueUI == null) return;
            _dialogueUI.Close();
            IsOpen = false;
            CursorManager.Lock();
            GameLog.Info(TAG, "Closed dialogue");
        }
    }
}
