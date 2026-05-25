using NUnit.Framework;

/// <summary>
/// Edit Mode tests for the Entity warning-state decision logic and the Entity SO
/// warning-range clamp (Entity.OnValidate). Pure formula simulation — no MonoBehaviour
/// lifecycle, no NavMesh. Pattern mirrors EnemyBrainStateTests.
/// </summary>
public class EntityWarningStateTests
{
    private enum Reaction { Engage, Warn }
    private enum WarnTick { Hold, Engage, Cancel }

    // Mirrors Entity.OnValidate clamp.
    private float ClampWarningRange(float warningRange, float detectionRange)
    {
        if (warningRange < 0f) warningRange = 0f;
        if (warningRange >= detectionRange)
        {
            warningRange = detectionRange - 0.5f;
            if (warningRange < 0f) warningRange = 0f;
        }
        return warningRange;
    }

    // Mirrors EntityBrain.RespondToDetectedPlayer.
    private Reaction RespondToDetectedPlayer(bool engageImmediately, float dist, float warningRange)
    {
        if (engageImmediately) return Reaction.Engage;
        return dist <= warningRange ? Reaction.Engage : Reaction.Warn;
    }

    // Mirrors EntityBrain.HandleWarning per-frame decision (escape > inner-ring > timer > hold).
    private WarnTick EvaluateWarning(float dist, float warningRange, float detectionRange, float timer)
    {
        if (dist > detectionRange) return WarnTick.Cancel;  // player escaped
        if (dist <= warningRange) return WarnTick.Engage;   // crossed inner ring
        if (timer <= 0f) return WarnTick.Engage;            // timer elapsed
        return WarnTick.Hold;
    }

    // --- OnValidate clamp (AC7) ---

    [Test]
    public void Clamp_NegativeWarning_ClampsToZero()
        => Assert.That(ClampWarningRange(-2f, 8f), Is.EqualTo(0f));

    [Test]
    public void Clamp_WarningEqualsDetection_ClampsBelow()
        => Assert.That(ClampWarningRange(8f, 8f), Is.EqualTo(7.5f));

    [Test]
    public void Clamp_WarningAboveDetection_ClampsBelow()
        => Assert.That(ClampWarningRange(20f, 8f), Is.EqualTo(7.5f));

    [Test]
    public void Clamp_ValidWarning_Unchanged()
        => Assert.That(ClampWarningRange(5f, 8f), Is.EqualTo(5f));

    [Test]
    public void Clamp_TinyDetection_DoesNotGoNegative()
        => Assert.That(ClampWarningRange(5f, 0.2f), Is.EqualTo(0f));

    // --- First-contact reaction (AC1/AC3/AC4) ---

    [Test]
    public void Respond_EngageImmediately_AlwaysEngages()
        => Assert.That(RespondToDetectedPlayer(true, 7f, 5f), Is.EqualTo(Reaction.Engage));

    [Test]
    public void Respond_InWarningBand_Warns()
        => Assert.That(RespondToDetectedPlayer(false, 7f, 5f), Is.EqualTo(Reaction.Warn));

    [Test]
    public void Respond_InsideWarningRange_Engages()
        => Assert.That(RespondToDetectedPlayer(false, 4f, 5f), Is.EqualTo(Reaction.Engage));

    [Test]
    public void Respond_ExactlyAtWarningRange_Engages()
        => Assert.That(RespondToDetectedPlayer(false, 5f, 5f), Is.EqualTo(Reaction.Engage));

    // --- Per-frame warning evaluation (AC2/AC3/AC5) ---

    [Test]
    public void Warning_PlayerEscaped_Cancels()
        => Assert.That(EvaluateWarning(9f, 5f, 8f, 2f), Is.EqualTo(WarnTick.Cancel));

    [Test]
    public void Warning_CrossedInnerRing_Engages()
        => Assert.That(EvaluateWarning(4f, 5f, 8f, 2f), Is.EqualTo(WarnTick.Engage));

    [Test]
    public void Warning_TimerElapsed_Engages()
        => Assert.That(EvaluateWarning(7f, 5f, 8f, 0f), Is.EqualTo(WarnTick.Engage));

    [Test]
    public void Warning_InBandTimerRemaining_Holds()
        => Assert.That(EvaluateWarning(7f, 5f, 8f, 2f), Is.EqualTo(WarnTick.Hold));

    [Test]
    public void Warning_EscapeTakesPriorityOverTimer()
        => Assert.That(EvaluateWarning(9f, 5f, 8f, 0f), Is.EqualTo(WarnTick.Cancel));
}
