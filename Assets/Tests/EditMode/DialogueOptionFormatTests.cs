using System.Collections.Generic;
using Game.Dialogue;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for <see cref="DialogueOptionFormat"/> and <see cref="ChoiceDialogueNode.ChoiceSeenId"/>.</summary>
    public class DialogueOptionFormatTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        private T Node<T>(DialogueNode next = null) where T : DialogueNode
        {
            var node = ScriptableObject.CreateInstance<T>();
            node.nextNode = next;
            _created.Add(node);
            return node;
        }

        // ── Classify ──────────────────────────────────────────────────────────

        [Test]
        public void Classify_ChainToShop_IsTrade()
        {
            var start = Node<StartDialogueNode>(Node<TextDialogueNode>(Node<ShopDialogueNode>()));
            Assert.That(DialogueOptionFormat.Classify(start), Is.EqualTo(DialogueOptionTag.Trade));
        }

        [Test]
        public void Classify_ChainToTeach_IsTeach()
        {
            var start = Node<StartDialogueNode>(Node<TeachChoiceDialogueNode>());
            Assert.That(DialogueOptionFormat.Classify(start), Is.EqualTo(DialogueOptionTag.Teach));
        }

        [Test]
        public void Classify_StopsAtChoiceNode()
        {
            var start = Node<StartDialogueNode>(Node<ChoiceDialogueNode>(Node<ShopDialogueNode>()));
            Assert.That(DialogueOptionFormat.Classify(start), Is.EqualTo(DialogueOptionTag.None));
        }

        [Test]
        public void Classify_PlainChainAndNull_AreNone()
        {
            Assert.That(DialogueOptionFormat.Classify(Node<StartDialogueNode>(Node<TextDialogueNode>())), Is.EqualTo(DialogueOptionTag.None));
            Assert.That(DialogueOptionFormat.Classify(null), Is.EqualTo(DialogueOptionTag.None));
        }

        [Test]
        public void Classify_Loop_Terminates()
        {
            var a = Node<TextDialogueNode>();
            var b = Node<TextDialogueNode>(a);
            a.nextNode = b;
            Assert.That(DialogueOptionFormat.Classify(a), Is.EqualTo(DialogueOptionTag.None));
        }

        [Test]
        public void Classify_ShopPastStepLimit_IsNone()
        {
            DialogueNode node = Node<ShopDialogueNode>();
            for (int i = 0; i < DialogueOptionFormat.MAX_CLASSIFY_STEPS; i++)
                node = Node<TextDialogueNode>(node);
            Assert.That(DialogueOptionFormat.Classify(node), Is.EqualTo(DialogueOptionTag.None));
        }

        // ── Label ─────────────────────────────────────────────────────────────

        [Test]
        public void Label_KeyTagAndEscapedText()
        {
            string label = DialogueOptionFormat.Label("1", "Show me <b>wares</b>", DialogueOptionTag.Trade, false);
            Assert.That(label, Does.StartWith("<color=#B8A988>1.</color> "));
            Assert.That(label, Does.Contain("[Trade]</color> "));
            Assert.That(label, Does.Contain("<noparse>Show me <b>wares</b></noparse>"));
        }

        [Test]
        public void Label_NoKey_NoTag_NotSeen_IsPlainEscapedText()
        {
            Assert.That(DialogueOptionFormat.Label(null, "Hello", DialogueOptionTag.None, false), Is.EqualTo("<noparse>Hello</noparse>"));
        }

        [Test]
        public void Label_Seen_IsDimmed()
        {
            string label = DialogueOptionFormat.Label("2", "Hello", DialogueOptionTag.None, true);
            Assert.That(label, Does.Contain("<color=#B8A98899><noparse>Hello</noparse></color>"));
        }

        [Test]
        public void Label_RichSuffix_AppendedUnescaped()
        {
            string label = DialogueOptionFormat.Label("3", "Train", DialogueOptionTag.None, false, " (<color=#A8473B>5g</color>)");
            Assert.That(label, Does.EndWith("</noparse> (<color=#A8473B>5g</color>)"));
        }

        // ── CostLabel ─────────────────────────────────────────────────────────

        [Test]
        public void CostLabel_OnlyShortGoldIsRed()
        {
            var choice = new TeachChoiceOption { statPoints = 10, goldCost = 50 };
            Assert.That(DialogueOptionFormat.CostLabel(choice, TeachCostShortfall.Gold),
                Is.EqualTo(" (10 LP, <color=#A8473B>50g</color>)"));
        }

        [Test]
        public void CostLabel_Affordable_HasNoColour()
        {
            var choice = new TeachChoiceOption { statPoints = 2, goldCost = 0 };
            Assert.That(DialogueOptionFormat.CostLabel(choice, TeachCostShortfall.None), Is.EqualTo(" (2 LP)"));
        }

        [Test]
        public void CostLabel_FreeChoice_IsEmpty()
        {
            var choice = new TeachChoiceOption { statPoints = 0, goldCost = 0 };
            Assert.That(DialogueOptionFormat.CostLabel(choice, TeachCostShortfall.None), Is.Empty);
        }

        // ── OrderUnseenFirst ──────────────────────────────────────────────────

        [Test]
        public void OrderUnseenFirst_IsStablePartition()
        {
            var items = new[] { "a*", "b", "c*", "d" };
            var ordered = DialogueOptionFormat.OrderUnseenFirst(items, s => s.EndsWith("*"));
            Assert.That(ordered, Is.EqualTo(new[] { "b", "d", "a*", "c*" }));
        }

        [Test]
        public void OrderUnseenFirst_Null_IsEmpty()
        {
            Assert.That(DialogueOptionFormat.OrderUnseenFirst<string>(null, _ => false), Is.Empty);
        }

        // ── ChoiceSeenId ──────────────────────────────────────────────────────

        [Test]
        public void ChoiceSeenId_NoNodeId_IsEmpty()
        {
            var node = Node<ChoiceDialogueNode>();
            var choice = new ChoiceOption();
            node.choices = new[] { choice };
            // A CreateInstance node is not an asset: its id is never synced.
            Assert.That(node.ChoiceSeenId(choice), Is.Empty);
        }

        [Test]
        public void ChoiceSeenId_IsNodeIdDotIndex_EmptyForForeignChoice()
        {
            var node = Node<ChoiceDialogueNode>();
            var first = new ChoiceOption();
            var second = new ChoiceOption();
            node.choices = new[] { first, second };
            var so = new UnityEditor.SerializedObject(node);
            so.FindProperty("_id").stringValue = "abc";
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(node.ChoiceSeenId(second), Is.EqualTo("abc.1"));
            Assert.That(node.ChoiceSeenId(new ChoiceOption()), Is.Empty);
        }
    }
}
