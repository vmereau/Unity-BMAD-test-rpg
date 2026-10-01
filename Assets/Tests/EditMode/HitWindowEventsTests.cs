using Game.Combat;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for HitWindowEvents — the pure event-list logic behind the Hitbox Tuner:
/// frame/time conversions, window validation, replace/keep/remove of HitboxEnable/HitboxDisable
/// events, immutability of the input and window lookup.
/// </summary>
public class HitWindowEventsTests
{
    private const float TOLERANCE = 1e-4f;

    private static AnimationEvent Evt(float time, string function, string stringParameter = "") =>
        new() { time = time, functionName = function, stringParameter = stringParameter };

    // --- Conversions ---

    [Test]
    public void FrameToSeconds_30fps_Frame15_ReturnsHalfSecond()
    {
        Assert.AreEqual(0.5f, HitWindowEvents.FrameToSeconds(15, 30f), TOLERANCE);
    }

    [Test]
    public void FrameToSeconds_ZeroFrameRate_ReturnsZero()
    {
        Assert.AreEqual(0f, HitWindowEvents.FrameToSeconds(15, 0f), TOLERANCE);
    }

    [Test]
    public void FrameToNormalized_MidFrame_ReturnsHalf()
    {
        Assert.AreEqual(0.5f, HitWindowEvents.FrameToNormalized(10, 0f, 20f), TOLERANCE);
    }

    [Test]
    public void FrameToNormalized_DegenerateRange_ReturnsZero()
    {
        Assert.AreEqual(0f, HitWindowEvents.FrameToNormalized(10, 20f, 20f), TOLERANCE);
    }

    [Test]
    public void NormalizedToFrame_RoundTrip_ReturnsSameFrame()
    {
        for (int frame = 0; frame <= 20; frame++)
        {
            float normalized = HitWindowEvents.FrameToNormalized(frame, 5f, 25f);
            Assert.AreEqual(frame, HitWindowEvents.NormalizedToFrame(normalized, 5f, 25f));
        }
    }

    [Test]
    public void SecondsToFrame_RoundTrip_ReturnsSameFrame()
    {
        for (int frame = 0; frame <= 40; frame++)
        {
            float seconds = HitWindowEvents.FrameToSeconds(frame, 30f);
            Assert.AreEqual(frame, HitWindowEvents.SecondsToFrame(seconds, 30f));
        }
    }

    // --- IsValidWindow ---

    [Test]
    public void IsValidWindow_StartNotBeforeEnd_False()
    {
        Assert.IsFalse(HitWindowEvents.IsValidWindow(10, 10, 21, out string reason));
        Assert.IsNotEmpty(reason);
    }

    [Test]
    public void IsValidWindow_EndBeyondClip_False()
    {
        Assert.IsFalse(HitWindowEvents.IsValidWindow(5, 21, 21, out string reason));
        Assert.IsNotEmpty(reason);
    }

    [Test]
    public void IsValidWindow_NegativeStart_False()
    {
        Assert.IsFalse(HitWindowEvents.IsValidWindow(-1, 5, 21, out string reason));
        Assert.IsNotEmpty(reason);
    }

    [Test]
    public void IsValidWindow_Valid_True()
    {
        Assert.IsTrue(HitWindowEvents.IsValidWindow(8, 20, 21, out string reason));
        Assert.IsEmpty(reason);
    }

    // --- ApplyWindow ---

    [Test]
    public void ApplyWindow_NullInput_AddsSortedPair()
    {
        AnimationEvent[] result = HitWindowEvents.ApplyWindow(null, "Bite", 0.4f, 0.7f);

        Assert.AreEqual(2, result.Length);
        Assert.AreEqual(HitWindowEvents.ENABLE_FUNCTION, result[0].functionName);
        Assert.AreEqual("Bite", result[0].stringParameter);
        Assert.AreEqual(0.4f, result[0].time, TOLERANCE);
        Assert.AreEqual(HitWindowEvents.DISABLE_FUNCTION, result[1].functionName);
        Assert.AreEqual("Bite", result[1].stringParameter);
        Assert.AreEqual(0.7f, result[1].time, TOLERANCE);
    }

    [Test]
    public void ApplyWindow_ReplacesExistingPairForSameId()
    {
        var existing = new[]
        {
            Evt(0.1f, HitWindowEvents.ENABLE_FUNCTION, "Bite"),
            Evt(0.2f, HitWindowEvents.DISABLE_FUNCTION, "Bite"),
        };

        AnimationEvent[] result = HitWindowEvents.ApplyWindow(existing, "Bite", 0.5f, 0.8f);

        Assert.AreEqual(2, result.Length);
        Assert.AreEqual(0.5f, result[0].time, TOLERANCE);
        Assert.AreEqual(0.8f, result[1].time, TOLERANCE);
    }

