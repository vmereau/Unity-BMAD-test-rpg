using Game.AI;
using Game.NPC;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// spec-npc-head-variants: <see cref="NpcHeadAppearance"/> swaps the Head SMR to a compatible <see cref="HeadAppearanceSO"/>,
/// keeps the default head otherwise, and every shipped head fits the NPC model.
/// </summary>
public class NpcHeadAppearanceTests
{
    private const string VARIANT = "Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab";
    private const string HEADS = "Assets/_Game/Data/Characters/Heads/";
    private const string NPCS = "Assets/_Game/Data/NPCs/";

    private static readonly (string npc, string head)[] Assigned =
    {
        ("Warden/NPC_Warden", "Warden"), ("Cook/NPC_Cook", "Cook"),
        ("BlackSmith/NPC_Blacksmith", "Blacksmith"), ("TentMate/NPC_TentMate", "TentMate"),
    };

    private GameObject _root;
    private GameObject _go;
    private NpcHeadAppearance _appearance;
    private SkinnedMeshRenderer _renderer;
    private Mesh _defaultMesh;
    private Material _defaultMaterial;

    [SetUp]
    public void SetUp()
    {
        _root = PrefabUtility.LoadPrefabContents(VARIANT);
        var prefabAppearance = _root.GetComponent<NpcHeadAppearance>();
        Assert.IsTrue(prefabAppearance != null, "NPC_base Variant has no NpcHeadAppearance");
        var prefabHead = prefabAppearance.HeadRenderer;
        Assert.IsTrue(prefabHead != null, "NpcHeadAppearance has no Head renderer wired");
        _defaultMesh = prefabHead.sharedMesh;
        _defaultMaterial = prefabHead.sharedMaterial;

        _go = new GameObject("HeadTest");
        _renderer = _go.AddComponent<SkinnedMeshRenderer>();
        _renderer.sharedMesh = _defaultMesh;
        _renderer.bones = prefabHead.bones;
        _renderer.rootBone = prefabHead.rootBone;
        _renderer.sharedMaterial = _defaultMaterial;
        _appearance = _go.AddComponent<NpcHeadAppearance>();
        var so = new SerializedObject(_appearance);
        so.FindProperty("_headRenderer").objectReferenceValue = _renderer;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        if (_go != null) Object.DestroyImmediate(_go);
        if (_root != null) PrefabUtility.UnloadPrefabContents(_root);
    }

    private static HeadAppearanceSO LoadHead(string name)
    {
        var head = AssetDatabase.LoadAssetAtPath<HeadAppearanceSO>($"{HEADS}HeadAppearance_{name}.asset");
        Assert.IsTrue(head != null, $"Missing {HEADS}HeadAppearance_{name}.asset");
        return head;
    }

    private HeadAppearanceSO Wrap(Mesh mesh)
    {
        var head = ScriptableObject.CreateInstance<HeadAppearanceSO>();
        head.name = "TestHead";
        head.EditorSet(mesh, LoadHead("Cook").Material);
        return head;
    }

    private void AssertDefaultKept()
    {
        Assert.AreSame(_defaultMesh, _renderer.sharedMesh);
        Assert.AreSame(_defaultMaterial, _renderer.sharedMaterial);
    }

    [Test]
    public void Variant_WiresTheModelHeadRenderer()
    {
        var appearance = _root.GetComponent<NpcHeadAppearance>();
        Assert.AreEqual(_root.transform.Find("Character/Head"), appearance.HeadRenderer.transform);
    }

    [Test]
    public void Apply_CompatibleHead_SwapsMeshAndMaterial()
    {
        var head = LoadHead("Warden");
        Assert.IsTrue(_appearance.Apply(head));
        Assert.AreSame(head.Mesh, _renderer.sharedMesh);
        Assert.AreSame(head.Material, _renderer.sharedMaterial);
    }

    [Test]
    public void Apply_NullHead_KeepsDefault()
    {
        Assert.IsFalse(_appearance.Apply(null));
        Assert.AreSame(_defaultMesh, _renderer.sharedMesh);
        Assert.AreSame(_defaultMaterial, _renderer.sharedMaterial);
    }

