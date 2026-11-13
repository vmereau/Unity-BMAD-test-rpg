using Game.World;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for <see cref="DialogueSystem.ShouldCloseFor"/> (spec-npc-dialogue-close-request).</summary>
    public class DialogueCloseRequestTests
    {
        private readonly object _harl = new object();
        private readonly object _hobb = new object();

        [Test] public void OpenBySameNpc_Closes() => Assert.That(DialogueSystem.ShouldCloseFor(true, _harl, _harl), Is.True);

        [Test] public void OpenByOtherNpc_StaysOpen() => Assert.That(DialogueSystem.ShouldCloseFor(true, _hobb, _harl), Is.False);

        [Test] public void NothingOpen_Ignored() => Assert.That(DialogueSystem.ShouldCloseFor(false, _harl, _harl), Is.False);

        [Test] public void NoOpener_ForcedLine_Ignored() => Assert.That(DialogueSystem.ShouldCloseFor(true, null, _harl), Is.False);

        [Test] public void NullRequester_NoOpener_Ignored() => Assert.That(DialogueSystem.ShouldCloseFor(true, null, null), Is.False);
    }
}
