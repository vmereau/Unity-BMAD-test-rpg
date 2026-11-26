using Game.Player;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for the third-person camera rig rules — CameraRigMath.
/// </summary>
public class CameraRigMathTests
{
    private const float EPS = 0.001f;

    [Test]
    public void LookUpDistanceScale_Level_IsOne()
        => Assert.That(CameraRigMath.LookUpDistanceScale(0f, -70f, 0.5f), Is.EqualTo(1f).Within(EPS));

    [Test]
    public void LookUpDistanceScale_FullLookUp_IsMinScale()
        => Assert.That(CameraRigMath.LookUpDistanceScale(-70f, -70f, 0.5f), Is.EqualTo(0.5f).Within(EPS));

    [Test]
    public void LookUpDistanceScale_HalfLookUp_IsLinear()
        => Assert.That(CameraRigMath.LookUpDistanceScale(-35f, -70f, 0.5f), Is.EqualTo(0.75f).Within(EPS));

    [Test]
    public void LookUpDistanceScale_LookDown_IsOne()
        => Assert.That(CameraRigMath.LookUpDistanceScale(60f, -70f, 0.5f), Is.EqualTo(1f).Within(EPS));

    [Test]
    public void EffectivePitchMin_NotSwimming_IsPitchMin()
        => Assert.That(CameraRigMath.EffectivePitchMin(-70f, false, -10f), Is.EqualTo(-70f));

    [Test]
    public void EffectivePitchMin_Swimming_IsSwimLimit()
        => Assert.That(CameraRigMath.EffectivePitchMin(-70f, true, -10f), Is.EqualTo(-10f));

    [Test]
    public void EffectivePitchMin_SwimLimitBeyondPitchMin_KeepsPitchMin()
        => Assert.That(CameraRigMath.EffectivePitchMin(-5f, true, -10f), Is.EqualTo(-5f));

    [Test]
    public void BlendRig_NoAim_KeepsBase()
    {
        CameraRigMath.BlendRig(new Vector3(0.3f, 0.5f, 0f), 4.5f, new Vector3(0.6f, 0.3f, -1.8f), 0f,
            out Vector3 shoulder, out float distance);
        Assert.That(Vector3.Distance(shoulder, new Vector3(0.3f, 0.5f, 0f)), Is.LessThan(EPS));
        Assert.That(distance, Is.EqualTo(4.5f).Within(EPS));
    }

    [Test]
    public void BlendRig_FullAim_UsesAimOffset()
    {
        CameraRigMath.BlendRig(new Vector3(0.3f, 0.5f, 0f), 4.5f, new Vector3(0.6f, 0.3f, -1.8f), 1f,
            out Vector3 shoulder, out float distance);
        Assert.That(Vector3.Distance(shoulder, new Vector3(0.6f, 0.3f, 0f)), Is.LessThan(EPS));
        Assert.That(distance, Is.EqualTo(1.8f).Within(EPS));
    }

    [Test]
    public void BlendRig_PositiveAimZ_ClampsToMinArm()
    {
        CameraRigMath.BlendRig(new Vector3(0.3f, 0.5f, 0f), 4.5f, new Vector3(0.6f, 0.3f, 1f), 1f,
            out _, out float distance);
        Assert.That(distance, Is.EqualTo(CameraRigMath.MinArmDistance).Within(EPS));
    }

    [Test]
    public void RigDistanceScale_FullAim_IgnoresLookUp()
        => Assert.That(CameraRigMath.RigDistanceScale(-70f, -70f, 0.5f, 1f), Is.EqualTo(1f).Within(EPS));

    [Test]
    public void RigDistanceScale_NoAim_IsLookUpScale()
        => Assert.That(CameraRigMath.RigDistanceScale(-70f, -70f, 0.5f, 0f), Is.EqualTo(0.5f).Within(EPS));

    [Test]
    public void StepPitchFloor_EnteringWater_EasesPitchUpByStep()
    {
        float floor = -70f, pitch = -40f;
        CameraRigMath.StepPitchFloor(ref floor, ref pitch, -5f, 3f);
        Assert.That(floor, Is.EqualTo(-67f).Within(EPS));
        Assert.That(pitch, Is.EqualTo(-40f).Within(EPS)); // floor still below the pitch: no change yet

        for (int i = 0; i < 12; i++) CameraRigMath.StepPitchFloor(ref floor, ref pitch, -5f, 3f);
        Assert.That(floor, Is.EqualTo(-31f).Within(EPS));
        Assert.That(pitch, Is.EqualTo(-31f).Within(EPS)); // pushed up with the floor, never past it in one step
    }

