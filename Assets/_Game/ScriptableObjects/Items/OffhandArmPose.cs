namespace Game.Inventory
{
    /// <summary>
    /// Left-arm pose an off-hand item asks for (spec-sword-shield-take-combo): the <c>LeftArm</c> Animator layer holds it in
    /// idle and locomotion (actions play the clips' arm), while <see cref="None"/> always leaves the clips' own arm
    /// (shields, empty hand). Append only: assets store the int.
    /// </summary>
    public enum OffhandArmPose
    {
        None = 0,
        TorchHold = 1,
    }
}
