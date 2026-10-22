using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Core;
using Game.Inventory;
using Game.NPC;
using Game.World;

namespace Game.Editor
{
    public enum StockChange { Added, Updated, Removed, Unchanged, NotFound, Error }

    public class StockChangeResult
    {
        public StockChange Change;
        public string Message;

        /// <summary>True when the stock now matches the request. NotFound (removing an absent item) is not a success.</summary>
        public bool Success => Change is not (StockChange.Error or StockChange.NotFound);
        /// <summary>True when the stock list was modified (scene needs saving).</summary>
        public bool Modified => Change is StockChange.Added or StockChange.Updated or StockChange.Removed;
        public override string ToString() => $"{Change}: {Message}";
    }

    /// <summary>
    /// Edits an NPC's shop stock — the <c>InventorySystem._startingItems</c> of its scene instance (the trade window
    /// lists the whole NPC inventory). Changes are saved as prefab-instance overrides in the scene.
    /// Driven by the /NPC:shop-dialogue Claude command through execute_code.
    /// </summary>
    public static class NpcShopEditor
    {
        private const string TAG = "[NpcShop]";
        private const string STARTING_ITEMS = "_startingItems";
        // InventorySystem.Awake adds starting items one unit at a time — keep typos from becoming huge loops.
        public const int MAX_COUNT = 999;

        /// <summary>Sets <paramref name="item"/>'s stock to <paramref name="count"/> (0 = remove). Pure — edits the list in place.</summary>
        public static StockChangeResult ApplyStockChange(List<StartingItem> stock, ItemSO item, int count)
        {
            if (stock == null || item == null)
                return new StockChangeResult { Change = StockChange.Error, Message = "Stock list or item is null." };
            if (count < 0 || count > MAX_COUNT)
                return new StockChangeResult { Change = StockChange.Error, Message = $"Count must be 0..{MAX_COUNT} (got {count})." };

            int index = stock.FindIndex(s => s.item == item);
            if (count > 0 && index >= 0 && stock[index].count == count && stock.FindLastIndex(s => s.item == item) == index)
                return new StockChangeResult { Change = StockChange.Unchanged, Message = $"{item.name} already x{count}." };
            if (count == 0)
            {
                if (index < 0)
                    return new StockChangeResult { Change = StockChange.NotFound, Message = $"{item.name} is not in stock." };
                stock.RemoveAll(s => s.item == item);
                return new StockChangeResult { Change = StockChange.Removed, Message = $"{item.name} removed." };
            }

            if (index < 0)
            {
                stock.Add(new StartingItem { item = item, count = count });
                return new StockChangeResult { Change = StockChange.Added, Message = $"{item.name} x{count} added." };
            }

            stock[index] = new StartingItem { item = item, count = count };
            // Collapse duplicate entries left by hand edits so the count is exact.
            for (int i = stock.Count - 1; i > index; i--)
                if (stock[i].item == item) stock.RemoveAt(i);
            return new StockChangeResult { Change = StockChange.Updated, Message = $"{item.name} set to x{count}." };
        }

        /// <summary>Scene inventories of <paramref name="npc"/> in every loaded scene (matched by PersistentID.Entity).</summary>
        public static List<InventorySystem> FindInstances(NPCEntity npc)
        {
            var found = new List<InventorySystem>();
            if (npc == null) return found;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (PersistentID id in root.GetComponentsInChildren<PersistentID>(true))
                {
                    if (id.Entity != npc) continue;
                    var inventory = id.GetComponent<InventorySystem>();
                    if (inventory != null) found.Add(inventory);
                }
            }
            return found;
        }

        public static StockChangeResult SetStock(NPCEntity npc, ItemSO item, int count)
        {
            // In Play mode the edit would hit the runtime copy (already read in Awake) and could not be saved.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Fail("Exit Play mode before editing shop stock.");
            if (!TryGetSingleInstance(npc, out InventorySystem inventory, out string error))
                return Fail(error);

            // SetStock saves the scene — never sweep unrelated unsaved edits into that save.
            Scene scene = inventory.gameObject.scene;
            if (scene.isDirty)
                return Fail($"Scene {scene.path} has unsaved changes — save or discard them first.");

            var so = new SerializedObject(inventory);
            SerializedProperty list = so.FindProperty(STARTING_ITEMS);
            if (list == null) return Fail($"{STARTING_ITEMS} not found on InventorySystem.");

            List<StartingItem> stock = ReadStock(list);
            StockChangeResult result = ApplyStockChange(stock, item, count);
            if (!result.Modified) return result;

            list.arraySize = stock.Count;
            for (int i = 0; i < stock.Count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative(nameof(StartingItem.item)).objectReferenceValue = stock[i].item;
                element.FindPropertyRelative(nameof(StartingItem.count)).intValue = stock[i].count;
            }
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                return Fail($"Stock change APPLIED but scene {scene.path} could not be saved — it is still dirty with the change; save it manually or discard it.");

            GameLog.Info(TAG, $"{npc.name} ({inventory.gameObject.name}): {result.Message}");
            return result;
        }

        /// <summary>Readable authored starting stock (not the live inventory after trades), e.g. for the command's "list" mode.</summary>
        public static string DescribeStock(NPCEntity npc)
        {
            if (!TryGetSingleInstance(npc, out InventorySystem inventory, out string error))
                return $"FAILED: {error}";

            List<StartingItem> stock = ReadStock(new SerializedObject(inventory).FindProperty(STARTING_ITEMS));
            var sb = new StringBuilder($"{npc.name} — {inventory.gameObject.name} in {inventory.gameObject.scene.path} (starting stock)\n");
            if (stock.Count == 0) sb.AppendLine("(empty)");
            foreach (StartingItem s in stock)
                sb.AppendLine($"{(s.item != null ? s.item.name : "<missing>")} x{s.count}");
            return sb.ToString();
        }

        private static List<StartingItem> ReadStock(SerializedProperty list)
        {
            var stock = new List<StartingItem>();
            if (list == null) return stock;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                stock.Add(new StartingItem
                {
                    item = element.FindPropertyRelative(nameof(StartingItem.item)).objectReferenceValue as ItemSO,
                    count = element.FindPropertyRelative(nameof(StartingItem.count)).intValue
                });
            }
            return stock;
        }

        private static bool TryGetSingleInstance(NPCEntity npc, out InventorySystem inventory, out string error)
        {
            inventory = null;
            error = null;
            if (npc == null) { error = "NPC is null."; return false; }

            List<InventorySystem> instances = FindInstances(npc);
            if (instances.Count == 1) { inventory = instances[0]; return true; }

            string scenes = string.Join(", ", Enumerable.Range(0, SceneManager.sceneCount)
                .Select(i => SceneManager.GetSceneAt(i).path));
            error = instances.Count == 0
                ? $"No scene instance of {npc.name} with an InventorySystem in the open scenes ({scenes})."
                : $"{instances.Count} scene instances of {npc.name} found ({string.Join(", ", instances.Select(x => x.gameObject.name))}) — ambiguous.";
            return false;
        }

        private static StockChangeResult Fail(string error)
        {
            GameLog.Error(TAG, error);
            return new StockChangeResult { Change = StockChange.Error, Message = error };
        }
    }
}
