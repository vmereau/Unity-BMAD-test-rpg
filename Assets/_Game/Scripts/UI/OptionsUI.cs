using Game.Core;
using UnityEngine;

namespace Game.UI
{
    public class OptionsUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[OptionsUI]";

        public void OnScreenOpen()
        {
            GameLog.Info(TAG, "Options opened (placeholder)");
        }

        public void OnScreenClose()
        {
            GameLog.Info(TAG, "Options closed (placeholder)");
        }
    }
}
