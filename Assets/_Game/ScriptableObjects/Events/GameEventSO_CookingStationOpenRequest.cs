using Game.Crafting;
using UnityEngine;

namespace Game.Core
{
    [System.Serializable]
    public struct CookingStationOpenRequestData
    {
        public RecipeSO[] recipes;  // the station's recipe list (asset refs)
        public string stationName;  // shown as the cooking screen title
    }

    [CreateAssetMenu(menuName = "Game/Events/Cooking Station Open Request", fileName = "NewCookingStationOpenRequestEvent")]
    public class GameEventSO_CookingStationOpenRequest : GameEventSO<CookingStationOpenRequestData> { }
}