    [Test]
    public void StepPitchFloor_ReachesTarget_AndStops()
    {
        float floor = -6f, pitch = -6f;
        CameraRigMath.StepPitchFloor(ref floor, ref pitch, -5f, 3f);
        Assert.That(floor, Is.EqualTo(-5f).Within(EPS));
        Assert.That(pitch, Is.EqualTo(-5f).Within(EPS));
    }

    [Test]
    public void StepPitchFloor_LeavingWater_NeverPullsPitchDown()
    {
        float floor = -5f, pitch = 10f;
        CameraRigMath.StepPitchFloor(ref floor, ref pitch, -70f, 3f);
        Assert.That(floor, Is.EqualTo(-8f).Within(EPS));
        Assert.That(pitch, Is.EqualTo(10f).Within(EPS));
    }

    [Test]
    public void BlendRig_HalfAim_IsMidway()
    {
        CameraRigMath.BlendRig(new Vector3(0.3f, 0.5f, 0f), 4.5f, new Vector3(0.6f, 0.3f, -1.8f), 0.5f,
            out Vector3 shoulder, out float distance);
        Assert.That(shoulder.x, Is.EqualTo(0.45f).Within(EPS));
        Assert.That(shoulder.y, Is.EqualTo(0.4f).Within(EPS));
        Assert.That(distance, Is.EqualTo(3.15f).Within(EPS));
    }

    // --- Mouse-wheel zoom (min 1.5, max 6, step 0.5) ---

    [TestCase(4.5f, 1f, 4.0f, TestName = "StepZoomTarget_WheelForward_ZoomsIn")]
    [TestCase(4.5f, -1f, 5.0f, TestName = "StepZoomTarget_WheelBack_ZoomsOut")]
    [TestCase(1.5f, 1f, 1.5f, TestName = "StepZoomTarget_AtMin_StaysAtMin")]
    [TestCase(6.0f, -1f, 6.0f, TestName = "StepZoomTarget_AtMax_StaysAtMax")]
    [TestCase(4.5f, 120f, 4.0f, TestName = "StepZoomTarget_PlatformRange_IsOneStep")]
    [TestCase(4.5f, -120f, 5.0f, TestName = "StepZoomTarget_PlatformRangeBack_IsOneStep")]
    [TestCase(4.5f, 0f, 4.5f, TestName = "StepZoomTarget_NoWheel_Unchanged")]
    [TestCase(1.7f, 1f, 1.5f, TestName = "StepZoomTarget_PartialStepPastMin_Clamped")]
    public void StepZoomTarget(float target, float wheel, float expected)
        => Assert.That(CameraRigMath.StepZoomTarget(target, wheel, 0.5f, 1.5f, 6f), Is.EqualTo(expected).Within(EPS));

    [Test]
    public void EaseZoom_MovesTowardsTarget_WithoutOvershoot()
    {
        float next = CameraRigMath.EaseZoom(4.5f, 4.0f, 10f, 0.016f);
        Assert.That(next, Is.LessThan(4.5f));
        Assert.That(next, Is.GreaterThan(4.0f));
    }

    [Test]
    public void EaseZoom_LargeStep_NeverOvershoots()
        => Assert.That(CameraRigMath.EaseZoom(4.5f, 4.0f, 10f, 10f), Is.EqualTo(4.0f).Within(EPS));

    [Test]
    public void EaseZoom_Converges_AndSnapsToTarget()
    {
        float d = 4.5f;
        for (int i = 0; i < 300; i++) d = CameraRigMath.EaseZoom(d, 1.5f, 10f, 0.016f);
        Assert.That(d, Is.EqualTo(1.5f));
    }

    [Test]
    public void EaseZoom_ZeroDeltaTime_Unchanged()
        => Assert.That(CameraRigMath.EaseZoom(4.5f, 4.0f, 10f, 0f), Is.EqualTo(4.5f).Within(EPS));
}