    [Test]
    public void Apply_HeadWithoutMaterial_KeepsRendererMaterial()
    {
        var head = ScriptableObject.CreateInstance<HeadAppearanceSO>();
        head.EditorSet(LoadHead("Cook").Mesh, null);
        try
        {
            Assert.IsTrue(_appearance.Apply(head));
            Assert.AreSame(head.Mesh, _renderer.sharedMesh);
            Assert.AreSame(_defaultMaterial, _renderer.sharedMaterial);
        }
        finally { Object.DestroyImmediate(head); }
    }

    [Test]
    public void Apply_BindPoseCountMismatch_KeepsDefault()
    {
        var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.up, Vector3.right }, triangles = new[] { 0, 1, 2 } };
        mesh.bindposes = new[] { Matrix4x4.identity };
        var head = Wrap(mesh);
        try
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("HeadTest: head TestHead is incompatible.*1 bind poses"));
            Assert.IsFalse(_appearance.Apply(head));
            AssertDefaultKept();
        }
        finally { Object.DestroyImmediate(head); Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void Apply_RendererBoneCountMismatch_KeepsDefault()
    {
        _renderer.bones = new Transform[0];
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("renderer has 0 bones"));
        Assert.IsFalse(_appearance.Apply(LoadHead("Warden")));
        AssertDefaultKept();
    }

    [Test]
    public void IsCompatible_BoneIndexOutOfRange_Rejected()
    {
        var mesh = Object.Instantiate(_defaultMesh);
        try
        {
            var weights = new BoneWeight[mesh.vertexCount];
            for (int i = 0; i < weights.Length; i++) weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1 };
            weights[0] = new BoneWeight { boneIndex0 = mesh.bindposeCount, weight0 = 1 };
            mesh.boneWeights = weights;
            Assert.IsFalse(NpcHeadAppearance.IsCompatible(mesh, _defaultMesh, out string reason));
            StringAssert.Contains("out of range", reason);
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void IsCompatible_NoBoneWeights_Rejected()
    {
        var mesh = Object.Instantiate(_defaultMesh);
        try
        {
            mesh.boneWeights = new BoneWeight[0];
            Assert.IsFalse(NpcHeadAppearance.IsCompatible(mesh, _defaultMesh, out string reason));
            StringAssert.Contains("no bone weights", reason);
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void IsCompatible_DifferentBindPose_Rejected()
    {
        var mesh = Object.Instantiate(_defaultMesh);
        try
        {
            var poses = mesh.bindposes;
            poses[0] = Matrix4x4.Translate(Vector3.up) * poses[0];
            mesh.bindposes = poses;
            Assert.IsFalse(NpcHeadAppearance.IsCompatible(mesh, _defaultMesh, out string reason));
            StringAssert.Contains("bind pose", reason);
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void Apply_NoRenderer_ReturnsFalse()
    {
        var so = new SerializedObject(_appearance);
        so.FindProperty("_headRenderer").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no Head renderer assigned"));
        Assert.IsFalse(_appearance.Apply(LoadHead("Warden")));
    }

    [Test]
    public void ShippedHeads_AreAssignedAndCompatibleWithTheModel()
    {
        foreach (var (npcPath, headName) in Assigned)
        {
            var npc = AssetDatabase.LoadAssetAtPath<NPCEntity>($"{NPCS}{npcPath}.asset");
            Assert.IsTrue(npc != null, $"Missing {NPCS}{npcPath}.asset");
            var head = LoadHead(headName);
            Assert.AreSame(head, npc.Head, $"{npc.name} should use HeadAppearance_{headName}");
            Assert.IsTrue(head.Mesh != null && head.Material != null, $"{headName} is missing its mesh or material");
            Assert.IsTrue(NpcHeadAppearance.IsCompatible(head.Mesh, _defaultMesh, out string reason), $"{headName}: {reason}");
        }
    }
}
