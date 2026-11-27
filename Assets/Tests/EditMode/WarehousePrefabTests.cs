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
        private static readonly string[] Walks = { "Floor", "Landing", "Ramp_River", RAMP_CAMP, "Cellar", "CellarSide", "CellarPassage", "Tally", "Quay" };
        private const string PARTS = "Assets/_Game/Art/Environment/Camp/models/warehouse/Warehouse.json";
        private const float PLAYER_RADIUS = 0.3f;

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
            // A box under each wall and one on the palisade line, from below the ground up to the floor. The river
            // wall's box and the palisade line's are each in two pieces: the cellar lies between them, closed by its
            // own walls. The east wall's is in three: the passage to the cellar's side room goes under it, and the
            // side room is closed by its own walls and the retaining wall.
            Transform rubble = _instance.transform.Find("Colliders/Rubble");
            Assert.IsNotNull(rubble);
            Assert.AreEqual(9, rubble.childCount);
            Transform west = rubble.Find("Skirt"), east = rubble.Find("Skirt_Line_East");
            Assert.IsNotNull(west, "the palisade line under the floor stays closed");
            Assert.IsNotNull(east, "the palisade line under the floor stays closed");
            // From each gable to the cellar's inside face: its side walls stand behind those faces.
            WarehouseBuilder.CellarFacts cellar = Cellar();
            var westBox = west.GetComponent<BoxCollider>();
            var eastBox = east.GetComponent<BoxCollider>();
            Assert.AreEqual(-6f, west.localPosition.x - westBox.size.x * 0.5f, 0.01f, "from the west gable");
            Assert.AreEqual(cellar.Inside.xMin, west.localPosition.x + westBox.size.x * 0.5f, 0.01f);
            Assert.AreEqual(cellar.Inside.xMax, east.localPosition.x - eastBox.size.x * 0.5f, 0.01f);
            Assert.AreEqual(6f, east.localPosition.x + eastBox.size.x * 0.5f, 0.01f, "to the east gable");
            foreach (Transform box in rubble)
            {
                var collider = box.GetComponent<BoxCollider>();
                Assert.IsNotNull(collider, box.name);
                Assert.AreEqual(0f, box.localPosition.y + collider.size.y * 0.5f, 0.01f, $"{box.name} must reach the floor");
                if (box.name == "Skirt_East_Header") continue; // over the passage: it comes down to the passage's head
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

        // ------------------------------------------------------------------------------------ cellar

        private static WarehouseBuilder.CellarFacts Cellar()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            Assert.IsTrue(WarehouseBuilder.TryReadCellar(parts, out WarehouseBuilder.CellarFacts cellar, out string problem), problem);
            return cellar;
        }

        [Test]
        public void CellarDoorway_IsADoorWideAndHeadHigh()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            var opening = (JArray)parts["cellar"]["door"]["opening"];   // min x, max x, height
            float from = (float)opening[0], to = (float)opening[1], height = (float)opening[2];
            WarehouseBuilder.CellarFacts cellar = Cellar();
            Assert.GreaterOrEqual(to - from, 1f, "the camp's doorways are at least 1 m wide");
            Assert.GreaterOrEqual(height, HEADROOM);

            // With the leaf swung away, a player-wide box passes through the wall from the step to the room.
            Transform door = _instance.transform.Find("CellarDoor");
            Assert.IsNotNull(door, "the dock door is an instance of the game's door prefab");
            door.gameObject.SetActive(false);
            Physics.SyncTransforms();
            // From just over the threshold up to the camp's 2.1 m of headroom.
            float tall = HEADROOM * 0.5f - 0.03f;
            var centre = Away + new Vector3((from + to) * 0.5f, cellar.FloorY + 0.05f + tall, cellar.Inside.yMax + 0.15f);
            var half = new Vector3(PLAYER_RADIUS, tall, 0.6f);
            CollectionAssert.IsEmpty(Blockers(centre, half, Quaternion.identity), "the doorway");
        }

        [Test]
        public void CellarDoor_IsUnlocked_SwingsIn_AndKeepsItsSaveId()
        {
            Transform door = _instance.transform.Find("CellarDoor");
            Assert.IsNotNull(door);
            Assert.IsNotNull(door.GetComponent<DoorInteractable>());
            Assert.IsFalse(new SerializedObject(door.GetComponent<Lockable>()).FindProperty("_isLocked").boolValue, "locking it is a later spec");
            var saveable = door.GetComponent<SaveableObject>();
            Assert.IsNotNull(saveable);
            Assert.AreEqual("area1-warehouse-cellar-door", new SerializedObject(saveable).FindProperty("_saveId").stringValue);
            // The leaf hangs on the east jamb and lies west of it when shut.
            WarehouseBuilder.CellarFacts cellar = Cellar();
            Assert.Less((door.rotation * Vector3.right).x, -0.99f);
            Assert.AreEqual(cellar.DoorPosition.x, door.localPosition.x, 0.001f);
        }

        [Test]
        public void CellarFloor_IsClear_BetweenItsPillars()
        {
            WarehouseBuilder.CellarFacts cellar = Cellar();
            Transform walk = _instance.transform.Find("Cellar");
            Assert.IsNotNull(walk);
            Assert.AreEqual(cellar.FloorY, walk.localPosition.y + walk.localScale.y * 0.5f, 0.01f, "the walk box's top is the floor");
            // The walk from the door to the back: in from the door, west between the two rows of pillars (a hall
            // post stands straight ahead of the door), then south between the hall's posts and the beam's to the back wall.
            float y = cellar.FloorY + 0.1f + HEADROOM * 0.5f, tall = HEADROOM * 0.5f - 0.02f;
            var legs = new (string name, Vector3 centre, Vector3 half)[]
            {
                ("in from the door", new Vector3(3.45f, y, 5.6f), new Vector3(PLAYER_RADIUS, tall, 1.3f)),
                ("between the rows", new Vector3(2.72f, y, 4.45f), new Vector3(1.03f, tall, PLAYER_RADIUS)),
                ("to the back", new Vector3(2f, y, (cellar.Inside.yMin + 0.3f + 4.6f) * 0.5f), new Vector3(PLAYER_RADIUS, tall, (4.6f - cellar.Inside.yMin - 0.3f) * 0.5f)),
            };
            foreach ((string name, Vector3 centre, Vector3 half) in legs)
                CollectionAssert.IsEmpty(Blockers(Away + centre, half, Quaternion.identity), name);
        }

        [Test]
        public void Cellar_IsClosedOnEverySide()
        {
            WarehouseBuilder.CellarFacts cellar = Cellar();
            var middle = Away + new Vector3(cellar.Inside.center.x + 1.2f, cellar.FloorY + 1f, cellar.Inside.center.y);
            foreach (Vector3 way in new[] { Vector3.left, Vector3.right, Vector3.back })
                Assert.IsTrue(Physics.Raycast(middle, way, 8f, ~0, QueryTriggerInteraction.Ignore), $"no wall toward {way}");
            // North is the front wall: a ray west of the doorway meets it.
            var westOfDoor = Away + new Vector3(2.4f, cellar.FloorY + 1f, cellar.Inside.center.y);
            Assert.IsTrue(Physics.Raycast(westOfDoor, Vector3.forward, 8f, ~0, QueryTriggerInteraction.Ignore), "no front wall");
        }

        [Test]
        public void CellarHatch_IsSolidFloor()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            var hatch = (JArray)parts["cellar"]["hatch"];   // min x, max x, min z, max z
            var over = Away + new Vector3(((float)hatch[0] + (float)hatch[1]) * 0.5f, 1f, ((float)hatch[2] + (float)hatch[3]) * 0.5f);
            Assert.IsTrue(Physics.Raycast(over, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore));
            Assert.AreEqual("Floor", hit.collider.name, "opening the hatch is a later spec");
            Assert.AreEqual(Away.y, hit.point.y, 0.01f);
        }

        [Test]
        public void CellarLanterns_OneNightLightOutside_TwoAlwaysOnInside()
        {
            Transform outside = _instance.transform.Find("CellarLantern_Door");
            Transform inside = _instance.transform.Find("CellarLantern_Inside");
            Assert.IsNotNull(outside);
            Assert.IsNotNull(inside);
            Assert.IsNotNull(outside.GetComponent<ClockNightLight>());
            Assert.IsFalse(outside.GetComponentInChildren<Light>(true).enabled, "ClockNightLight switches it on at dusk");
            Transform back = _instance.transform.Find("CellarLantern_Back");
            Assert.IsNotNull(back);
            WarehouseBuilder.CellarFacts cellar = Cellar();
            foreach (Transform lantern in new[] { inside, back })
            {
                Assert.IsNull(lantern.GetComponent<ClockNightLight>(), "the room has no window: its lanterns burn at all hours");
                Assert.IsTrue(lantern.GetComponentInChildren<Light>(true).enabled);
                Assert.AreNotEqual(LightShadows.None, lantern.GetComponentInChildren<Light>(true).shadows, "unshadowed, it lights the dock through the wall");
                Assert.IsTrue(cellar.Inside.Contains(new Vector2(lantern.localPosition.x, lantern.localPosition.z)), lantern.name);
            }
            Assert.Greater(outside.localPosition.z, cellar.Inside.yMax);
        }

        [Test]
        public void CellarRestrictedBox_AndHole_StayInsideTheWalls()
        {
            WarehouseBuilder.CellarFacts cellar = Cellar();
            Rect area = Rect.MinMaxRect(cellar.AreaCentre.x - cellar.AreaSize.x * 0.5f, cellar.AreaCentre.z - cellar.AreaSize.z * 0.5f,
                cellar.AreaCentre.x + cellar.AreaSize.x * 0.5f, cellar.AreaCentre.z + cellar.AreaSize.z * 0.5f);
            Assert.GreaterOrEqual(area.xMin, cellar.Inside.xMin);
            Assert.LessOrEqual(area.xMax, cellar.Inside.xMax);
            Assert.GreaterOrEqual(area.yMin, cellar.Inside.yMin);
            Assert.LessOrEqual(area.yMax, cellar.Inside.yMax);
            Assert.Less(cellar.AreaCentre.y + cellar.AreaSize.y * 0.5f, 0f, "under the hall's floor: the hall has its own area");

            // The hole's edge is jagged on the terrain's cell grid: it must fall inside a wall's collider on each
            // side, 0.5 m off the floor, and never north of the front wall's inner face.
            Assert.Less(cellar.Hole.yMax, cellar.Inside.yMax, "a hole past the front wall would open the shelf outside");
            float y = cellar.FloorY + 0.5f;
            var probes = new[]
            {
                // (the east probe stands south of the passage to the side room, whose own hole carries on there)
                new Vector3(cellar.Hole.xMin, y, cellar.Inside.center.y), new Vector3(cellar.Hole.xMax, y, cellar.Inside.center.y - 1f),
                new Vector3(cellar.Hole.xMin, y, cellar.Inside.yMax - 0.3f), new Vector3(cellar.Hole.xMax, y, cellar.Inside.yMax - 0.3f),
                new Vector3(cellar.Inside.center.x, y, cellar.Hole.yMin + 0.3f),
            };
            foreach (Vector3 probe in probes)
                CollectionAssert.IsNotEmpty(Blockers(Away + probe, Vector3.one * 0.02f, Quaternion.identity), $"hole edge at {probe} is not inside a wall");
        }

        [Test]
        public void HoleCells_AreTheCellsWhollyInsideTheRectangle()
        {
            // Area 1's terrain: 1000 m from (-500, -500), 1024 cells. The rectangle of the build brief.
            var world = Rect.MinMaxRect(13.6f, 49.15f, 20.6f, 58.95f);
            RectInt cells = WarehouseBuilder.HoleCells(world, new Vector2(-500f, -500f), 1000f, 1024);
            Assert.AreEqual(new RectInt(526, 563, 7, 9), cells);
            float cell = 1000f / 1024f;
            Assert.GreaterOrEqual(-500f + cells.xMin * cell, world.xMin);
            Assert.LessOrEqual(-500f + cells.xMax * cell, world.xMax);
            Assert.AreEqual(0, WarehouseBuilder.HoleCells(Rect.MinMaxRect(13.7f, 51.8f, 14.5f, 52.6f), new Vector2(-500f, -500f), 1000f, 1024).width,
                "a rectangle narrower than a cell holds none");
            Assert.AreEqual(0, WarehouseBuilder.HoleCells(world, new Vector2(-500f, -500f), 0f, 1024).width);
        }

        [TestCase("{}")]
        [TestCase("{\"cellar\": 3}")]
        [TestCase("{\"cellar\": {\"hole\": [0, 0, 1], \"inside\": [0, 0, 1, 1], \"floorY\": -4}}")]
        [TestCase("{\"cellar\": {\"hole\": [1, 0, 0, 1], \"inside\": [0, 0, 1, 1], \"floorY\": -4}}")]
        [TestCase("{\"cellar\": {\"hole\": [0, 0, 1, 1], \"inside\": [0, 0, 1, 1], \"floorY\": \"low\"}}")]
        [TestCase("{\"cellar\": {\"hole\": [0, 0, 1, 1], \"inside\": [0, 0, 1, 1], \"floorY\": -4, \"door\": {\"position\": [0, 0], \"yaw\": 0, \"scaleY\": 1, \"openAngle\": 90}}}")]
        [TestCase("{\"cellar\": {\"hole\": [0, 0, 1, 1], \"inside\": [0, 0, 1, 1], \"floorY\": -4, \"door\": {\"position\": [0, 0, 0], \"yaw\": 0, \"scaleY\": 0, \"openAngle\": 90}}}")]
        [TestCase("{\"cellar\": {\"hole\": [0, 0, 1, 1], \"inside\": [0, 0, 1, 1], \"floorY\": -4, \"door\": {\"position\": [0, 0, 0], \"yaw\": 0, \"scaleY\": 1, \"openAngle\": 90}, \"restricted\": {\"centre\": [0, 0, 0], \"size\": [1, 0, 1]}}}")]
        public void MalformedCellar_IsRefused_WithoutAThrow(string json)
        {
            Assert.IsFalse(WarehouseBuilder.TryReadCellar(JObject.Parse(json), out _, out string problem));
            Assert.IsNotEmpty(problem);
        }

        [Test]
        public void CutCellarHole_OpensOnlyTheCellarsCells()
        {
            var holes = new bool[1024, 1024];
            for (int z = 0; z < 1024; z++)
                for (int x = 0; x < 1024; x++)
                    holes[z, x] = true;
            Assert.IsTrue(WarehouseBuilder.CutCellarHole(holes, new Vector2(-500f, -500f), 1000f));
            int open = 0;
            foreach (bool solid in holes)
                if (!solid) open++;
            Assert.That(open, Is.InRange(61, 82), "about 7 x 9 cells of 0.98 m, and 3 x 4 for the side room");
            Assert.IsFalse(holes[567, 534], "the middle of the side room");
            Assert.IsTrue(holes[567, 536], "the dock spur's logs, east of the side room");
            Assert.IsTrue(holes[565, 534], "the palisade's line, south of the side room");
            Assert.IsTrue(holes[570, 534], "the foot of the retaining wall");
            Assert.IsFalse(holes[568, 529], "the middle of the room");
            Assert.IsTrue(holes[573, 529], "the dock shelf in front of the door");
            Assert.IsFalse(holes[563, 529], "under the hall, south of the palisade line");
            Assert.IsTrue(holes[561, 529], "behind the back wall");
        }

        // ------------------------------------------------------------------------------------ quay, tally room, side room

        private static WarehouseBuilder.QuayFacts Quay()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            Assert.IsTrue(WarehouseBuilder.TryReadQuay(parts, out WarehouseBuilder.QuayFacts quay, out string problem), problem);
            return quay;
        }

        [Test]
        public void TallyDoorway_IsADoorWideAndHeadHigh()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            var opening = (JArray)parts["tally"]["door"]["opening"];   // min z, max z, height
            float from = (float)opening[0], to = (float)opening[1], height = (float)opening[2];
            Assert.GreaterOrEqual(to - from, 1f, "the camp's doorways are at least 1 m wide");
            Assert.GreaterOrEqual(height, HEADROOM);

            // With the leaf swung away, a player-wide box passes through the gable from the hall into the room.
            Transform door = _instance.transform.Find("TallyDoor");
            Assert.IsNotNull(door, "the tally room's door is an instance of the game's door prefab");
            door.gameObject.SetActive(false);
            Physics.SyncTransforms();
            float tall = HEADROOM * 0.5f - 0.03f;
            var centre = Away + new Vector3(6f, 0.05f + tall, (from + to) * 0.5f);
            CollectionAssert.IsEmpty(Blockers(centre, new Vector3(0.9f, tall, PLAYER_RADIUS), Quaternion.identity), "the doorway");
        }

        [Test]
        public void TallyDoor_IsUnlocked_AndKeepsItsSaveId()
        {
            Transform door = _instance.transform.Find("TallyDoor");
            Assert.IsNotNull(door);
            Assert.IsNotNull(door.GetComponent<DoorInteractable>());
            Assert.IsFalse(new SerializedObject(door.GetComponent<Lockable>()).FindProperty("_isLocked").boolValue, "locking it is a later spec");
            Assert.AreEqual("area1-warehouse-tally-door", new SerializedObject(door.GetComponent<SaveableObject>()).FindProperty("_saveId").stringValue);
            // The leaf hangs on the south jamb and lies north of it when shut, in the gable's line.
            Assert.Greater((door.rotation * Vector3.right).z, 0.99f);
            Assert.AreEqual(Quay().TallyDoor.Position.z, door.localPosition.z, 0.001f);
        }

        [Test]
        public void TallyRoom_HasAFloor_APlankWall_AndARoof()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            var inside = (JArray)parts["tally"]["inside"];   // min x, min z, max x, max z
            var middle = Away + new Vector3(((float)inside[0] + (float)inside[2]) * 0.5f, 1f, ((float)inside[1] + (float)inside[3]) * 0.5f);
            Assert.IsTrue(Physics.Raycast(middle, Vector3.down, out RaycastHit floor, 3f, ~0, QueryTriggerInteraction.Ignore));
            Assert.AreEqual("Tally", floor.collider.name);
            Assert.AreEqual(Away.y, floor.point.y, 0.01f, "its floor is the hall's");
            // The palisade's panels are its south and east walls (the scene's); the model closes the north and the top.
            Assert.IsTrue(Physics.Raycast(middle, Vector3.forward, 4f, ~0, QueryTriggerInteraction.Ignore), "no plank wall toward the quay");
            Assert.IsTrue(Physics.Raycast(middle, Vector3.up, out RaycastHit roof, 4f, ~0, QueryTriggerInteraction.Ignore), "no roof");
            Assert.GreaterOrEqual(roof.point.y - Away.y, HEADROOM);

            WarehouseBuilder.QuayFacts quay = Quay();
            Assert.GreaterOrEqual(quay.TallyAreaCentre.x - quay.TallyAreaSize.x * 0.5f, (float)inside[0]);
            Assert.LessOrEqual(quay.TallyAreaCentre.x + quay.TallyAreaSize.x * 0.5f, (float)inside[2]);
            Assert.GreaterOrEqual(quay.TallyAreaCentre.z - quay.TallyAreaSize.z * 0.5f, (float)inside[1]);
            Assert.LessOrEqual(quay.TallyAreaCentre.z + quay.TallyAreaSize.z * 0.5f, (float)inside[3]);
            Assert.Greater(quay.TallyAreaCentre.y - quay.TallyAreaSize.y * 0.5f, -0.5f, "above the cellar's side room, which has its own box");
        }

        [Test]
        public void Quay_IsAWalkSurface_WithAClearCartLane()
        {
            WarehouseBuilder.QuayFacts quay = Quay();
            Transform walk = _instance.transform.Find("Quay");
            Assert.IsNotNull(walk);
            Assert.AreEqual(quay.DeckY, walk.localPosition.y + walk.localScale.y * 0.5f, 0.01f, "the walk box's top is the deck");
            Assert.AreEqual(quay.Deck.width, walk.localScale.x, 0.01f);
            // The deck's south 2 m, over its whole length, a cart high: nothing solid reaches into it.
            float tall = 1.4f;
            var centre = Away + new Vector3(quay.Deck.center.x, quay.DeckY + 0.05f + tall, quay.Deck.yMin + 1f);
            CollectionAssert.IsEmpty(Blockers(centre, new Vector3(quay.Deck.width * 0.5f - 0.05f, tall, 1f), Quaternion.identity), "the cart lane");
            // ... and the goods Sites places keep north of it (their pivots, with a crate's reach).
            foreach ((string kit, Vector3 position, _, _, _) in quay.Goods)
                if (position.z < quay.Deck.yMax)
                    Assert.Greater(position.z, quay.Deck.yMin + 2f + 0.5f, $"{kit} at {position} stands in the cart lane");
            Assert.GreaterOrEqual(quay.Goods.Count, 15);
        }

        [Test]
        public void QuayLanterns_AreNightLights_OffByDefault()
        {
            WarehouseBuilder.QuayFacts quay = Quay();
            foreach (string name in new[] { "QuayLantern_West", "QuayLantern_East" })
            {
                Transform lantern = _instance.transform.Find(name);
                Assert.IsNotNull(lantern, name);
                Assert.IsNotNull(lantern.GetComponent<ClockNightLight>(), name);
                Assert.IsFalse(lantern.GetComponentInChildren<Light>(true).enabled, "ClockNightLight switches it on at dusk");
                Assert.Greater(lantern.localPosition.y - quay.DeckY, 1.9f, "over a walker's head");
                Assert.Greater(lantern.localPosition.z, quay.Deck.yMin + 2f, "not over the cart lane");
            }
        }

        [Test]
        public void SideRoom_IsReachedThroughAPassage_AndClosedBeyond()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            var passage = (JArray)parts["cellar"]["side"]["passage"];   // min x, min z, max x, max z
            var room = (JArray)parts["cellar"]["side"]["inside"];
            float height = (float)parts["cellar"]["side"]["passageHeight"], top = (float)parts["cellar"]["side"]["top"];
            WarehouseBuilder.CellarFacts cellar = Cellar();
            Assert.GreaterOrEqual((float)passage[3] - (float)passage[1], 1f, "the camp's doorways are at least 1 m wide");
            Assert.GreaterOrEqual(height, HEADROOM);
            Assert.GreaterOrEqual(top - cellar.FloorY, HEADROOM, "the side room's ceiling");

            // A player-wide box passes from the cellar through the wall into the side room.
            float tall = HEADROOM * 0.5f - 0.03f;
            float z = ((float)passage[1] + (float)passage[3]) * 0.5f;
            var centre = Away + new Vector3(((float)passage[0] + (float)room[0]) * 0.5f, cellar.FloorY + 0.05f + tall, z);
            var half = new Vector3(((float)room[0] - (float)passage[0]) * 0.5f + 0.4f, tall, PLAYER_RADIUS);
            CollectionAssert.IsEmpty(Blockers(centre, half, Quaternion.identity), "the passage");
            foreach (string name in new[] { "CellarSide", "CellarPassage" })
            {
                Transform walk = _instance.transform.Find(name);
                Assert.IsNotNull(walk, name);
                Assert.AreEqual(cellar.FloorY, walk.localPosition.y + walk.localScale.y * 0.5f, 0.01f, name);
            }

            // The room is closed on its other three sides and overhead.
            var middle = Away + new Vector3(((float)room[0] + (float)room[2]) * 0.5f, cellar.FloorY + 1f, ((float)room[1] + (float)room[3]) * 0.5f);
            foreach (Vector3 way in new[] { Vector3.right, Vector3.forward, Vector3.back, Vector3.up })
                Assert.IsTrue(Physics.Raycast(middle, way, 4f, ~0, QueryTriggerInteraction.Ignore), $"no wall toward {way}");
        }

        [Test]
        public void SideRoom_HoleAndRestrictedBoxes_StayInsideItsWalls()
        {
            JObject parts = JObject.Parse(File.ReadAllText(PARTS));
            var room = (JArray)parts["cellar"]["side"]["inside"];
            WarehouseBuilder.CellarFacts cellar = Cellar();
            Assert.Greater(cellar.SideHole.width, 0f, "the side room needs its own terrain hole");
            Assert.AreEqual(2, cellar.MoreAreas.Count);
            foreach ((string name, Vector3 centre, Vector3 size) in cellar.MoreAreas)
            {
                Assert.Less(centre.y + size.y * 0.5f, -1f, $"{name}: under the tally room, which has its own box");
                Assert.GreaterOrEqual(centre.z - size.z * 0.5f, (float)room[1], name);
                Assert.LessOrEqual(centre.z + size.z * 0.5f, (float)room[3], name);
                Assert.LessOrEqual(centre.x + size.x * 0.5f, (float)room[2], name);
            }
            // The hole's edge falls inside a wall's collider on each side, 0.5 m off the floor.
            float y = cellar.FloorY + 0.5f;
            float midX = ((float)room[0] + (float)room[2]) * 0.5f, midZ = ((float)room[1] + (float)room[3]) * 0.5f;
            var probes = new[]
            {
                new Vector3(cellar.SideHole.xMax, y, midZ), new Vector3(midX, y, cellar.SideHole.yMin), new Vector3(midX, y, cellar.SideHole.yMax),
                new Vector3(cellar.SideHole.xMax, y, cellar.SideHole.yMin), new Vector3(cellar.SideHole.xMax, y, cellar.SideHole.yMax),
            };
            foreach (Vector3 probe in probes)
                CollectionAssert.IsNotEmpty(Blockers(Away + probe, Vector3.one * 0.02f, Quaternion.identity), $"hole edge at {probe} is not inside a wall");

            // Area 1's terrain: the cells this rectangle opens, at the warehouse's place.
            var world = new Rect(15f + cellar.SideHole.xMin, 52f + cellar.SideHole.yMin, cellar.SideHole.width, cellar.SideHole.height);
            Assert.AreEqual(new RectInt(533, 566, 3, 4), WarehouseBuilder.HoleCells(world, new Vector2(-500f, -500f), 1000f, 1024));
        }

        [Test]
        public void SideRoomLantern_IsAlwaysOn_WithShadows()
        {
            Transform lantern = _instance.transform.Find("CellarLantern_Side");
            Assert.IsNotNull(lantern);
            Assert.IsNull(lantern.GetComponent<ClockNightLight>(), "the room has no window");
            Assert.IsTrue(lantern.GetComponentInChildren<Light>(true).enabled);
            Assert.AreNotEqual(LightShadows.None, lantern.GetComponentInChildren<Light>(true).shadows, "unshadowed, it lights the tally room through its floor");
        }

        private const string GOOD_TALLY = "\"tally\": {\"door\": {\"position\": [0, 0, 0], \"yaw\": 0, \"scaleY\": 1, \"openAngle\": 90}, \"restricted\": {\"centre\": [0, 0, 0], \"size\": [1, 1, 1]}}";

        [TestCase("{}")]
        [TestCase("{\"tally\": {}, \"quay\": 3}")]
        [TestCase("{\"tally\": {\"door\": {\"position\": [0, 0, 0], \"yaw\": 0, \"scaleY\": 0, \"openAngle\": 90}}, \"quay\": {}}")]
        [TestCase("{\"tally\": {\"door\": {\"position\": [0, 0, 0], \"yaw\": 0, \"scaleY\": 1, \"openAngle\": 90}, \"restricted\": {\"centre\": [0, 0, 0], \"size\": [1, -1, 1]}}, \"quay\": {}}")]
        [TestCase("{" + GOOD_TALLY + ", \"quay\": {\"restricted\": {\"centre\": [0, 0, 0], \"size\": [1, 1, 1]}, \"deckY\": \"low\", \"deck\": [0, 0, 1, 1], \"goods\": []}}")]
        [TestCase("{" + GOOD_TALLY + ", \"quay\": {\"restricted\": {\"centre\": [0, 0, 0], \"size\": [1, 1, 1]}, \"deckY\": -4, \"deck\": [1, 0, 0, 1], \"goods\": []}}")]
        [TestCase("{" + GOOD_TALLY + ", \"quay\": {\"restricted\": {\"centre\": [0, 0, 0], \"size\": [1, 1, 1]}, \"deckY\": -4, \"deck\": [0, 0, 1, 1]}}")]
        [TestCase("{" + GOOD_TALLY + ", \"quay\": {\"restricted\": {\"centre\": [0, 0, 0], \"size\": [1, 1, 1]}, \"deckY\": -4, \"deck\": [0, 0, 1, 1], \"goods\": [{\"kit\": \"x\", \"position\": [0, 0], \"euler\": [0, 0, 0], \"scale\": 1, \"solid\": true}]}}")]
        [TestCase("{" + GOOD_TALLY + ", \"quay\": {\"restricted\": {\"centre\": [0, 0, 0], \"size\": [1, 1, 1]}, \"deckY\": -4, \"deck\": [0, 0, 1, 1], \"goods\": [{\"kit\": \"x\", \"position\": [0, 0, 0], \"euler\": [0, 0, 0], \"scale\": 0, \"solid\": true}]}}")]
        public void MalformedQuay_IsRefused_WithoutAThrow(string json)
        {
            Assert.IsFalse(WarehouseBuilder.TryReadQuay(JObject.Parse(json), out _, out string problem));
            Assert.IsNotEmpty(problem);
        }

        [TestCase("\"sideHole\": [0, 0, 1]")]
        [TestCase("\"sideHole\": [1, 0, 0, 1]")]
        [TestCase("\"restrictedMore\": 3")]
        [TestCase("\"restrictedMore\": [{\"centre\": [0, 0, 0], \"size\": [1, 1, 1]}]")]
        [TestCase("\"restrictedMore\": [{\"name\": \"x\", \"centre\": [0, 0, 0], \"size\": [1, 0, 1]}]")]
        public void MalformedSideRoom_IsRefused_WithoutAThrow(string field)
        {
            string json = "{\"cellar\": {\"hole\": [0, 0, 1, 1], \"inside\": [0, 0, 1, 1], \"floorY\": -4, \"door\": {\"position\": [0, 0, 0], \"yaw\": 0, \"scaleY\": 1, \"openAngle\": 90}, "
                          + "\"restricted\": {\"centre\": [0, 0, 0], \"size\": [1, 1, 1]}, " + field + "}}";
            Assert.IsFalse(WarehouseBuilder.TryReadCellar(JObject.Parse(json), out _, out string problem));
            Assert.IsNotEmpty(problem);
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
