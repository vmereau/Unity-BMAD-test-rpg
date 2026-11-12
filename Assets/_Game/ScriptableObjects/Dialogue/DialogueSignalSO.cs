using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// Identity asset a <see cref="ChoiceOption"/> can carry. Picking the choice raises <c>OnDialogueSignal</c> with it, so a
    /// gameplay system (e.g. a toll gate) can react to the player's answer without reading dialogue facts. No data of its
    /// own: the asset reference is the id.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Dialogue/Signal", fileName = "Signal_")]
    public class DialogueSignalSO : ScriptableObject { }
}
