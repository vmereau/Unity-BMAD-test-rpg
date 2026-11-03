using System.Collections.Generic;
using Game.Inventory;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for binding skinned body parts (armor) to the player skeleton by bone name — SkinnedMeshRebinder.
/// </summary>
public class SkinnedMeshRebinderTests
{
    private GameObject _root;
    private Transform _hips, _spine, _neck;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Character");
        _hips = new GameObject("mixamorig:Hips").transform;
        _hips.SetParent(_root.transform);
        _spine = new GameObject("mixamorig:Spine").transform;
        _spine.SetParent(_hips);
        _neck = new GameObject("mixamorig:Neck").transform;
        _neck.SetParent(_spine);
    }

    private readonly List<Mesh> _meshes = new List<Mesh>();

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
        foreach (var m in _meshes) Object.DestroyImmediate(m);
        _meshes.Clear();
    }

    private SkinnedMeshRenderer MakePart(int bindPoseCount)
    {
        var smr = new GameObject("Armor").AddComponent<SkinnedMeshRenderer>();
        smr.transform.SetParent(_root.transform);
        var mesh = new Mesh();
        var poses = new Matrix4x4[bindPoseCount];
        for (int i = 0; i < poses.Length; i++) poses[i] = Matrix4x4.identity;
        mesh.bindposes = poses;
        _meshes.Add(mesh);
        smr.sharedMesh = mesh;
        return smr;
    }

    [Test]
    public void ResolveBones_AllFound_KeepsOrder()
    {
        var skeleton = SkinnedMeshRebinder.IndexSkeleton(_hips);
        var missing = new List<string>();
        var bones = SkinnedMeshRebinder.ResolveBones(new[] { "mixamorig:Neck", "mixamorig:Hips", "mixamorig:Spine" }, skeleton, missing);
        Assert.That(bones, Is.EqualTo(new[] { _neck, _hips, _spine }));
        Assert.That(missing, Is.Empty);
    }

    [Test]
    public void ResolveBones_MissingBone_ReturnsNullAndReportsName()
    {
        var skeleton = SkinnedMeshRebinder.IndexSkeleton(_hips);
        var missing = new List<string>();
        var bones = SkinnedMeshRebinder.ResolveBones(new[] { "mixamorig:Hips", "mixamorig:LeftHandPinky1" }, skeleton, missing);
        Assert.That(bones, Is.Null);
        Assert.That(missing, Is.EqualTo(new[] { "mixamorig:LeftHandPinky1" }));
    }

    [Test]
    public void TryBind_MissingBone_LeavesRendererUntouched()
    {
        var smr = MakePart(2);
        var skeleton = SkinnedMeshRebinder.IndexSkeleton(_hips);

        bool ok = SkinnedMeshRebinder.TryBind(smr, new[] { "mixamorig:Hips", "mixamorig:Tail" }, "mixamorig:Hips", skeleton, _hips, new List<string>());

        Assert.That(ok, Is.False);
        Assert.That(smr.bones, Is.Empty);
        Assert.That(smr.rootBone, Is.Null);
    }

    [Test]
    public void TryBind_ResolvesRootBone_AndFallsBackWhenAbsent()
    {
        var smr = MakePart(2);
        var skeleton = SkinnedMeshRebinder.IndexSkeleton(_hips);
        var names = new[] { "mixamorig:Hips", "mixamorig:Spine" };

        Assert.That(SkinnedMeshRebinder.TryBind(smr, names, "mixamorig:Spine", skeleton, _hips, null), Is.True);
        Assert.That(smr.rootBone, Is.EqualTo(_spine));
        Assert.That(smr.bones, Is.EqualTo(new[] { _hips, _spine }));

        Assert.That(SkinnedMeshRebinder.TryBind(smr, names, "mixamorig:Missing", skeleton, _hips, null), Is.True);
        Assert.That(smr.rootBone, Is.EqualTo(_hips));
    }

    [Test]
    public void TryBind_NameCountDiffersFromBindPoses_Fails()
    {
        var smr = MakePart(3);
        var skeleton = SkinnedMeshRebinder.IndexSkeleton(_hips);
        var missing = new List<string>();

        bool ok = SkinnedMeshRebinder.TryBind(smr, new[] { "mixamorig:Hips", "mixamorig:Spine" }, "mixamorig:Hips", skeleton, _hips, missing);

        Assert.That(ok, Is.False);
        Assert.That(smr.bones, Is.Empty);
        Assert.That(missing, Has.Count.EqualTo(1));
    }

    [Test]
    public void IndexBones_OnlyIndexesListedBones()
    {
        var socket = new GameObject("mixamorig:Neck").transform; // e.g. a weapon child reusing a bone name
        socket.SetParent(_hips);
        var map = SkinnedMeshRebinder.IndexBones(new[] { _spine, _neck }, _hips);
        Assert.That(map["mixamorig:Neck"], Is.SameAs(_neck));
        Assert.That(map["mixamorig:Hips"], Is.SameAs(_hips));
        Assert.That(map.Count, Is.EqualTo(3));
    }
}
