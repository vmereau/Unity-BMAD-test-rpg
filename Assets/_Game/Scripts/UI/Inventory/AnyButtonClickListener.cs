using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>Catches any pointer button click and forwards it to a callback.</summary>
    public class AnyButtonClickListener : MonoBehaviour, IPointerClickHandler
    {
        [System.NonSerialized] public System.Action<PointerEventData.InputButton> callback;
        public void OnPointerClick(PointerEventData eventData) => callback?.Invoke(eventData.button);
    }
}
