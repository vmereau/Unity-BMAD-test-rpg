using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Abstract ScriptableObject base for all typed world facts.
    /// Subclasses encode their key format in ToString() — WorldStateManager calls
    /// fact.ToString() and stores the result in Dictionary&lt;string, bool&gt;.
    /// Create Fact assets via the Game/Facts/ Create Asset menus.
    /// </summary>
    public abstract class Fact : ScriptableObject
    {
        public WorldFactPrefix Prefix { get; protected set; }
        public abstract override string ToString();
    }
}
