namespace Game.Core
{
    [System.Serializable]
    public struct FactData
    {
        public string key;
        public bool value;
        public FactData(string key, bool value) { this.key = key; this.value = value; }
    }
}
