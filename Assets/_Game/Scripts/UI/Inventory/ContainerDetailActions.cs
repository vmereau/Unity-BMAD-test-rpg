using Game.Core;
using Game.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class ContainerDetailActions : MonoBehaviour
    {
        private const string TAG = "[ContainerDetailActions]";

        [SerializeField] private Button _takeButton;
        [SerializeField] private Button _putButton;

        public void Bind(ContainerUI owner, int slotIndex, ItemSO item, ContainerSide side, bool takeOnly)
        {
            if (item == null || owner == null)
            {
                GameLog.Warn(TAG, "Bind: item or owner is null");
                return;
            }

            if (_takeButton != null) _takeButton.onClick.RemoveAllListeners();
            if (_putButton != null) _putButton.onClick.RemoveAllListeners();

            if (side == ContainerSide.Container)
            {
                if (_takeButton != null)
                {
                    _takeButton.gameObject.SetActive(true);
                    _takeButton.onClick.AddListener(() => owner.TakeItem(slotIndex));
                }
                if (_putButton != null) _putButton.gameObject.SetActive(false);
            }
            else // player side
            {
                if (_takeButton != null) _takeButton.gameObject.SetActive(false);
                if (_putButton != null)
                {
                    bool showPut = !takeOnly;
                    _putButton.gameObject.SetActive(showPut);
                    if (showPut) _putButton.onClick.AddListener(() => owner.PutItem(slotIndex));
                }
            }
        }
    }
}
