using Game.UI;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for <see cref="DialogueTranscript"/> (dialogue history above the NPC line).</summary>
    public class DialogueTranscriptTests
    {
        [Test]
        public void Add_PastCapacity_DropsOldest()
        {
            var t = new DialogueTranscript(2);
            t.Add("Guard", "one", false);
            t.Add("You", "two", true);
            t.Add("Guard", "three", false);

            string text = t.BuildRichText();
            Assert.That(t.Count, Is.EqualTo(2));
            Assert.That(text, Does.Not.Contain("one"));
            Assert.That(text.IndexOf("two"), Is.LessThan(text.IndexOf("three")), "oldest first");
        }

        [Test]
        public void Add_EmptyText_Ignored()
        {
            var t = new DialogueTranscript(3);
            t.Add("Guard", "  ", false);
            t.Add("Guard", null, false);
            Assert.That(t.Count, Is.EqualTo(0));
            Assert.That(t.BuildRichText(), Is.Empty);
        }

        [Test]
        public void Clear_RemovesAllLines()
        {
            var t = new DialogueTranscript(3);
            t.Add("You", "hello", true);
            t.Clear();
            Assert.That(t.Count, Is.EqualTo(0));
            Assert.That(t.BuildRichText(), Is.Empty);
        }

        [Test]
        public void BuildRichText_LabelsSpeakers()
        {
            var t = new DialogueTranscript(3);
            t.Add(DialogueTranscript.PLAYER_SPEAKER, "What are you guarding?", true);
            t.Add("Guard", "The gate.", false);
            string text = t.BuildRichText();
            Assert.That(text, Does.Contain("You</noparse>:</color>"));
            Assert.That(text, Does.Contain("Guard</noparse>:</color>"));
            Assert.That(text.Split('\n').Length, Is.EqualTo(2));
        }

        [Test]
        public void Capacity_BelowOne_ClampedToOne()
        {
            var t = new DialogueTranscript(0);
            Assert.That(t.Capacity, Is.EqualTo(1));
        }

        [Test]
        public void LineAlpha_NewestOpaqueOldestFaded_Monotonic()
        {
            const int capacity = 4;
            int previous = int.MaxValue;
            for (int age = 0; age < capacity; age++)
            {
                int alpha = DialogueTranscript.LineAlpha(age, capacity);
                Assert.That(alpha, Is.LessThan(previous), $"age {age}");
                previous = alpha;
            }
            Assert.That(DialogueTranscript.LineAlpha(0, capacity), Is.EqualTo(0xCC));
            Assert.That(DialogueTranscript.LineAlpha(capacity - 1, capacity), Is.EqualTo(0x66));
            Assert.That(DialogueTranscript.LineAlpha(10, capacity), Is.EqualTo(0x66));
            Assert.That(DialogueTranscript.LineAlpha(0, 1), Is.EqualTo(0xCC));
        }

        [Test]
        public void Add_ExactRepeatOfNewest_Ignored()
        {
            var t = new DialogueTranscript(3);
            t.Add("Trainer", "What do you want to learn?", false);
            t.Add("Trainer", "What do you want to learn?", false);
            Assert.That(t.Count, Is.EqualTo(1));
        }

        [Test]
        public void Add_LongText_CroppedWithEllipsis()
        {
            var t = new DialogueTranscript(3, 10);
            t.Add("Guard", "abcdefghijklmnop", false);
            Assert.That(t.BuildRichText(), Does.Contain("abcdefghi…"));
            Assert.That(t.BuildRichText(), Does.Not.Contain("abcdefghij"));
        }

        [Test]
        public void Crop_Boundaries()
        {
            Assert.That(DialogueTranscript.Crop("short", 0), Is.EqualTo("short"));
            Assert.That(DialogueTranscript.Crop("short", 5), Is.EqualTo("short"));
            Assert.That(DialogueTranscript.Crop("shorts", 5), Is.EqualTo("shor…"));
            Assert.That(DialogueTranscript.Crop("ab cd", 4), Is.EqualTo("ab…"), "trailing space trimmed before ellipsis");
        }

        [Test]
        public void BuildRichText_AuthoredMarkup_IsNoParsed()
        {
            var t = new DialogueTranscript(3);
            t.Add("Guard", "I <3 this </color> town", false);
            Assert.That(t.BuildRichText(), Does.Contain("<noparse>I <3 this </color> town</noparse>"));
        }
    }
}
