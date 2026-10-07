using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>One label/value row in the item detail panel's stats block. Pooled by <see cref="ItemDetailPanelUI"/>.</summary>
    public class ItemStatRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _value;

        public void Set(string label, string value, Color valueColor)
        {
            if (_label != null) _label.text = label;
            if (_value == null) return;

            bool hasValue = !string.IsNullOrEmpty(value);
            _value.gameObject.SetActive(hasValue); // tag rows (e.g. "Consumable") have no value
            if (!hasValue) return;
            _value.text = value;
            _value.color = valueColor;
        }
    }
}
