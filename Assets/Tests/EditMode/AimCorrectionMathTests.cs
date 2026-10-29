using Game.Combat;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for the bow aim correction math — AimCorrectionMath.
/// </summary>
public class AimCorrectionMathTests
{
    private const float EPS = 0.01f;

    [Test]
    public void YawPitchDelta_TurnRight_IsPositiveYaw()
    {
        Vector2 d = AimCorrectionMath.YawPitchDelta(Vector3.forward, Vector3.right);
        Assert.That(d.x, Is.EqualTo(90f).Within(EPS));
        Assert.That(d.y, Is.EqualTo(0f).Within(EPS));
    }

    [Test]
    public void YawPitchDelta_TurnLeft_IsNegativeYaw()
        => Assert.That(AimCorrectionMath.YawPitchDelta(Vector3.forward, Vector3.left).x, Is.EqualTo(-90f).Within(EPS));

    [Test]
    public void YawPitchDelta_TakesShortestWayAcrossBehind()
    {
        Vector3 current = Quaternion.Euler(0f, 170f, 0f) * Vector3.forward;
        Vector3 desired = Quaternion.Euler(0f, -170f, 0f) * Vector3.forward;
        Assert.That(AimCorrectionMath.YawPitchDelta(current, desired).x, Is.EqualTo(20f).Within(EPS));
    }

    [Test]
    public void YawPitchDelta_AimUp_IsPositivePitch()
    {
        Vector3 up30 = Quaternion.Euler(-30f, 0f, 0f) * Vector3.forward;
        Assert.That(AimCorrectionMath.YawPitchDelta(Vector3.forward, up30).y, Is.EqualTo(30f).Within(EPS));
    }

    [Test]
    public void YawPitchDelta_AimDown_IsNegativePitch()
    {
        Vector3 down45 = Quaternion.Euler(45f, 0f, 0f) * Vector3.forward;
        Assert.That(AimCorrectionMath.YawPitchDelta(Vector3.forward, down45).y, Is.EqualTo(-45f).Within(EPS));
    }

    [Test]
    public void YawPitchDelta_VerticalDirection_HasNoYaw()
        => Assert.That(AimCorrectionMath.YawPitchDelta(Vector3.forward, Vector3.up).x, Is.EqualTo(0f));

    [Test]
    public void BoneStep_SplitsEvenlyOverBonesLeft()
    {
        Vector2 step = AimCorrectionMath.BoneStep(new Vector2(-45f, 30f), 3, 35f);
        Assert.That(step.x, Is.EqualTo(-15f).Within(EPS));
        Assert.That(step.y, Is.EqualTo(10f).Within(EPS));
    }

    [Test]
    public void BoneStep_ThreeBonesSumToTotal_UnderClamp()
    {
        Vector2 remaining = new Vector2(60f, -36f);
        Vector2 total = Vector2.zero;
        for (int left = 3; left > 0; left--)
        {
            Vector2 step = AimCorrectionMath.BoneStep(remaining, left, 35f);
            total += step;
            remaining -= step;
        }
        Assert.That(total.x, Is.EqualTo(60f).Within(EPS));
        Assert.That(total.y, Is.EqualTo(-36f).Within(EPS));
    }

    [Test]
    public void BoneStep_ClampsEachAxisPerBone()
    {
        Vector2 step = AimCorrectionMath.BoneStep(new Vector2(150f, -150f), 3, 35f);
        Assert.That(step.x, Is.EqualTo(35f).Within(EPS));
        Assert.That(step.y, Is.EqualTo(-35f).Within(EPS));
    }

    [Test]
    public void BoneStep_NegativeMax_IsZero()
        => Assert.That(AimCorrectionMath.BoneStep(new Vector2(30f, 10f), 3, -5f), Is.EqualTo(Vector2.zero));

    [Test]
    public void YawPitchDelta_NearVerticalTarget_HasNoYaw()
    {
        // 1 mm flat offset under a 5 m vertical target: yaw is undefined, must not snap to an arbitrary ±180°.
        Assert.That(AimCorrectionMath.YawPitchDelta(Vector3.forward, new Vector3(-0.001f, 5f, -0.001f)).x,
            Is.EqualTo(0f));
    }

    [Test]
    public void YawPitchDelta_ZeroCurrent_IsFinite()
    {
        Vector2 d = AimCorrectionMath.YawPitchDelta(Vector3.zero, Vector3.forward);
        Assert.That(float.IsNaN(d.x) || float.IsNaN(d.y), Is.False);
    }

    [Test]
    public void YawReachFactor_FullBelowThreshold() => Assert.That(AimCorrectionMath.YawReachFactor(-100f, 120f, 160f), Is.EqualTo(1f));

    [Test]
    public void YawReachFactor_ZeroBehind() => Assert.That(AimCorrectionMath.YawReachFactor(175f, 120f, 160f), Is.EqualTo(0f));

    [Test]
    public void YawReachFactor_FadesBetween()
        => Assert.That(AimCorrectionMath.YawReachFactor(-140f, 120f, 160f), Is.EqualTo(0.5f).Within(EPS));

    [Test]
    public void BoneStep_NoBonesLeft_IsZero()
        => Assert.That(AimCorrectionMath.BoneStep(new Vector2(30f, 10f), 0, 35f), Is.EqualTo(Vector2.zero));
}
