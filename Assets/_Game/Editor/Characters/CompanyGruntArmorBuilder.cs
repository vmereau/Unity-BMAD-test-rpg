using System;
using System.IO;
using System.Collections.Generic;
using Game.Inventory;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Characters
{
    /// <summary>Rebuilds the Company outfit from its fitted canonical-rig FBX.</summary>
    public static class CompanyGruntArmorBuilder
    {
        public const string Folder = "Assets/_Game/Art/Characters/Humanoids/models/Company/Grunt armor/";
        public const string VisualPath = "Assets/_Game/Prefabs/Items/Armors/CompanyGruntArmor_Visual.prefab";
        public const string WorldPath = "Assets/_Game/Prefabs/Items/Armors/CompanyGruntArmor_World.prefab";
        public const string ItemPath = "Assets/_Game/Data/Items/Armor_CompanyGrunt.asset";
        private const string TextureStem = "Meshy_AI_Armored_Medieval_Guard_20261111_texture";

        public static string Build()
        {
            string model = Folder + "CompanyGrunt_Fitted.fbx";
            AssetDatabase.ImportAsset(model, ImportAssetOptions.ForceUpdate);
            var importer = (ModelImporter)AssetImporter.GetAtPath(model);
            importer.globalScale = 100;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = false;
            importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
            var target = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Art/Characters/Humanoids/models/test base hero/rigged test hero with textures.fbx")
                .GetComponentInChildren<SkinnedMeshRenderer>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(model).GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = SkinnedMeshBaker.Bake(source, target, null, "CompanyGrunt_baked", out string error);
            if (mesh == null) throw new InvalidOperationException(error);
            var names = Array.ConvertAll(target.bones, b => b.name);
            var report = SkinnedNeckValidator.ValidateBindSpace(mesh, names, target.sharedMesh, names);
            if (!report.IsValid) { UnityEngine.Object.DestroyImmediate(mesh); throw new InvalidOperationException(string.Join("; ", report.Errors)); }
            SkinnedNeckBaker.RepairNeckUV(mesh,1.48f,1.62f,.17f);
            var uvReport = SkinnedNeckValidator.ValidateNeckUV(mesh,1.48f,1.62f,.17f);
            if (!uvReport.IsValid) { UnityEngine.Object.DestroyImmediate(mesh); throw new InvalidOperationException(string.Join("; ", uvReport.Errors)); }
            if (mesh.bounds.min.y < -.03f || mesh.bounds.max.y < 1.5f || mesh.bounds.max.y > 1.63f)
            { UnityEngine.Object.DestroyImmediate(mesh); throw new InvalidOperationException("Company outfit extent is outside canonical feet/collar limits."); }
            mesh = SkinnedMeshBaker.SaveMesh(mesh, Folder + "CompanyGrunt_baked.asset");

            string materialPath = Folder + "M_CompanyGrunt.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,materialPath); }
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(Folder + TextureStem + "_normal.png");
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + TextureStem + ".png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + TextureStem + "_normal.png"));
            material.SetFloat("_BumpScale", .65f);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", .15f);
            EditorUtility.SetDirty(material);
            var leather = AssetDatabase.LoadAssetAtPath<ArmorSO>("Assets/_Game/Data/Items/Armor_TestArmor.asset");
            GameObject visual;
            var template = UnityEngine.Object.Instantiate(leather.equipVisualPrefab);
            try
            {
                template.name = "CompanyGruntArmor_Visual";
                var renderer = template.GetComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                template.GetComponent<SkinnedBodyPart>().EditorSetup(renderer,names,target.rootBone.name);
                // The leather template carries conservative root-space bounds.
                // Setting localBounds on an intentionally skeleton-less SMR
                // asks Unity to evaluate absent bones and logs a bind error.
                visual = PrefabUtility.SaveAsPrefabAsset(template,VisualPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(template); }
            var item = AssetDatabase.LoadAssetAtPath<ArmorSO>(ItemPath);
            if (item == null) { item = ScriptableObject.CreateInstance<ArmorSO>(); AssetDatabase.CreateAsset(item, ItemPath); }
            item.itemName = "Company Guard Armor";
            item.description = "An issued leather breastpiece over a charcoal padded jack, marked with the Company's barred passage. Faded ochre shoulders identify its wearer at every gate.";
            item.maxStacks = 1;
            item.slot = EquipmentSlot.Armor;
            item.defenseBonus = leather.defenseBonus;
            item.enduranceBonus = leather.enduranceBonus;
            item.buyValue = leather.buyValue;
            item.sellValue = leather.sellValue;
            item.equipVisualPrefab = visual;
            item.icon = MakeIcon();
            var world = UnityEngine.Object.Instantiate(leather.worldItemPrefab);
            try
            {
                world.name = "CompanyGruntArmor_World";
                world.GetComponent<ItemPickup>().Configure(item);
                // Keep the known pickup components, but use this outfit's actual
                // textured torso rather than applying its atlas to leather UVs.
                foreach (var renderer in world.GetComponentsInChildren<Renderer>()) UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                var garment = new GameObject("Folded Company Jack");
                garment.transform.SetParent(world.transform,false);
                garment.transform.localRotation = Quaternion.Euler(90,0,0);
                garment.transform.localScale = Vector3.one*.65f;
                garment.AddComponent<MeshFilter>().sharedMesh = MakePickupMesh(mesh);
                garment.AddComponent<MeshRenderer>().sharedMaterial = material;
                foreach (var collider in world.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                var bounds = garment.GetComponent<Renderer>().bounds;
                var box = world.AddComponent<BoxCollider>();
                box.center = bounds.center-world.transform.position;
                box.size = bounds.size;
                item.worldItemPrefab = PrefabUtility.SaveAsPrefabAsset(world,WorldPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(world); }
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
            Game.Editor.Save.ItemCatalogAutoSync.SyncItemCatalog();
            return $"Built Company armor: {mesh.vertexCount} vertices, {mesh.bindposes.Length} canonical bones. Leather-equivalent provisional stats/prices.";
        }

        private static Mesh MakePickupMesh(Mesh source)
        {
            var vertices = source.vertices;
            var triangles = source.triangles;
            var kept = new List<int>();
            for(int i=0;i<triangles.Length;i+=3)
            {
                bool include = true;
                for(int j=0;j<3;j++)
                {
                    var p = vertices[triangles[i+j]];
                    include &= p.y > .78f && Mathf.Abs(p.x) < .27f;
                }
                if(include) { kept.Add(triangles[i]); kept.Add(triangles[i+1]); kept.Add(triangles[i+2]); }
            }
            // Compact indices so limb vertices cannot inflate pickup bounds.
            var remap = new Dictionary<int,int>();
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var sourceNormals=source.normals; var sourceTangents=source.tangents; var sourceUvs=source.uv;
            for(int i=0;i<kept.Count;i++)
            {
                int old=kept[i];
                if(!remap.TryGetValue(old,out int mapped))
                {
                    mapped=positions.Count; remap.Add(old,mapped);
                    positions.Add(vertices[old]-new Vector3(0,1.17f,0));
                    normals.Add(sourceNormals[old]); tangents.Add(sourceTangents[old]); uvs.Add(sourceUvs[old]);
                }
                kept[i]=mapped;
            }
            var mesh = new Mesh { name="CompanyGrunt_Pickup",indexFormat=source.indexFormat };
            mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetTangents(tangents); mesh.SetUVs(0,uvs);
            mesh.triangles=kept.ToArray();
            MeshUtility.Optimize(mesh);
            mesh.RecalculateBounds();
            return SkinnedMeshBaker.SaveMesh(mesh,Folder+"CompanyGrunt_Pickup.asset");
        }

        private static Sprite MakeIcon()
        {
            string path = Folder + "CompanyGrunt_Icon.png";
            var texture = new Texture2D(2,2);
            try
            {
                ImageConversion.LoadImage(texture,File.ReadAllBytes(Folder + "grunt armor.png"));
                // Torso crop from the supplied 1380x768 concept; normalized for reproducibility.
                int x = Mathf.RoundToInt(texture.width*.405f), y = Mathf.RoundToInt(texture.height*.405f);
                int w = Mathf.RoundToInt(texture.width*.20f), h = Mathf.RoundToInt(texture.height*.43f);
                var crop = new Texture2D(w,h);
                try { crop.SetPixels(texture.GetPixels(x,y,w,h)); crop.Apply(); File.WriteAllBytes(path,crop.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(crop); }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