    [Test]
    public void ApplyWindow_KeepsPairsOfOtherIds()
    {
        var existing = new[]
        {
            Evt(0.1f, HitWindowEvents.ENABLE_FUNCTION, "Tail"),
            Evt(0.9f, HitWindowEvents.DISABLE_FUNCTION, "Tail"),
        };

        AnimationEvent[] result = HitWindowEvents.ApplyWindow(existing, "Bite", 0.4f, 0.6f);

        Assert.AreEqual(4, result.Length);
        Assert.AreEqual("Tail", result[0].stringParameter);
        Assert.AreEqual("Bite", result[1].stringParameter);
        Assert.AreEqual("Bite", result[2].stringParameter);
        Assert.AreEqual("Tail", result[3].stringParameter);
    }

    [Test]
    public void ApplyWindow_KeepsUnrelatedEvents_WithAllParameters()
    {
        var combo = new AnimationEvent
        {
            time = 0.5f,
            functionName = "ComboWindowOpen",
            stringParameter = "x",
            floatParameter = 1.5f,
            intParameter = 7,
            messageOptions = SendMessageOptions.DontRequireReceiver,
        };

        AnimationEvent[] result = HitWindowEvents.ApplyWindow(new[] { combo }, "Bite", 0.2f, 0.8f);

        Assert.AreEqual(3, result.Length);
        AnimationEvent kept = result[1];
        Assert.AreEqual("ComboWindowOpen", kept.functionName);
        Assert.AreEqual(0.5f, kept.time, TOLERANCE);
        Assert.AreEqual("x", kept.stringParameter);
        Assert.AreEqual(1.5f, kept.floatParameter, TOLERANCE);
        Assert.AreEqual(7, kept.intParameter);
        Assert.AreEqual(SendMessageOptions.DontRequireReceiver, kept.messageOptions);
    }

    [Test]
    public void ApplyWindow_NullId_TreatedAsEmpty()
    {
        var existing = new[]
        {
            Evt(0.1f, HitWindowEvents.ENABLE_FUNCTION, ""),
            Evt(0.2f, HitWindowEvents.DISABLE_FUNCTION, null),
        };

        AnimationEvent[] result = HitWindowEvents.ApplyWindow(existing, null, 0.3f, 0.6f);

        Assert.AreEqual(2, result.Length);
        Assert.AreEqual("", result[0].stringParameter);
        Assert.AreEqual(0.3f, result[0].time, TOLERANCE);
        Assert.AreEqual("", result[1].stringParameter);
        Assert.AreEqual(0.6f, result[1].time, TOLERANCE);
    }

    [Test]
    public void ApplyWindow_DoesNotMutateInput()
    {
        AnimationEvent enable = Evt(0.1f, HitWindowEvents.ENABLE_FUNCTION, "Bite");
        AnimationEvent other = Evt(0.5f, "ComboWindowOpen");
        var existing = new[] { enable, other };

        AnimationEvent[] result = HitWindowEvents.ApplyWindow(existing, "Bite", 0.3f, 0.6f);

        Assert.AreEqual(2, existing.Length);
        Assert.AreSame(enable, existing[0]);
        Assert.AreSame(other, existing[1]);
        Assert.AreEqual(0.1f, enable.time, TOLERANCE);
        CollectionAssert.DoesNotContain(result, other); // copied, not shared
    }

    // --- RemoveWindow ---

    [Test]
    public void RemoveWindow_RemovesOnlyMatchingId()
    {
        var existing = new[]
        {
            Evt(0.1f, HitWindowEvents.ENABLE_FUNCTION, "Bite"),
            Evt(0.2f, HitWindowEvents.ENABLE_FUNCTION, "Tail"),
            Evt(0.3f, "ComboWindowOpen"),
            Evt(0.4f, HitWindowEvents.DISABLE_FUNCTION, "Bite"),
            Evt(0.5f, HitWindowEvents.DISABLE_FUNCTION, "Tail"),
        };

        AnimationEvent[] result = HitWindowEvents.RemoveWindow(existing, "Bite");

        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("Tail", result[0].stringParameter);
        Assert.AreEqual("ComboWindowOpen", result[1].functionName);
        Assert.AreEqual("Tail", result[2].stringParameter);
    }

    // --- TryGetWindow ---

    [Test]
    public void TryGetWindow_ReturnsTimes()
    {
        AnimationEvent[] events = HitWindowEvents.ApplyWindow(null, "Bite", 0.4f, 0.7f);

        Assert.IsTrue(HitWindowEvents.TryGetWindow(events, "Bite", out float start, out float end));
        Assert.AreEqual(0.4f, start, TOLERANCE);
        Assert.AreEqual(0.7f, end, TOLERANCE);
    }

    [Test]
    public void TryGetWindow_Absent_ReturnsFalse()
    {
        AnimationEvent[] events = HitWindowEvents.ApplyWindow(null, "Tail", 0.4f, 0.7f);

        Assert.IsFalse(HitWindowEvents.TryGetWindow(events, "Bite", out _, out _));
        Assert.IsFalse(HitWindowEvents.TryGetWindow(null, "Bite", out _, out _));
    }
}
