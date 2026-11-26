using System.Collections.Generic;
using Game.Inventory;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Builds the Inventory paper doll (spec-inventory-equipment-paper-doll; look = UX DESIGN.md) into the prefabs, idempotent:
    /// <c>EquipmentSlot.prefab</c> (well, outer ring, icon, empty label, caption), <c>EquipmentPanel.prefab</c> (panel, header,
    /// divider, silhouette, leader lines, seven slots) and the panel instance in <c>InventoryUI.prefab</c> (rect, grid drop
    /// area). Keeps the <c>EquipmentUI</c> component and slot objects so Player.prefab overrides survive.
    /// Menu: Tools/UI/Build Equipment Paper Doll. MCP: <c>Game.Editor.InventoryEquipmentLayoutBuilder.Build()</c>.
    /// </summary>
    public static class InventoryEquipmentLayoutBuilder
    {
        private const string SlotPrefab = "Assets/_Game/Prefabs/UI/Equipment/EquipmentSlot.prefab";
        private const string PanelPrefab = "Assets/_Game/Prefabs/UI/Equipment/EquipmentPanel.prefab";
        private const string InventoryPrefab = "Assets/_Game/Prefabs/UI/Inventory/InventoryUI.prefab";
        private const string SlotWellSprite = "Assets/_Game/Art/UI/Sprites/UI_SlotWell.png";
        private const string SilhouetteSprite = "Assets/_Game/Art/UI/Sprites/UI_EquipmentSilhouette.png";
        private const string DashSprite = "Assets/_Game/Art/UI/Sprites/UI_LeaderDash.png";
        private const string FrameSprite = "Assets/_Game/Art/UI/Sprites/UI_OutlineFrame.png";
        private const string FontAsset = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // DESIGN.md colours
        private static readonly Color Panel = Hex(0x2B2118), PanelEdge = Hex(0x4A3828), Ink = Hex(0x6B5440);
        private static readonly Color TextMuted = Hex(0x807C74), EmptyLabel = Hex(0x4D4840);

        // Panel at screen (40, 140), 496×800; everything below is panel-local, top-left, y down.
        private static readonly Vector2 PanelPos = new Vector2(40, 140);
        private const float SlotSize = 88f;

        private struct SlotSpec
        {
            public string Name; public EquipmentSlot Slot; public Vector2 Pos; public Vector2 LineFrom, LineTo;
            public SlotSpec(string name, EquipmentSlot slot, float x, float y, float fx, float fy, float tx, float ty)
            { Name = name; Slot = slot; Pos = new Vector2(x, y); LineFrom = new Vector2(fx, fy); LineTo = new Vector2(tx, ty); }
        }

        // Screen coordinates (DESIGN.md layout; line ends from the mockup, helmet end moved onto the rendered head).
        private static readonly SlotSpec[] Slots =
        {
            new SlotSpec("SlotHelmet",   EquipmentSlot.Helmet,   72, 200, 160, 244, 258, 262),
            new SlotSpec("SlotArmor",    EquipmentSlot.Armor,    72, 370, 160, 414, 250, 400),
            new SlotSpec("SlotWeapon",   EquipmentSlot.Weapon,   72, 540, 160, 584, 190, 596),
            new SlotSpec("SlotRing1",    EquipmentSlot.Ring1,    72, 710, 160, 754, 186, 618),
            new SlotSpec("SlotNecklace", EquipmentSlot.Necklace, 416, 286, 416, 330, 298, 322),
            new SlotSpec("SlotOffhand",  EquipmentSlot.Offhand,  416, 540, 416, 584, 386, 596),
            new SlotSpec("SlotRing2",    EquipmentSlot.Ring2,    416, 710, 416, 754, 390, 618),
        };

        [MenuItem("Tools/UI/Build Equipment Paper Doll")]
        public static string Build()
        {
            EnsureFrameSprite();
            BuildSlotPrefab();
            BuildPanelPrefab();
            BuildInventoryInstance();
            AssetDatabase.SaveAssets();
            return "Equipment paper doll built";
        }

        // ── Sprites ─────────────────────────────────────────────────────────────

        /// <summary>White 9×9 frame sprite, border 3: the slot's outer ring (Sliced, centre not filled).</summary>
        private static void EnsureFrameSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(FrameSprite) != null) return;
            var tex = new Texture2D(9, 9, TextureFormat.RGBA32, false);
            var px = new Color[81];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(FrameSprite, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(FrameSprite);
            var ti = (TextureImporter)AssetImporter.GetAtPath(FrameSprite);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
            ti.spriteBorder = new Vector4(3, 3, 3, 3);
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }

        // ── EquipmentSlot.prefab ────────────────────────────────────────────────

        private static void BuildSlotPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(SlotPrefab);
            try
            {
                var rt = (RectTransform)root.transform;
                rt.sizeDelta = new Vector2(SlotSize, SlotSize);

                var button = root.GetComponent<Button>();
                if (button != null) Object.DestroyImmediate(button);
                var old = root.transform.Find("SlotLabel");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var well = root.GetComponent<Image>();
                well.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlotWellSprite);
                well.type = Image.Type.Sliced;
                well.pixelsPerUnitMultiplier = 32f; // same as ItemSlot: 4 px frame
                well.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                well.raycastTarget = true;

                var ring = Child(root.transform, "OuterRing");
                Stretch(ring, -3f);
                var ringImg = Get<Image>(ring);
                ringImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameSprite);
                ringImg.type = Image.Type.Sliced;
                ringImg.fillCenter = false;
                ringImg.pixelsPerUnitMultiplier = 1f;
                ringImg.raycastTarget = false;
                ringImg.enabled = false;
                ring.SetSiblingIndex(0);

                var icon = Child(root.transform, "Icon");
                Stretch(icon, 7f); // icon 84 % of the slot
                var iconImg = Get<Image>(icon);
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                iconImg.enabled = false;
                icon.SetSiblingIndex(1);

                var label = Child(root.transform, "EmptyLabel");
                Stretch(label, 2f);
                var labelText = Text(label, 15f, EmptyLabel, 0f); // DESIGN 0.5 px spacing overflows "OFFHAND" in this font
                labelText.text = "SLOT";

                var caption = Child(root.transform, "Caption");
                caption.anchorMin = new Vector2(0f, 0f);
                caption.anchorMax = new Vector2(1f, 0f);
                caption.pivot = new Vector2(0.5f, 1f);
                caption.anchoredPosition = new Vector2(0f, -4f);
                caption.sizeDelta = new Vector2(0f, 22f);
                var captionText = Text(caption, 17f, TextMuted, 0f);
                captionText.text = "Slot";

                var slotUI = root.GetComponent<EquipmentSlotUI>();
                var so = new SerializedObject(slotUI);
                so.FindProperty("_backgroundImage").objectReferenceValue = well;
                so.FindProperty("_iconImage").objectReferenceValue = iconImg;
                so.FindProperty("_emptyLabel").objectReferenceValue = labelText;
                so.FindProperty("_caption").objectReferenceValue = captionText;
                so.FindProperty("_outerRing").objectReferenceValue = ringImg;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, SlotPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ── EquipmentPanel.prefab ───────────────────────────────────────────────

        private static void BuildPanelPrefab()
        {
            var slotAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefab);
            var root = PrefabUtility.LoadPrefabContents(PanelPrefab);
            try
            {
                var layout = root.GetComponent<VerticalLayoutGroup>();
                if (layout != null) Object.DestroyImmediate(layout);
                var rootRt = (RectTransform)root.transform;
                TopLeft(rootRt, PanelPos, new Vector2(496, 800));

                // Panel: edge colour on the root, surface inset 4 px. Not raycast: clicks fall through to the context-menu blocker.
                var edge = Get<Image>(rootRt);
                edge.sprite = null;
                edge.color = PanelEdge;
                edge.raycastTarget = false;
                var surface = Child(rootRt, "Surface");
                Stretch(surface, 4f);
                var surfaceImg = Get<Image>(surface);
                surfaceImg.color = Panel;
                surfaceImg.raycastTarget = false;
                surface.SetSiblingIndex(0);

                var header = Child(rootRt, "Header");
                TopLeftLocal(header, new Vector2(32, 22), new Vector2(432, 26));
                var headerText = Text(header, 18f, TextMuted, 11f);
                headerText.alignment = TextAlignmentOptions.BottomLeft;
                headerText.text = "EQUIPMENT";
                header.SetSiblingIndex(1);

                var divider = Child(rootRt, "Divider");
                TopLeftLocal(divider, new Vector2(32, 56), new Vector2(432, 2));
                var dividerImg = Get<Image>(divider);
                dividerImg.color = Ink;
                dividerImg.raycastTarget = false;
                divider.SetSiblingIndex(2);

                var silhouette = Child(rootRt, "Silhouette");
                TopLeftLocal(silhouette, new Vector2(158, 214) - PanelPos, new Vector2(260, 700));
                var silImg = Get<Image>(silhouette);
                silImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SilhouetteSprite);
                silImg.color = Color.white;
                silImg.raycastTarget = false;
                silhouette.SetSiblingIndex(3);

                var lines = Child(rootRt, "LeaderLines");
                Stretch(lines, 0f);
                lines.SetSiblingIndex(4);

                var eqUI = root.GetComponent<EquipmentUI>();
                var eqSo = new SerializedObject(eqUI);
                var dash = AssetDatabase.LoadAssetAtPath<Sprite>(DashSprite);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAsset);

                int sibling = 5;
                foreach (var spec in Slots)
                {
                    var line = Child(lines, "Line_" + spec.Name.Substring(4));
                    LayoutLine(line, spec.LineFrom - PanelPos, spec.LineTo - PanelPos);
                    var lineImg = Get<Image>(line);
                    lineImg.sprite = dash;
                    lineImg.type = Image.Type.Tiled;
                    lineImg.color = Ink;
                    lineImg.raycastTarget = false;

                    var slotGo = EnsureSlotInstance(rootRt, spec.Name, slotAsset);
                    var slotRt = (RectTransform)slotGo.transform;
                    TopLeftLocal(slotRt, spec.Pos - PanelPos, new Vector2(SlotSize, SlotSize));
                    slotRt.SetSiblingIndex(sibling++);
                    slotRt.Find("Caption").GetComponent<TMP_Text>().text = EquipmentPanelRules.Caption(spec.Slot);
                    slotRt.Find("EmptyLabel").GetComponent<TMP_Text>().text = EquipmentPanelRules.EmptyLabel(spec.Slot);
                    foreach (var t in slotGo.GetComponentsInChildren<TMP_Text>(true)) t.font = font;

                    var slotUI = slotGo.GetComponent<EquipmentSlotUI>();
                    var slotSo = new SerializedObject(slotUI);
                    slotSo.FindProperty("_leaderLine").objectReferenceValue = lineImg;
                    slotSo.ApplyModifiedPropertiesWithoutUndo();

                    eqSo.FindProperty(FieldFor(spec.Slot)).objectReferenceValue = slotUI;
                }
                headerText.font = font;
                eqSo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PanelPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>The named slot as a fresh-reverted <c>EquipmentSlot.prefab</c> instance (an unpacked copy is replaced).</summary>
        private static GameObject EnsureSlotInstance(Transform parent, string name, GameObject slotAsset)
        {
            var existing = parent.Find(name);
            if (existing != null && PrefabUtility.IsAnyPrefabInstanceRoot(existing.gameObject)
                && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == slotAsset)
            {
                // Drop the old per-instance overrides (label text, sizes); the builder sets what it needs again.
                PrefabUtility.RevertPrefabInstance(existing.gameObject, InteractionMode.AutomatedAction);
                return existing.gameObject;
            }
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(slotAsset, parent);
            go.name = name;
            return go;
        }

        // ── InventoryUI.prefab ──────────────────────────────────────────────────

        private static void BuildInventoryInstance()
        {
            var root = PrefabUtility.LoadPrefabContents(InventoryPrefab);
            try
            {
                var panel = root.transform.Find("EquipmentPanel") as RectTransform;
                TopLeft(panel, PanelPos, new Vector2(496, 800));
                var eq = panel.GetComponent<EquipmentUI>();
                var so = new SerializedObject(eq);
                so.FindProperty("_gridDropArea").objectReferenceValue = root.transform.Find("InventoryPanel") as RectTransform;
                so.ApplyModifiedPropertiesWithoutUndo();
                // The old column's VerticalLayoutGroup left driven slot rects as overrides here: the panel prefab owns them now.
                foreach (var rt in panel.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rt == panel || !PrefabUtility.IsPartOfPrefabInstance(rt)) continue;
                    if (PrefabUtility.GetObjectOverrides(rt.gameObject).Exists(o => o.instanceObject == rt))
                        PrefabUtility.RevertObjectOverride(rt, InteractionMode.AutomatedAction);
                }
                PrefabUtility.SaveAsPrefabAsset(root, InventoryPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static string FieldFor(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Weapon => "_weaponSlot",
            EquipmentSlot.Offhand => "_offhandSlot",
            EquipmentSlot.Helmet => "_helmetSlot",
            EquipmentSlot.Armor => "_armorSlot",
            EquipmentSlot.Ring1 => "_ring1Slot",
            EquipmentSlot.Ring2 => "_ring2Slot",
            _ => "_necklaceSlot"
        };

        private static RectTransform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return (RectTransform)t;
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static T Get<T>(Component c) where T : Component =>
            c.GetComponent<T>() != null ? c.GetComponent<T>() : c.gameObject.AddComponent<T>();

        private static TMP_Text Text(RectTransform rt, float size, Color color, float spacing)
        {
            var text = Get<TextMeshProUGUI>(rt);
            text.fontSize = size;
            text.color = color;
            text.characterSpacing = spacing;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Top-left anchored rect at a parent-local top-left position (y down).</summary>
        private static void TopLeftLocal(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.sizeDelta = size;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        private static void TopLeft(RectTransform rt, Vector2 screenPos, Vector2 size) => TopLeftLocal(rt, screenPos, size);

        /// <summary>A line rect from <paramref name="from"/> to <paramref name="to"/> (parent-local, y down), pivot at its start.</summary>
        private static void LayoutLine(RectTransform rt, Vector2 from, Vector2 to)
        {
            Vector2 d = new Vector2(to.x - from.x, -(to.y - from.y)); // y up
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(from.x, -from.y);
            rt.sizeDelta = new Vector2(d.magnitude, 2f);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            rt.localScale = Vector3.one;
        }

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
