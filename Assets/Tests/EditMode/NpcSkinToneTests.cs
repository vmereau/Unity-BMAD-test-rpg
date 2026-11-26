using System.Text.RegularExpressions;
using Game.AI;
using Game.NPC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// spec-npc-skin-tones: <see cref="NpcSkinTone"/> swaps masked SG_SkinLit materials for shared toned copies
/// (<see cref="SkinToneMaterials"/>) and leaves everything else alone; shipped assets are wired as specified.
/// </summary>
public class NpcSkinToneTests
{
    private const string MODELS = "Assets/_Game/Art/Characters/Humanoids/models/";
    private const string BASE_MAT = MODELS + "test base hero/M_TestBaseHero.mat";
    private const string ARMOR_MAT = MODELS + "Company/Grunt armor/M_CompanyGrunt.mat";
    private const string HEAD_MAT = MODELS + "heads/Cook/M_Head_Cook.mat";
    private const string TONES = "Assets/_Game/Data/Characters/SkinTones/";
    private const string VARIANT = "Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab";

    private static readonly (string npc, string tone)[] Assigned =
    {
        ("BeatenWorker/NPC_BeatenWorker", "Fair"), ("StashOwner/NPC_StashOwner", "Olive"),
        ("Storekeeper/NPC_Storekeeper", "Deep"), ("RacketGuard/NPC_RacketGuard", "Weathered"),
        ("DockGuard/NPC_DockGuard", "Fair"),
        // spec-npc-faces-second-batch
        ("CompanyGuard/NPC_CompanyGuard_ArrivalLeft", "Fair"), ("CompanyGuard/NPC_CompanyGuard_ArrivalRight", "Olive"),
        ("WarehouseGuard/NPC_WarehouseGuard", "Deep"), ("Bandit/NPC_Bandit", "Weathered"),
        ("KitchenHand/NPC_KitchenHand", "Olive"),
    };

    private GameObject _root;
    private Transform _model;
    private NpcSkinTone _skin;
    private SkinToneSO _tone;
    private SkinToneSO _otherTone;
    private Material _lit;

    [SetUp]
    public void SetUp()
    {
        SkinToneMaterials.Clear();
        _root = new GameObject("SkinToneTest");
        _model = new GameObject("Character").transform;
        _model.SetParent(_root.transform);
        _skin = _root.AddComponent<NpcSkinTone>();
        var so = new SerializedObject(_skin);
        so.FindProperty("_model").objectReferenceValue = _model;
        so.ApplyModifiedPropertiesWithoutUndo();
        _tone = ScriptableObject.CreateInstance<SkinToneSO>();
        _tone.name = "TestTone";
        _tone.EditorSet(new Color(0.4f, 0.25f, 0.15f), 0.8f);
        _otherTone = ScriptableObject.CreateInstance<SkinToneSO>();
        _otherTone.name = "OtherTone";
        _otherTone.EditorSet(new Color(0.8f, 0.6f, 0.5f), 1f);
        _lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    }

    [TearDown]
    public void TearDown()
    {
        SkinToneMaterials.Clear();
        Object.DestroyImmediate(_root);
        Object.DestroyImmediate(_tone);
        Object.DestroyImmediate(_otherTone);
        Object.DestroyImmediate(_lit);
    }

