using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// Topic that hands the player <see cref="item"/> up to <see cref="perDay"/> times per NPC day. The label shows the
    /// count ("My daily ration (2 left)"); the count resets when the NPC wakes up at the end of its routine Sleep block
    /// (<c>NpcRoutine.TryGetWakeHour</c>), not at midnight. Picking it gives one item and plays <see cref="DialogueNode.nextNode"/>;
    /// refused with <see cref="notNowLine"/> while the NPC is in its Sleep block (awake = woken up) and with
    /// <see cref="noneLeftLine"/> when none are left. State keyed by this node's id and saved: one node per NPC (a node shared by two NPCs shares one count). Never marked seen (never dimmed), its
    /// <see cref="StartDialogueNode.dialogueFact"/> is ignored.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Dialogue/Daily Ration Node", fileName = "Start_")]
    public class DailyRationDialogueNode : StartDialogueNode
    {
        [Header("Daily ration")]
        [Tooltip("Item given to the player each time the topic is picked (one unit).")]
        public ItemSO item;
        [Tooltip("How many can be taken per NPC day (the day starts when the NPC wakes up from its routine sleep).")]
        [Min(1)] public int perDay = 2;
        [Tooltip("NPC line when none are left today.")]
        [TextArea] public string noneLeftLine = "That's all for today. Come back tomorrow.";
        [Tooltip("NPC line while the NPC should be asleep (the player woke it up).")]
        [TextArea] public string notNowLine = "Not at this hour. Let me sleep.";
        [Tooltip("NPC line when the player's inventory can't take the item (nothing counted).")]
        [TextArea] public string inventoryFullLine = "Your hands are full. Make some room first.";

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (item == null) GameLog.Warn("[DailyRationDialogueNode]", $"{name}: no item — the ration gives nothing");
            if (nextNode == null) GameLog.Warn("[DailyRationDialogueNode]", $"{name}: no nextNode — the topic can't start");
            if (dialogueFact != null)
                GameLog.Warn("[DailyRationDialogueNode]", $"{name}: dialogueFact is ignored — a daily ration is always repeatable");
        }
#endif
    }
}
