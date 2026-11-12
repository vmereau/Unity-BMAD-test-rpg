using Game.Dialogue;

namespace Game.Core
{
    /// <summary>Payload of <c>OnDialogueSignal</c>: which signal a picked choice carried, and who the player was talking to.</summary>
    [System.Serializable]
    public struct DialogueSignalData
    {
        public DialogueSignalSO signal;
        public string npcName;
    }
}
