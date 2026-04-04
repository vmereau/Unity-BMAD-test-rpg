using Game.Core;
using UnityEngine;

namespace Game.UI
{
    public class QuestLogUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[QuestLogUI]";

        public void OnScreenOpen()
        {
            GameLog.Info(TAG, "Quest Log opened (placeholder)");
        }

        public void OnScreenClose()
        {
            GameLog.Info(TAG, "Quest Log closed (placeholder)");
        }
    }
}
