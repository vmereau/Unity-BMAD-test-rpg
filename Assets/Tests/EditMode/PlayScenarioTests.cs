using Game.Core;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class PlayScenarioTests
    {
        [TestCase(PlayScenario.OpenScenes, ArrivalStartOverride.None)]
        [TestCase(PlayScenario.MainMenu, ArrivalStartOverride.Play)]
        [TestCase(PlayScenario.Arrival, ArrivalStartOverride.Play)]
        [TestCase(PlayScenario.AfterArrival, ArrivalStartOverride.SkipAll)]
        public void ArrivalOverrideFor_MapsEachScenario(PlayScenario scenario, ArrivalStartOverride expected) =>
            Assert.AreEqual(expected, PlayScenarios.ArrivalOverrideFor(scenario));

        [TestCase(PlayScenario.OpenScenes, null)]
        [TestCase(PlayScenario.MainMenu, null)]
        [TestCase(PlayScenario.Arrival, PlayScenarios.WretchOriginId)]
        [TestCase(PlayScenario.AfterArrival, PlayScenarios.WretchOriginId)]
        public void OriginFor_ForcesWretchOnlyForArrivalScenarios(PlayScenario scenario, string expected) =>
            Assert.AreEqual(expected, PlayScenarios.OriginFor(scenario));

        [Test]
        public void Active_OutsidePlayMode_IsOpenScenes() =>
            Assert.AreEqual(PlayScenario.OpenScenes, PlayScenarios.Active);
    }
}
