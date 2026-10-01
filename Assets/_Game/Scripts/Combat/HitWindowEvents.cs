using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Pure event-list logic for hit windows authored as <c>HitboxEnable(id)</c> /
    /// <c>HitboxDisable(id)</c> animation events (used by the Hitbox Tuner).
    /// Time-unit agnostic: callers pass seconds for <c>.anim</c> clips and normalized [0..1] time for
    /// FBX importer clips. Never mutates the input array or its elements. EditMode-testable.
    /// </summary>
    public static class HitWindowEvents
    {
        public const string ENABLE_FUNCTION  = "HitboxEnable";
        public const string DISABLE_FUNCTION = "HitboxDisable";

        /// <summary>Seconds for a frame of a .anim clip. frameRate &lt;= 0 → 0.</summary>
        public static float FrameToSeconds(int frame, float frameRate) =>
            frameRate <= 0f ? 0f : frame / frameRate;

        /// <summary>
        /// Normalized [0..1] time for an FBX clip (importer events are normalized over
        /// firstFrame..lastFrame). <paramref name="frame"/> is relative to firstFrame.
        /// lastFrame &lt;= firstFrame → 0.
        /// </summary>
        public static float FrameToNormalized(int frame, float firstFrame, float lastFrame)
        {
            float range = lastFrame - firstFrame;
            return range <= 0f ? 0f : frame / range;
        }

        /// <summary>Inverse of <see cref="FrameToNormalized"/>, rounded, relative to firstFrame.</summary>
        public static int NormalizedToFrame(float normalized, float firstFrame, float lastFrame)
        {
            float range = lastFrame - firstFrame;
            return range <= 0f ? 0 : Mathf.RoundToInt(normalized * range);
        }

        /// <summary>Inverse of <see cref="FrameToSeconds"/>, rounded.</summary>
        public static int SecondsToFrame(float seconds, float frameRate) =>
            frameRate <= 0f ? 0 : Mathf.RoundToInt(seconds * frameRate);

        /// <summary>0 &lt;= startFrame &lt; endFrame &lt;= frameCount - 1. Returns false + a human-readable reason.</summary>
        public static bool IsValidWindow(int startFrame, int endFrame, int frameCount, out string reason)
        {
            if (startFrame < 0)
            {
                reason = $"Start frame {startFrame} is negative.";
                return false;
            }
            if (startFrame >= endFrame)
            {
                reason = $"Start frame {startFrame} must be before end frame {endFrame}.";
                return false;
            }
            if (endFrame > frameCount - 1)
            {
                reason = $"End frame {endFrame} is beyond the last frame of the clip ({frameCount - 1}).";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// New array: every event of <paramref name="existing"/> except HitboxEnable/HitboxDisable for
        /// <paramref name="hitboxId"/> (null treated as ""), plus Enable at startTime and Disable at
        /// endTime, stable-sorted by time. Other events are copied untouched.
        /// </summary>
        public static AnimationEvent[] ApplyWindow(AnimationEvent[] existing, string hitboxId, float startTime, float endTime)
        {
            string id = hitboxId ?? string.Empty;
            List<AnimationEvent> result = CopyWithout(existing, id);
            result.Add(new AnimationEvent { time = startTime, functionName = ENABLE_FUNCTION, stringParameter = id });
            result.Add(new AnimationEvent { time = endTime, functionName = DISABLE_FUNCTION, stringParameter = id });
            return StableSortByTime(result);
        }

        /// <summary>Removes the Enable/Disable events for <paramref name="hitboxId"/>; others untouched. Null input → empty array.</summary>
        public static AnimationEvent[] RemoveWindow(AnimationEvent[] existing, string hitboxId) =>
            CopyWithout(existing, hitboxId ?? string.Empty).ToArray();

        /// <summary>
        /// First Enable for <paramref name="hitboxId"/> and the first Disable for it at or after that
        /// time. False if either is absent.
        /// </summary>
        public static bool TryGetWindow(AnimationEvent[] events, string hitboxId, out float startTime, out float endTime)
        {
            startTime = 0f;
            endTime = 0f;
            if (events == null) return false;
            string id = hitboxId ?? string.Empty;

            bool foundStart = false;
            foreach (AnimationEvent evt in events)
            {
                if (evt == null || !IsWindowEvent(evt, id) || evt.functionName != ENABLE_FUNCTION) continue;
                if (!foundStart || evt.time < startTime) startTime = evt.time;
                foundStart = true;
            }
            if (!foundStart) return false;

            bool foundEnd = false;
            foreach (AnimationEvent evt in events)
            {
                if (evt == null || !IsWindowEvent(evt, id) || evt.functionName != DISABLE_FUNCTION) continue;
                if (evt.time < startTime) continue;
                if (!foundEnd || evt.time < endTime) endTime = evt.time;
                foundEnd = true;
            }
            return foundEnd;
        }

        private static bool IsWindowEvent(AnimationEvent evt, string id) =>
            (evt.functionName == ENABLE_FUNCTION || evt.functionName == DISABLE_FUNCTION) &&
            (evt.stringParameter ?? string.Empty) == id;

        private static List<AnimationEvent> CopyWithout(AnimationEvent[] existing, string id)
        {
            var result = new List<AnimationEvent>();
            if (existing == null) return result;
            foreach (AnimationEvent evt in existing)
            {
                if (evt == null || IsWindowEvent(evt, id)) continue;
                result.Add(Copy(evt));
            }
            return result;
        }

        private static AnimationEvent Copy(AnimationEvent evt) => new()
        {
            time = evt.time,
            functionName = evt.functionName,
            stringParameter = evt.stringParameter,
            floatParameter = evt.floatParameter,
            intParameter = evt.intParameter,
            objectReferenceParameter = evt.objectReferenceParameter,
            messageOptions = evt.messageOptions,
        };

        // List.Sort is unstable — insertion sort keeps authoring order for equal times.
        private static AnimationEvent[] StableSortByTime(List<AnimationEvent> events)
        {
            AnimationEvent[] array = events.ToArray();
            for (int i = 1; i < array.Length; i++)
            {
                AnimationEvent current = array[i];
                int j = i - 1;
                while (j >= 0 && array[j].time > current.time)
                {
                    array[j + 1] = array[j];
                    j--;
                }
                array[j + 1] = current;
            }
            return array;
        }
    }
}
