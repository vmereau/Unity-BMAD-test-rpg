namespace Game.Core
{
    [System.Serializable]
    public struct WorldFactData
    {
        public string key;
        public bool value;
        public WorldFactData(string key, bool value) { this.key = key; this.value = value; }
    }
}
