using System.Collections.Generic;
using System.IO;
using Game.Editor.World;
using Game.World;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The contract the game relies on in the generated dock gate prefab (Tools/World/Build Dock Gate Prefab):
    /// a cart passes through the open gateway, nothing of the leaves, the lean-to or the fire basket stands in the
    /// way, and the fire is a night light.
    /// </summary>
    public class DockGateTests
    {
        private const string PREFAB = "Assets/_Game/Prefabs/World/DockGate/DockGate.prefab";
        private const string PARTS = "Assets/_Game/Art/Environment/Camp/models/dock gate/DockGate.json";
        private const float CART_HEIGHT = 2.8f;   // the warehouse's cart doorways
        // Far from anything a loaded scene holds, so only the prefab's own colliders answer the overlap tests.
        private static readonly Vector3 Away = new Vector3(0f, 5000f, 300f);

        private GameObject _instance;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
            Assert.IsNotNull(prefab, $"{PREFAB} is missing: run Tools/World/Build Dock Gate Prefab");
            _instance = Object.Instantiate(prefab, Away, Quaternion.identity);
            _instance.hideFlags = HideFlags.DontSave; // never saved into the open scene if a run is cut short
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
        }

        private static DockGateBuilder.GateFacts Gate()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            Assert.IsTrue(DockGateBuilder.TryReadGate(parts, out DockGateBuilder.GateFacts gate, out string problem), problem);
            return gate;
        }

        [Test]
        public void Gateway_IsACartWideAndHigh_AndClearAlongThePath()
        {
            DockGateBuilder.GateFacts gate = Gate();   // opening: min z, max z, height
            Assert.GreaterOrEqual(gate.Opening.y - gate.Opening.x, 2.5f, "between the posts");
            Assert.GreaterOrEqual(gate.Opening.z, CART_HEIGHT);
            // From 3 m inside the camp to 7 m along the bank path: the leaves stand open beside it, the lean-to and
            // the fire basket south of it.
            float tall = CART_HEIGHT * 0.5f - 0.03f;
            float halfWidth = (gate.Opening.y - gate.Opening.x) * 0.5f - 0.05f;
            var centre = Away + new Vector3(2f, 0.05f + tall, (gate.Opening.x + gate.Opening.y) * 0.5f);
            CollectionAssert.IsEmpty(Blockers(centre, new Vector3(5f, tall, halfWidth)), "the gateway and the path beyond it");
        }

        [Test]
        public void Gateway_HasPostsALintelAndTwoLeaves()
        {
            DockGateBuilder.GateFacts gate = Gate();
            Assert.IsNotNull(_instance.transform.Find("Colliders/Gate/Post_South"));
            Assert.IsNotNull(_instance.transform.Find("Colliders/Gate/Post_North"));
            Assert.IsNotNull(_instance.transform.Find("Colliders/Leaves/Leaf_South"));
            Assert.IsNotNull(_instance.transform.Find("Colliders/Leaves/Leaf_North"));
            Assert.IsTrue(Physics.Raycast(Away + new Vector3(0f, 1f, 0f), Vector3.up, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore), "no lintel");
            Assert.AreEqual(gate.Opening.z, hit.point.y - Away.y, 0.05f, "the lintel's underside is the opening's height");
            // No way round the posts inside the gate's own width: a ray along the wall's line meets each post.
            foreach (Vector3 way in new[] { Vector3.forward, Vector3.back })
                Assert.IsTrue(Physics.Raycast(Away + new Vector3(0f, 1f, 0f), way, 2.5f, ~0, QueryTriggerInteraction.Ignore), $"no post toward {way}");
        }

        [Test]
        public void Fire_IsANightLight_OffByDefault_InTheBasket()
        {
            DockGateBuilder.GateFacts gate = Gate();
            Transform fire = _instance.transform.Find("BrazierFire");
            Assert.IsNotNull(fire);
            Assert.IsNotNull(fire.GetComponent<ClockNightLight>());
            Light light = fire.GetComponentInChildren<Light>(true);
            Assert.IsNotNull(light);
            Assert.IsFalse(light.enabled, "ClockNightLight switches it on at dusk");
            Assert.AreEqual(gate.Fire, fire.localPosition);
            Transform basket = _instance.transform.Find("Colliders/Brazier/Brazier");
            Assert.IsNotNull(basket, "the basket is solid");
            Assert.AreEqual(gate.Fire.x, basket.localPosition.x, 0.01f);
            Assert.AreEqual(gate.Fire.z, basket.localPosition.z, 0.01f);
        }

        [Test]
        public void ModelParts_StayOutOfTheBake_AndLeavesAreNotWalkable()
        {
            // The test assembly does not reference Unity.AI.Navigation: the components are read by name.
            foreach (Transform part in _instance.transform.Find("Model"))
            {
                Component modifier = part.GetComponent("NavMeshModifier");
                Assert.IsNotNull(modifier, part.name);
                Assert.IsTrue(new SerializedObject(modifier).FindProperty("m_IgnoreFromBuild").boolValue, part.name);
            }
            Transform volumes = _instance.transform.Find("NavMesh");
            Assert.IsNotNull(volumes);
            Assert.GreaterOrEqual(volumes.childCount, 4, "two leaves, the bench, the fire basket");
            foreach (Transform volume in volumes)
            {
                Component modifier = volume.GetComponent("NavMeshModifierVolume");
                Assert.IsNotNull(modifier, volume.name);
                Assert.AreEqual(1, new SerializedObject(modifier).FindProperty("m_Area").intValue, $"{volume.name} must be Not Walkable");
            }
        }

        [TestCase("{}")]
        [TestCase("{\"version\": 2, \"objects\": [], \"colliders\": [], \"notWalkable\": [], \"opening\": [-1, 1, 3], \"fire\": [0, 0, 0]}")]
        [TestCase("{\"version\": 1, \"objects\": [], \"colliders\": [], \"opening\": [-1, 1, 3], \"fire\": [0, 0, 0]}")]
        [TestCase("{\"version\": 1, \"objects\": [], \"colliders\": [], \"notWalkable\": [], \"opening\": [1, -1, 3], \"fire\": [0, 0, 0]}")]
        [TestCase("{\"version\": 1, \"objects\": [], \"colliders\": [], \"notWalkable\": [], \"opening\": [-1, 1, 3], \"fire\": [0, 0]}")]
        [TestCase("{\"version\": 1, \"objects\": [], \"colliders\": [{\"name\": \"x\", \"centre\": [0, 0, 0], \"size\": [1, 1]}], \"notWalkable\": [], \"opening\": [-1, 1, 3], \"fire\": [0, 0, 0]}")]
        public void MalformedParts_AreRefused_WithoutAThrow(string json)
        {
            Assert.IsFalse(DockGateBuilder.TryReadGate(JObject.Parse(json), out _, out string problem));
            Assert.IsNotEmpty(problem);
        }

        [Test]
        public void NullParts_AreRefused()
        {
            Assert.IsFalse(DockGateBuilder.TryReadGate(null, out _, out string problem));
            Assert.IsNotEmpty(problem);
        }

        /// <summary>Solid colliders of the prefab in a box.</summary>
        private List<string> Blockers(Vector3 centre, Vector3 half)
        {
            var found = new List<string>();
            foreach (Collider collider in Physics.OverlapBox(centre, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (collider.transform.IsChildOf(_instance.transform)) found.Add($"{collider.transform.parent.name}/{collider.name}");
            return found;
        }
    }
}
