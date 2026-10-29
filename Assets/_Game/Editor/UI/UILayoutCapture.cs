using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Renders one screen of UICanvas.prefab offscreen at the 1920×1080 reference resolution, as a preview of
    /// the implemented UI (e.g. after wiring /ui:create sprites). Runtime-filled item grids get ItemSlot
    /// placeholders with real item icons, and every Image without a sprite is outlined so unskinned panels
    /// stay visible. Also measures rects for
    /// the UI Metrics table in Art/UI/CLAUDE.md. Works in edit mode — the prefab is loaded in isolation.
    /// </summary>
    public static class UILayoutCapture
    {
        public const int Width = 1920;
        public const int Height = 1080;

        private const string CanvasPrefabPath = "Assets/_Game/Prefabs/UI/UICanvas.prefab";
        private const string ItemSlotPrefabPath = "Assets/_Game/Prefabs/UI/Items/ItemSlot.prefab";
        private const string ItemIconsFolder = "Assets/_Game/Art/UI/Items";
        private const string OutputFolder = "Assets/_Game/Art/UI/Previews";

        private static readonly Color BackgroundColor = new(0.18f, 0.18f, 0.17f, 1f);
        private static readonly Color OutlineColor = new(0.85f, 0.8f, 0.69f, 1f);

        private sealed class ScreenDef
        {
            /// <summary>UICanvas child paths shown with their whole subtree (runtime-toggled panels included).</summary>
            public string[] Roots;
            /// <summary>UICanvas paths hidden again — alternate states that would overlap the default view.</summary>
            public string[] Hidden = Array.Empty<string>();
        }

        private static readonly Dictionary<string, ScreenDef> Screens = new(StringComparer.OrdinalIgnoreCase)
        {
            { "HUD", new ScreenDef { Roots = new[] { "Game" } } },
            { "Inventory", new ScreenDef { Roots = new[] { "Menus/TabBar", "Menus/InventoryUI" } } },
            { "QuestLog", new ScreenDef { Roots = new[] { "Menus/TabBar", "Menus/QuestLogUI" }, Hidden = new[] { "Menus/QuestLogUI/QuestInfoPanel/EmptyState" } } },
            { "Character", new ScreenDef { Roots = new[] { "Menus/TabBar", "Menus/CharacterStatsUI" } } },
            { "Skills", new ScreenDef { Roots = new[] { "Menus/TabBar", "Menus/SkillsUI" } } },
            { "Container", new ScreenDef { Roots = new[] { "ContainerUI" } } },
            { "Trade", new ScreenDef { Roots = new[] { "NPCTradeUI" } } },
            { "Crafting", new ScreenDef { Roots = new[] { "CookingUI" }, Hidden = new[] { "CookingUI/StationSide/StationPanel/ExperimentPanel", "CookingUI/StationSide/StationPanel/RecipesPanel/RecipeList/NoRecipesText", "CookingUI/StationSide/StationPanel/RecipesPanel/RecipeDetail/ItemDetailPanel/PriceSection" } } },
            { "Dialogue", new ScreenDef { Roots = new[] { "DialoguePanel" }, Hidden = new[] { "DialoguePanel/ContentArea/Choice Text", "DialoguePanel/ContentArea/ResponseWrapper" } } },
            { "GameMenu", new ScreenDef { Roots = new[] { "GameMenu" }, Hidden = new[] { "GameMenu/Panel/Window/MainButtons/SaveBlockedText", "GameMenu/Panel/Window/SaveSlotList", "GameMenu/Panel/Window/OptionsUI", "GameMenu/Panel/Window/BackButton" } } },
            { "DeathScreen", new ScreenDef { Roots = new[] { "DeathScreen" } } },
            { "ConfirmDialog", new ScreenDef { Roots = new[] { "ConfirmDialog" } } },
            { "Loading", new ScreenDef { Roots = new[] { "LoadingOverlay" } } },
        };

        public static IEnumerable<string> ScreenNames => Screens.Keys;

        [MenuItem("Tools/UI/Capture All Layout Previews")]
        private static void CaptureAllMenu()
        {
            foreach (string screen in Screens.Keys)
                Debug.Log($"[UILayoutCapture] {Capture(screen)}");
        }

        /// <summary>Captures <paramref name="screen"/> to Art/UI/Previews/UI_&lt;Screen&gt;_Preview.png. Returns the asset path or an "ERROR: …" string.</summary>
        public static string Capture(string screen)
        {
            if (!Screens.TryGetValue(screen, out ScreenDef def))
                return $"ERROR: unknown screen '{screen}'. Known: {string.Join(", ", Screens.Keys)}";

            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            RenderTexture renderTexture = null;
            Texture2D texture = null;
            try
            {
                var camera = new GameObject("UILayoutCaptureCamera").AddComponent<Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, root.scene);
                camera.scene = root.scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = BackgroundColor;
                camera.orthographic = true;
                renderTexture = new RenderTexture(Width, Height, 24);
                camera.targetTexture = renderTexture;

                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;

                string error = ShowOnly(root.transform, def);
                if (error != null) return error;

                FillEmptyItemGrids(root.transform);
                RebuildLayout(root);

                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                RenderTexture.active = previous;

                DrawOutlines(texture, root.transform);
                texture.Apply();

                Directory.CreateDirectory(OutputFolder);
                string assetPath = $"{OutputFolder}/UI_{CanonicalName(screen)}_Preview.png";
                File.WriteAllBytes(assetPath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                return assetPath;
            }
            catch (Exception e)
            {
                return $"ERROR: {e.Message}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (renderTexture != null) UnityEngine.Object.DestroyImmediate(renderTexture);
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Lists the visible rects of <paramref name="screen"/> at 1920×1080 (origin top-left), to depth
        /// <paramref name="maxDepth"/>: "name x,y w×h", plus grid cell sizes and font sizes.
        /// </summary>
        public static string Measure(string screen, int maxDepth = 4)
        {
            if (!Screens.TryGetValue(screen, out ScreenDef def))
                return $"ERROR: unknown screen '{screen}'. Known: {string.Join(", ", Screens.Keys)}";

            GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var canvasRect = (RectTransform)root.transform;
                canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRect.sizeDelta = new Vector2(Width, Height);
                canvasRect.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                canvasRect.localScale = Vector3.one;

                string error = ShowOnly(root.transform, def);
                if (error != null) return error;
                FillEmptyItemGrids(root.transform);
                RebuildLayout(root);

                var sb = new StringBuilder($"{CanonicalName(screen)} @ {Width}x{Height}, origin top-left\n");
                AppendRects(sb, root.transform, 0, maxDepth);
                return sb.ToString();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string CanonicalName(string screen)
        {
            foreach (string key in Screens.Keys)
                if (string.Equals(key, screen, StringComparison.OrdinalIgnoreCase)) return key;
            return screen;
        }

        private static string ShowOnly(Transform root, ScreenDef def)
        {
            foreach (Transform child in root)
                child.gameObject.SetActive(false);

            foreach (string path in def.Roots)
            {
                Transform current = root;
                foreach (string segment in path.Split('/'))
                {
                    Transform next = current.Find(segment);
                    if (next == null)
                        return $"ERROR: '{path}' not found under UICanvas (missing '{segment}') — update UILayoutCapture.Screens";
                    // Siblings of a nested segment keep their authored state, except the top-level hide above.
                    next.gameObject.SetActive(true);
                    current = next;
                }
                // Panels shown at runtime (equipment, item detail, toggled Panel children) start inactive in the prefab.
                foreach (Transform t in current.GetComponentsInChildren<Transform>(true))
                    t.gameObject.SetActive(true);
            }

            foreach (string path in def.Hidden)
            {
                Transform hidden = root.Find(path);
                if (hidden == null)
                    Debug.LogWarning($"[UILayoutCapture] Hidden path '{path}' not found — update UILayoutCapture.Screens");
                else
                    hidden.gameObject.SetActive(false);
            }
            return null;
        }

        /// <summary>Item grids are spawned at runtime — fill each empty GridLayoutGroup with ItemSlot placeholders up to its panel's height.</summary>
        private static void FillEmptyItemGrids(Transform root)
        {
            var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemSlotPrefabPath);
            if (slotPrefab == null)
            {
                Debug.LogWarning($"[UILayoutCapture] {ItemSlotPrefabPath} not found — item grids stay empty.");
                return;
            }

            List<Sprite> icons = LoadItemIcons();
            RebuildLayout(root.gameObject); // grid widths must be settled before counting columns
            foreach (var grid in root.GetComponentsInChildren<GridLayoutGroup>(false))
            {
                if (grid.transform.childCount > 0) continue;
                var content = (RectTransform)grid.transform;
                var panel = content.parent as RectTransform;
                if (panel == null) continue;

                float step = grid.cellSize.x + grid.spacing.x;
                float rowStep = grid.cellSize.y + grid.spacing.y;
                int columns = Mathf.Max(1, Mathf.FloorToInt((content.rect.width - grid.padding.horizontal + grid.spacing.x) / step));
                int rows = Mathf.Max(1, Mathf.FloorToInt((panel.rect.height - grid.padding.vertical + grid.spacing.y) / rowStep) - 1);
                for (int i = 0; i < columns * rows; i++)
                {
                    GameObject slot = UnityEngine.Object.Instantiate(slotPrefab, content);
                    // Runtime grids only hold slots for owned items — show a real icon like ItemSlotUI.Bind does.
                    Transform icon = slot.transform.Find("Icon");
                    if (icon != null && icon.TryGetComponent(out Image iconImage) && icons.Count > 0)
                    {
                        iconImage.sprite = icons[i % icons.Count];
                        iconImage.color = Color.white;
                    }
                }
            }
        }

        private static List<Sprite> LoadItemIcons()
        {
            var icons = new List<Sprite>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { ItemIconsFolder }))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                if (sprite != null) icons.Add(sprite);
            }
            return icons;
        }

        private static void RebuildLayout(GameObject root)
        {
            // Two passes: a grid inside a layout group computes its height before its width is settled.
            var rootRect = (RectTransform)root.transform;
            for (int pass = 0; pass < 2; pass++)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
            }
            Canvas.ForceUpdateCanvases();
        }

        private static void DrawOutlines(Texture2D texture, Transform root)
        {
            var canvasRect = (RectTransform)root;
            foreach (var image in root.GetComponentsInChildren<Image>(false))
            {
                if (image.sprite != null || image.color.a <= 0f) continue; // outlines only mark visible, unskinned rects
                Rect r = ToCanvasPixels(image.rectTransform, canvasRect);
                if (r.width >= Width - 1 && r.height >= Height - 1) continue; // full-screen backdrops
                DrawRect(texture, r, OutlineColor);
            }
        }

        /// <summary>Rect in canvas pixels, origin bottom-left (texture space).</summary>
        private static Rect ToCanvasPixels(RectTransform rect, RectTransform canvasRect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = canvasRect.InverseTransformPoint(corners[0]);
            Vector3 max = canvasRect.InverseTransformPoint(corners[2]);
            Rect canvas = canvasRect.rect;
            return Rect.MinMaxRect(min.x - canvas.xMin, min.y - canvas.yMin, max.x - canvas.xMin, max.y - canvas.yMin);
        }

        private static void DrawRect(Texture2D texture, Rect r, Color color)
        {
            int xMin = Mathf.Clamp(Mathf.RoundToInt(r.xMin), 0, Width - 1);
            int xMax = Mathf.Clamp(Mathf.RoundToInt(r.xMax) - 1, 0, Width - 1);
            int yMin = Mathf.Clamp(Mathf.RoundToInt(r.yMin), 0, Height - 1);
            int yMax = Mathf.Clamp(Mathf.RoundToInt(r.yMax) - 1, 0, Height - 1);
            if (xMax <= xMin || yMax <= yMin) return;

            for (int x = xMin; x <= xMax; x++)
            {
                texture.SetPixel(x, yMin, color);
                texture.SetPixel(x, yMax, color);
            }
            for (int y = yMin; y <= yMax; y++)
            {
                texture.SetPixel(xMin, y, color);
                texture.SetPixel(xMax, y, color);
            }
        }

        private static void AppendRects(StringBuilder sb, Transform t, int depth, int maxDepth)
        {
            if (depth > 0 && t is RectTransform rect && t.gameObject.activeInHierarchy)
            {
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                float x = corners[0].x + Width / 2f;
                float yTop = Height / 2f - corners[2].y;
                float w = corners[2].x - corners[0].x;
                float h = corners[2].y - corners[0].y;
                sb.Append(' ', depth * 2).Append($"{t.name} {x:0},{yTop:0} {w:0}x{h:0}");
                var grid = t.GetComponent<GridLayoutGroup>();
                if (grid != null) sb.Append($" grid cell {grid.cellSize.x:0}x{grid.cellSize.y:0} gap {grid.spacing.x:0}");
                var text = t.GetComponent<TMPro.TMP_Text>();
                if (text != null) sb.Append($" font {text.fontSize:0}");
                sb.AppendLine();
            }
            if (depth >= maxDepth || !t.gameObject.activeInHierarchy) return;
            foreach (Transform child in t)
                AppendRects(sb, child, depth + 1, maxDepth);
        }
    }
}