    private static Material Load(string path)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Assert.IsTrue(m != null, $"Missing {path}");
        return m;
    }

    private MeshRenderer AddRenderer(string name, params Material[] materials)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_model);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = materials;
        return r;
    }

    [Test]
    public void TonedBareNpc_SwapsBodyAndHeadToTonedCopy()
    {
        var baseMat = Load(BASE_MAT);
        var body = AddRenderer("base_hero", baseMat);
        var head = AddRenderer("Head", baseMat);

        Assert.AreEqual(2, _skin.Apply(_tone));

        Assert.AreNotSame(baseMat, body.sharedMaterial);
        Assert.AreSame(body.sharedMaterial, head.sharedMaterial, "same source + tone = one shared copy");
        var set = body.sharedMaterial.GetColor("_SkinTone");
        Assert.Less(Mathf.Abs(set.r - _tone.Tone.r) + Mathf.Abs(set.g - _tone.Tone.g) + Mathf.Abs(set.b - _tone.Tone.b), 1e-4f, $"tone {set}");
        Assert.AreEqual(_tone.Strength, body.sharedMaterial.GetFloat("_SkinToneStrength"), 1e-6f);
        Assert.AreEqual(0f, baseMat.GetFloat("_SkinToneStrength"), 1e-6f, "the asset itself stays untoned");
    }

    [Test]
    public void TonedArmoredNpc_TonesArmorSkinMaterial_KeepsOtherSlots()
    {
        var armorMat = Load(ARMOR_MAT);
        var armor = AddRenderer("Armor", armorMat, _lit);

        Assert.AreEqual(1, _skin.Apply(_tone));

        Assert.IsTrue(SkinToneMaterials.IsToned(armor.sharedMaterials[0]));
        Assert.AreSame(_lit, armor.sharedMaterials[1]);
    }

    [Test]
    public void ReEquip_NewArmorRenderer_IsTonedOnReapply_OldOnesUntouched()
    {
        var baseMat = Load(BASE_MAT);
        var body = AddRenderer("base_hero", baseMat);
        _skin.Apply(_tone);
        var toned = body.sharedMaterial;

        var armor = AddRenderer("Armor", Load(ARMOR_MAT));
        Assert.AreEqual(1, _skin.Apply(_tone), "only the new armor is swapped");
        Assert.IsTrue(SkinToneMaterials.IsToned(armor.sharedMaterial));
        Assert.AreSame(toned, body.sharedMaterial);
    }

    [Test]
    public void Retone_SwapsCopies_NullRestoresSource()
    {
        var baseMat = Load(BASE_MAT);
        var body = AddRenderer("base_hero", baseMat);
        _skin.Apply(_tone);

        Assert.AreEqual(1, _skin.Apply(_otherTone));
        Assert.AreSame(SkinToneMaterials.Get(baseMat, _otherTone), body.sharedMaterial);
        Assert.AreSame(_otherTone, _skin.Tone);

        Assert.AreEqual(1, _skin.Apply(null));
        Assert.AreSame(baseMat, body.sharedMaterial);
    }

    [Test]
    public void MissingModel_WarnsOnlyWithATone()
    {
        var so = new SerializedObject(_skin);
        so.FindProperty("_model").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        LogAssert.Expect(LogType.Warning, new Regex("no model assigned"));
        Assert.AreEqual(0, _skin.Apply(_tone));
        Assert.AreEqual(0, _skin.Apply(null)); // no second warning
    }

    [Test]
    public void NoTone_LeavesMaterialsUntouched()
    {
        var baseMat = Load(BASE_MAT);
        var body = AddRenderer("base_hero", baseMat);

        Assert.AreEqual(0, _skin.Apply(null));

        Assert.AreSame(baseMat, body.sharedMaterial);
        Assert.AreEqual(0, SkinToneMaterials.Count);
    }

    [Test]
    public void NonSkinMaterial_IsUntouched()
    {
        var weapon = AddRenderer("Sword", _lit);

        Assert.AreEqual(0, _skin.Apply(_tone));

        Assert.AreSame(_lit, weapon.sharedMaterial);
    }

    [Test]
    public void SharedCache_SameToneSameInstance_DifferentToneDifferentInstance()
    {
        var baseMat = Load(BASE_MAT);

        var a = SkinToneMaterials.Get(baseMat, _tone);
        var b = SkinToneMaterials.Get(baseMat, _tone);
        var c = SkinToneMaterials.Get(baseMat, _otherTone);

        Assert.AreSame(a, b);
        Assert.AreNotSame(a, c);
        Assert.AreSame(a, SkinToneMaterials.Get(a, _otherTone), "a toned copy is never re-toned");
        Assert.AreEqual(2, SkinToneMaterials.Count);
    }

    [Test]
    public void VariantHeadWithoutMask_KeepsItsMaterial_WarnsOnce()
    {
        // Every shipped head has a mask now (spec-npc-faces-second-batch): a legacy / unbaked one is simulated.
        var headMat = new Material(Load(HEAD_MAT)) { name = "M_Head_Unmasked" };
        headMat.SetTexture("_SkinMask", null);
        try
        {
            Assert.IsFalse(SkinToneMaterials.HasMask(headMat));
            var head = AddRenderer("Head", headMat);
            var body = AddRenderer("base_hero", Load(BASE_MAT));

            LogAssert.Expect(LogType.Warning, new Regex("M_Head_Unmasked has no skin mask"));
            _skin.Apply(_tone);
            _skin.Apply(_tone); // second apply: no second warning (LogAssert fails on an unexpected one)

            Assert.AreSame(headMat, head.sharedMaterial);
            Assert.IsTrue(SkinToneMaterials.IsToned(body.sharedMaterial));
        }
        finally { Object.DestroyImmediate(headMat); }
    }

    [Test]
    public void MaskedVariantHead_IsToned_LikeTheBody()
    {
        var headMat = Load(HEAD_MAT);
        var head = AddRenderer("Head", headMat);
        var body = AddRenderer("base_hero", Load(BASE_MAT));

        _skin.Apply(_tone); // no warning expected: LogAssert fails on an unexpected one

        Assert.IsTrue(SkinToneMaterials.IsToned(head.sharedMaterial), "M_Head_Cook has its own mask");
        Assert.AreSame(headMat, SkinToneMaterials.SourceOf(head.sharedMaterial));
        Assert.IsTrue(SkinToneMaterials.IsToned(body.sharedMaterial));
    }

    [Test]
    public void SkinMaterials_UseSkinShader_BaseAndArmorHaveMasks()
    {
        foreach (var path in new[] { BASE_MAT, ARMOR_MAT })
        {
            var m = Load(path);
            Assert.IsTrue(SkinToneMaterials.HasMask(m), $"{path}: SG_SkinLit with a mask");
            Assert.Greater(m.GetFloat("_SkinRefLuminance"), 0.01f, path);
            Assert.AreEqual(0f, m.GetFloat("_SkinToneStrength"), 1e-6f, $"{path}: untoned by default");
        }
        var baseMask = Load(BASE_MAT).GetTexture("_SkinMask");
        float baseRef = Load(BASE_MAT).GetFloat("_SkinRefLuminance");
        int heads = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Material M_Head_", new[] { MODELS + "heads" }))
        {
            var head = Load(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsTrue(SkinToneMaterials.IsSkin(head), $"{head.name} uses SG_SkinLit");
            Assert.IsTrue(SkinToneMaterials.HasMask(head), $"{head.name}: has a skin mask (spec-npc-faces-second-batch)");
            Assert.AreNotSame(baseMask, head.GetTexture("_SkinMask"), $"{head.name}: never the base hero's mask");
            Assert.AreEqual("T_SkinMask_" + head.name.Substring(2), head.GetTexture("_SkinMask").name,
                $"{head.name}: its own mask, not another head's");
            heads++;
            Assert.AreEqual(baseRef, head.GetFloat("_SkinRefLuminance"), 1e-6f, $"{head.name}: base ref luminance (same tone transform at the neck)");
            Assert.AreEqual(0f, head.GetFloat("_SkinToneStrength"), 1e-6f, $"{head.name}: untoned by default");
        }
        Assert.AreEqual(18, heads, "shipped head materials (14 painted / portrait + 4 Meshy: spec-npc-meshy-heads)");
    }

    [Test]
    public void ShippedNpcs_HaveTheirTones()
    {
        foreach (var (npc, tone) in Assigned)
        {
            var entity = AssetDatabase.LoadAssetAtPath<NPCEntity>($"Assets/_Game/Data/NPCs/{npc}.asset");
            Assert.IsTrue(entity != null, $"Missing {npc}");
            var expected = AssetDatabase.LoadAssetAtPath<SkinToneSO>($"{TONES}Tone_{tone}.asset");
            Assert.IsTrue(expected != null, $"Missing Tone_{tone}");
            Assert.AreSame(expected, entity.SkinTone, npc);
        }
    }

    [Test]
    public void FirstBatchFaces_StayUntoned()
    {
        // spec-npc-faces-second-batch Never: no tones on the Warden, Cook, Blacksmith or TentMate (toneable heads only).
        foreach (var npc in new[] { "Warden/NPC_Warden", "Cook/NPC_Cook", "BlackSmith/NPC_Blacksmith", "TentMate/NPC_TentMate" })
        {
            var entity = AssetDatabase.LoadAssetAtPath<NPCEntity>($"Assets/_Game/Data/NPCs/{npc}.asset");
            Assert.IsTrue(entity != null, $"Missing {npc}");
            Assert.IsTrue(entity.SkinTone == null, $"{npc} should keep the base skin");
        }
    }

    [Test]
    public void NpcVariant_HasSkinToneWired()
    {
        var root = PrefabUtility.LoadPrefabContents(VARIANT);
        try
        {
            var skin = root.GetComponent<NpcSkinTone>();
            Assert.IsTrue(skin != null, "NPC_base Variant has no NpcSkinTone");
            var so = new SerializedObject(skin);
            Assert.IsTrue(so.FindProperty("_model").objectReferenceValue != null, "_model unwired");
            Assert.IsTrue(so.FindProperty("_visuals").objectReferenceValue != null, "_visuals unwired");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
