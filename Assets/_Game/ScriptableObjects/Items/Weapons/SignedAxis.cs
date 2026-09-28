namespace Game.Inventory
{
    /// <summary>A signed principal axis in a mesh's local space (used to describe blade / edge directions).</summary>
    /// <remarks>
    /// Values are paired per axis (value / 2 = axis index) — <see cref="WeaponGripMath.AreValid"/> relies on it.
    /// Keep the explicit values when adding or reordering members.
    /// </remarks>
    public enum SignedAxis
    {
        PosX = 0,
        NegX = 1,
        PosY = 2,
        NegY = 3,
        PosZ = 4,
        NegZ = 5,
    }
}
