using System.Collections.Generic;
using Game.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The contract the game relies on in the generated warehouse prefab (Tools/World/Build Warehouse Prefab):
    /// the camp ramp's walk box the home yard's tarp is tied to, clear cart doorways and decks, night lanterns.
    /// </summary>
    public class WarehousePrefabTests
    {
        private const string PREFAB = "Assets/_Game/Prefabs/World/Warehouse/Warehouse.prefab";
        private const string RAMP_CAMP = "Ramp_Camp";
        private const float CART_WIDTH = 3.5f;
        private const float HEADROOM = 2.1f;       // the camp's doors: the player's capsule reaches 1.8 m
        private const float DOOR_X = 2f;           // both cart doorways, east of the hall's middle
        private const float DOOR_WIDTH = 2.4f;
        private const float HALF_DEPTH = 4f;
        // Far from anything a loaded scene holds, so only the prefab's own colliders answer the overlap tests.
        private static readonly Vector3 Away = new Vector3(0f, 5000f, 0f);
        private static readonly string[] Walks = { "Floor", "Landing", "Ramp_River", RAMP_CAMP };

        private GameObject _instance;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
            Assert.IsNotNull(prefab, $"{PREFAB} is missing: run Tools/World/Build Warehouse Prefab");
            _instance = Object.Instantiate(prefab, Away, Quaternion.identity);
            _instance.hideFlags = HideFlags.DontSave; // never saved into the open scene if a run is cut short
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_instance != null) Object.DestroyImmediate(_instance);
        }

        [Test]
        public void CampRamp_IsAnActiveScaledBox_DirectlyUnderTheRoot()
        {
            Transform ramp = _instance.transform.Find(RAMP_CAMP);
            Assert.IsNotNull(ramp, "HomeYardDresser ties the east yard's tarp to Warehouse/Ramp_Camp");
            Assert.IsTrue(ramp.gameObject.activeSelf);
            Assert.IsNotNull(ramp.GetComponent<BoxCollider>());
            Assert.AreEqual(Vector3.one, ramp.GetComponent<BoxCollider>().size, "its local ±0.5 must be the deck's faces");
            Assert.AreEqual(CART_WIDTH, ramp.localScale.x, 0.001f);
            Assert.AreEqual(0.2f, ramp.localScale.y, 0.001f);
            // The deck's length follows the ground at the ramp's foot: 13.2 m in plan, about 13.85 m today.
            Assert.That(ramp.localScale.z, Is.InRange(13.2f, 14.2f));
        }

        [Test]
        public void OverlapQueries_SeeThePrefabsColliders()
        {
            // The clearance tests below only prove something if a box through a wall is reported.
            var centre = Away + new Vector3(-3f, 2f, -(HALF_DEPTH - 0.2f));
            CollectionAssert.IsNotEmpty(Blockers(centre, new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity));
        }

        [Test]
        public void EveryWalkSurface_Exists()
        {
            foreach (string name in Walks)
            {
                Transform walk = _instance.transform.Find(name);
                Assert.IsNotNull(walk, name);
                Assert.IsNotNull(walk.GetComponent<BoxCollider>(), name);
            }
        }

        [Test]
        public void BothCartDoorways_AreClear()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var centre = Away + new Vector3(DOOR_X, 0.05f + HEADROOM * 0.5f, side * (HALF_DEPTH - 0.2f));
                var half = new Vector3(DOOR_WIDTH * 0.5f - 0.005f, HEADROOM * 0.5f, 0.4f);
                CollectionAssert.IsEmpty(Blockers(centre, half, Quaternion.identity), $"doorway on z {side * HALF_DEPTH}");
            }
        }

        [Test]
        public void Decks_AreClear_ACartWideAndHeadHigh()
        {
            foreach (string name in new[] { "Landing", "Ramp_River", RAMP_CAMP })
            {
                Transform walk = _instance.transform.Find(name);
                Assert.IsNotNull(walk, name);
                Vector3 size = walk.localScale;
                // From just over the deck's top up to head height, a little short of both ends.
                Vector3 centre = walk.position + walk.up * (size.y * 0.5f + 0.05f + HEADROOM * 0.5f);
                var half = new Vector3(size.x * 0.5f - 0.005f, HEADROOM * 0.5f, size.z * 0.5f - 0.3f);
                Assert.GreaterOrEqual(size.x, CART_WIDTH, name);
                CollectionAssert.IsEmpty(Blockers(centre, half, walk.rotation), name);
            }
        }

        [Test]
        public void Decks_HaveHandrails()
        {
            Transform rails = _instance.transform.Find("Colliders/Rails");
            Assert.IsNotNull(rails);
            // Camp ramp: both sides. River ramp: the bank side, and the hall side past its west end. Landing: two edges.
            Assert.GreaterOrEqual(rails.childCount, 6);
        }

        [Test]
        public void NothingPassesUnderTheHall()
        {
            // A box under each wall and one on the palisade line, from below the ground up to the floor.
            Transform rubble = _instance.transform.Find("Colliders/Rubble");
            Assert.IsNotNull(rubble);
            Assert.AreEqual(5, rubble.childCount);
            foreach (Transform box in rubble)
            {
                var collider = box.GetComponent<BoxCollider>();
                Assert.IsNotNull(collider, box.name);
                Assert.AreEqual(0f, box.localPosition.y + collider.size.y * 0.5f, 0.01f, $"{box.name} must reach the floor");
                Assert.Greater(collider.size.y, 4f, $"{box.name} must reach below the lowest ground");
            }
        }

        [Test]
        public void DoorLanterns_AreNightLights_OffByDefault()
        {
            int lanterns = 0;
            foreach (Transform child in _instance.transform)
            {
                if (!child.name.StartsWith("DoorLantern")) continue;
                lanterns++;
                Assert.IsNotNull(child.GetComponent<ClockNightLight>(), child.name);
                Light light = child.GetComponentInChildren<Light>(true);
                Assert.IsNotNull(light, child.name);
                Assert.IsFalse(light.enabled, "ClockNightLight switches it on at dusk");
            }
            Assert.AreEqual(2, lanterns, "one lantern by each cart door");
        }

        /// <summary>Solid colliders of the prefab in a box, walk surfaces left out.</summary>
        private List<string> Blockers(Vector3 centre, Vector3 half, Quaternion rotation)
        {
            var found = new List<string>();
            foreach (Collider collider in Physics.OverlapBox(centre, half, rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!collider.transform.IsChildOf(_instance.transform)) continue;
                if (collider.transform.parent == _instance.transform && System.Array.IndexOf(Walks, collider.name) >= 0) continue;
                found.Add($"{collider.transform.parent.name}/{collider.name}");
            }
            return found;
        }
    }
}
