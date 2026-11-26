using System.Collections.Generic;
using System.IO;
using Game.World;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The contract the game relies on in the generated cookhouse prefab (Tools/World/Build Cookhouse Prefab):
    /// walkable floor, clear doorways, a hatch nobody walks through, a store closed off from the kitchen except
    /// by its door, the restricted volume inside the store, night lights and the mess tables.
    /// </summary>
    public class CookhousePrefabTests
    {
        private const string PREFAB = "Assets/_Game/Prefabs/World/Cookhouse/Cookhouse.prefab";
        private const string PARTS = "Assets/_Game/Art/Environment/Camp/models/cookhouse/Cookhouse.json";
        private const float MIN_HEAD = 2.1f;      // the camp's doors: the player's capsule reaches 1.8 m
        private const float MIN_DOOR = 1f;
        private const float SKIN = 0.03f;         // jambs, an open leaf's hinge edge and the lintel's underside
        // Far from anything a loaded scene holds, so only the prefab's own colliders answer the overlap tests.
        private static readonly Vector3 Away = new Vector3(0f, 5000f, 0f);
        private static readonly string[] Walks = { "Floor", "Step_Kitchen", "Step_Delivery" };

        // Doorways, from the parts file's anchors (from, to, head, the wall's line); true = the wall runs along x.
        private static readonly (string anchor, bool alongX)[] Doors =
        {
            ("kitchenDoor", true), ("innerDoor", true), ("deliveryDoor", false),
        };

        private GameObject _instance;
        private JObject _anchors;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
            Assert.IsNotNull(prefab, $"{PREFAB} is missing: run Tools/World/Build Cookhouse Prefab");
            Assert.IsTrue(File.Exists(PARTS), $"{PARTS} is missing: run build_cookhouse.py");
            _anchors = (JObject)JObject.Parse(File.ReadAllText(PARTS))["anchors"];
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
        public void EveryWalkSurface_Exists_AndTheFloorsTopIsTheOrigin()
        {
            foreach (string name in Walks)
            {
                Transform walk = _instance.transform.Find(name);
                Assert.IsNotNull(walk, name);
                Assert.IsNotNull(walk.GetComponent<BoxCollider>(), name);
            }
            Transform floor = _instance.transform.Find("Floor");
            Assert.AreEqual(0f, floor.localPosition.y + floor.localScale.y * 0.5f, 0.001f);
        }

        [Test]
        public void Doorways_AreClear_AMetreWideAndHeadHigh()
        {
            foreach ((string anchor, bool alongX) in Doors)
            {
                float[] door = Floats(anchor);   // from, to, head, line
                Assert.GreaterOrEqual(door[1] - door[0], MIN_DOOR - 0.001f, anchor + " width");
                Assert.GreaterOrEqual(door[2], MIN_HEAD - 0.001f, anchor + " head");
                // The whole opening, less a skin, from just over the floor to just under the head, through the wall.
                float mid = (door[0] + door[1]) * 0.5f, halfWidth = (door[1] - door[0]) * 0.5f - SKIN, halfHeight = (door[2] - 0.05f - SKIN) * 0.5f;
                var centre = Away + (alongX ? new Vector3(mid, 0.05f + halfHeight, door[3]) : new Vector3(door[3], 0.05f + halfHeight, mid));
                var half = alongX ? new Vector3(halfWidth, halfHeight, 0.2f) : new Vector3(0.2f, halfHeight, halfWidth);
                CollectionAssert.IsEmpty(Blockers(centre, half), anchor);
            }
        }

        [Test]
        public void Hatch_IsNotAWayIn()
        {
            float[] hatch = Floats("hatch");   // z from, z to, sill, head
            var centre = Away + new Vector3(2.63f, hatch[2] * 0.5f, (hatch[0] + hatch[1]) * 0.5f);
            CollectionAssert.Contains(Blockers(centre, new Vector3(0.05f, hatch[2] * 0.4f, 0.5f)), "Walls/Wall_East_Sill", "the wall under the hatch's sill");
            Assert.GreaterOrEqual(hatch[2], 0.9f, "a sill too high to step over");
        }

        [Test]
        public void Partition_ClosesTheStore_ExceptAtItsDoor()
        {
            foreach (float x in new[] { -2f, -0.85f, 0.5f })
                CollectionAssert.IsNotEmpty(Blockers(Away + new Vector3(x, 1f, 0.92f), new Vector3(0.1f, 0.5f, 0.05f)), $"partition at x {x}");
        }

        [Test]
        public void RestrictedBox_LiesInsideTheStore_ClearOfTheKitchen()
        {
            float[] box = Floats("storeBox");           // centre x, y, z, size x, y, z
            float[] store = Floats("storeFloor");       // min x, max x, min z, max z
            float[] kitchen = Floats("kitchenFloor");
            Assert.GreaterOrEqual(box[0] - box[3] * 0.5f, store[0] - 0.001f);
            Assert.LessOrEqual(box[0] + box[3] * 0.5f, store[1] + 0.001f);
            Assert.GreaterOrEqual(box[2] - box[5] * 0.5f, store[2] - 0.001f);
            Assert.LessOrEqual(box[2] + box[5] * 0.5f, store[3] + 0.001f);
            Assert.Greater(box[2] - box[5] * 0.5f, kitchen[3], "the box must not reach into the kitchen");
            // Standing in the inner doorway (on the kitchen's side of the boards) is not trespassing: the area tests a point.
            Assert.Less(Floats("innerDoor")[3], box[2] - box[5] * 0.5f);
        }

        [Test]
        public void CookingSpot_BedAndCounter_AreOutsideTheRestrictedBox()
        {
            float[] box = Floats("storeBox");
            foreach (string name in new[] { "station", "bed", "behindCounter", "frontOfHatch", "byTheHearth" })
            {
                float[] at = Floats(name);
                bool inside = Mathf.Abs(at[0] - box[0]) <= box[3] * 0.5f && Mathf.Abs(at[2] - box[2]) <= box[5] * 0.5f;
                Assert.IsFalse(inside, name);
            }
        }

        [Test]
        public void NightLights_AreClockDriven_AndDarkByDefault()
        {
            ClockNightLight[] lights = _instance.GetComponentsInChildren<ClockNightLight>(true);
            Assert.AreEqual(3, lights.Length, "the hatch lantern, the hearth and the porch fire");
            foreach (Light light in _instance.GetComponentsInChildren<Light>(true)) Assert.IsFalse(light.enabled, light.transform.parent.name);
        }

        [Test]
        public void MessTables_AreSixSolidSets()
        {
            Transform tables = _instance.transform.Find("Tables");
            Assert.IsNotNull(tables);
            Assert.AreEqual(6, tables.childCount);
            foreach (Transform table in tables) Assert.AreEqual(3, table.GetComponents<BoxCollider>().Length, table.name);
        }

        private float[] Floats(string anchor)
        {
            var list = _anchors[anchor] as JArray;
            Assert.IsNotNull(list, $"{PARTS} has no anchor '{anchor}'");
            var values = new float[list.Count];
            for (int i = 0; i < values.Length; i++) values[i] = (float)list[i];
            return values;
        }

        private List<string> Blockers(Vector3 centre, Vector3 half)
        {
            var found = new List<string>();
            foreach (Collider collider in Physics.OverlapBox(centre, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!collider.transform.IsChildOf(_instance.transform)) continue;
                if (collider.transform.parent == _instance.transform && System.Array.IndexOf(Walks, collider.name) >= 0) continue;
                found.Add($"{collider.transform.parent.name}/{collider.name}");
            }
            return found;
        }
    }
}
