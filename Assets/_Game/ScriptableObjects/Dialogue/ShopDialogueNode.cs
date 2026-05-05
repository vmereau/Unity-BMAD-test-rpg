using UnityEngine;

namespace Game.Dialogue
{
    [CreateAssetMenu(menuName = "Game/Dialogue/Shop Node", fileName = "Shop_")]
    public class ShopDialogueNode : DialogueNode
    {
        // Inherits: 
        // text: NPC greeting before opening the shop interface.
        // nextNode: Not used for trade logic, trade UI closes dialogue on exit.
    }
}
