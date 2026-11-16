using Game.Combat;
using Game.Player;
using NUnit.Framework;
using UnityEngine;

// spec-execute-unconscious-npc: target selection, stomp alignment, start gate.
public class ExecuteTests
{
    private static readonly Vector3 Foot = new Vector3(-0.45f, 0f, 0.88f);

    [Test]
    public void IsCandidate_InFrontAndInRange()
    {
        Assert.IsTrue(ExecuteMath.IsCandidate(Vector3.zero, Vector3.forward, new Vector3(0.3f, 0f, 1.2f), 1.8f, 70f));
    }

    [Test]
    public void IsCandidate_TooFar()
    {
        Assert.IsFalse(ExecuteMath.IsCandidate(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 2f), 1.8f, 70f));
    }

    [Test]
    public void IsCandidate_Behind()
    {
        Assert.IsFalse(ExecuteMath.IsCandidate(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -1f), 1.8f, 70f));
    }

    [Test]
    public void IsCandidate_IgnoresHeight()
    {
        // A lying body's chest is well below the player root height on a slope; only the ground plane counts.
        Assert.IsTrue(ExecuteMath.IsCandidate(new Vector3(0f, 1f, 0f), Vector3.forward, new Vector3(0f, -0.5f, 1.5f), 1.8f, 70f));
    }

    [Test]
    public void IsCandidate_RightUnderThePlayer()
    {
        Assert.IsTrue(ExecuteMath.IsCandidate(Vector3.zero, Vector3.forward, new Vector3(0f, -0.2f, 0f), 1.8f, 70f));
    }

    [TestCase(0f, 0f, 1.5f)]
    [TestCase(2f, 3f, -1f)]
    [TestCase(-1f, 0f, 0.2f)]
    public void AlignStance_PutsTheFootOnTheTarget(float x, float z, float y)
    {
        var player = new Vector3(0.5f, y, -0.3f);
        var target = new Vector3(x, 0.2f, z);
        var (pos, yaw) = ExecuteMath.AlignStance(player, Vector3.forward, target, Foot);

        Vector3 foot = pos + Quaternion.Euler(0f, yaw, 0f) * Foot;
        Assert.AreEqual(target.x, foot.x, 1e-4f);
        Assert.AreEqual(target.z, foot.z, 1e-4f);
        Assert.AreEqual(player.y, pos.y, 1e-4f, "the player's height is kept");
    }

    [Test]
    public void AlignStance_KeepsTheApproachBearing()
    {
        var player = Vector3.zero;
        var target = new Vector3(0f, 0f, 1.5f);
        var (pos, _) = ExecuteMath.AlignStance(player, Vector3.forward, target, Foot);

        // The landing foot lies on the player → target line extended from the new stance.
        Vector3 toTarget = (target - pos); toTarget.y = 0f;
        Vector3 approach = (target - player); approach.y = 0f;
        Assert.AreEqual(0f, Vector3.Angle(toTarget, approach), 0.01f);
        Assert.AreEqual(Foot.magnitude, toTarget.magnitude, 1e-4f);
    }

    [Test]
    public void AlignStance_StandingOnTheTarget_KeepsTheFacing()
    {
        // Target right under the player: the approach bearing falls back to the player's facing, not world north.
        var player = new Vector3(1f, 0f, 1f);
        var (pos, yaw) = ExecuteMath.AlignStance(player, Vector3.right, player, Foot);

        float offsetBearing = Mathf.Atan2(Foot.x, Foot.z) * Mathf.Rad2Deg;
        Assert.AreEqual(0f, Mathf.DeltaAngle(90f - offsetBearing, yaw), 0.01f);
        Vector3 foot = pos + Quaternion.Euler(0f, yaw, 0f) * Foot;
        Assert.AreEqual(player.x, foot.x, 1e-4f);
        Assert.AreEqual(player.z, foot.z, 1e-4f);
    }

    [Test]
    public void CanExecute_IdleInCombat()
    {
        Assert.IsTrue(PlayerStateManager.EvaluateCanExecute(false, true, false, false, false, false, false, false, false));
    }

    [TestCase(true, true, false, false, false, false, false, false, false, TestName = "Busy")]
    [TestCase(false, false, false, false, false, false, false, false, false, TestName = "Sheathed")]
    [TestCase(false, true, true, false, false, false, false, false, false, TestName = "Airborne")]
    [TestCase(false, true, false, true, false, false, false, false, false, TestName = "Swimming")]
    [TestCase(false, true, false, false, true, false, false, false, false, TestName = "Attacking")]
    [TestCase(false, true, false, false, false, true, false, false, false, TestName = "Blocking")]
    [TestCase(false, true, false, false, false, false, true, false, false, TestName = "Aiming")]
    [TestCase(false, true, false, false, false, false, false, true, false, TestName = "Dodging")]
    [TestCase(false, true, false, false, false, false, false, false, true, TestName = "SwitchingWeapon")]
    public void CanExecute_Refused(bool busy, bool inCombat, bool airborne, bool swimming, bool attacking, bool blocking,
        bool aiming, bool dodging, bool switching)
    {
        Assert.IsFalse(PlayerStateManager.EvaluateCanExecute(busy, inCombat, airborne, swimming, attacking, blocking,
            aiming, dodging, switching));
    }
}
