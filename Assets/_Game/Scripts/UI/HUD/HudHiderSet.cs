using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>
    /// Pure set of objects currently asking the HUD to hide (one entry per open world screen).
    /// The HUD is hidden while the set is non-empty; adding / removing the same hider twice is a no-op.
    /// </summary>
    public class HudHiderSet
    {
        private readonly HashSet<object> _hiders = new HashSet<object>();

        public bool IsHidden => _hiders.Count > 0;

        /// <summary>Returns true when the hidden state changed.</summary>
        public bool Add(object hider)
        {
            if (hider == null) return false;
            bool wasHidden = IsHidden;
            _hiders.Add(hider);
            return wasHidden != IsHidden;
        }

        /// <summary>Returns true when the hidden state changed.</summary>
        public bool Remove(object hider)
        {
            if (hider == null) return false;
            bool wasHidden = IsHidden;
            _hiders.Remove(hider);
            return wasHidden != IsHidden;
        }
    }
}
