using System.Collections.Generic;
using Game.Editor.Characters;
using NUnit.Framework;
using UnityEngine;

public class SkinnedNeckValidatorTests
{
    private readonly List<Mesh> _meshes = new List<Mesh>();
    private static readonly string[] Bones = { "Neck", "Spine" };

    [TearDown]
    public void TearDown()
    {
        foreach (var mesh in _meshes) Object.DestroyImmediate(mesh);
        _meshes.Clear();
    }

    private Mesh Fixture(string name = "Head")
    {
        var mesh = new Mesh { name = name };
        mesh.vertices = new[] { new Vector3(0, 1, 0), new Vector3(.1f, 1, 0), new Vector3(0, 2, 0) };
        mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
        mesh.tangents = new[] { new Vector4(1,0,0,1), new Vector4(1,0,0,1), new Vector4(1,0,0,1) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.Translate(Vector3.up) };
        mesh.boneWeights = new[] { Weight(0), Weight(0), Weight(0) };
        _meshes.Add(mesh);
        return mesh;
    }

    private static BoneWeight Weight(int bone) => new BoneWeight { boneIndex0 = bone, weight0 = 1 };

    [Test]
    public void BindSpace_ReorderedBonesMatchByName()
    {
        var canonical = Fixture(); var part = Fixture();
        part.bindposes = new[] { canonical.bindposes[1], canonical.bindposes[0] };
        Assert.That(SkinnedNeckValidator.ValidateBindSpace(part, new[] { "Spine", "Neck" }, canonical, Bones).IsValid, Is.True);
    }

    [Test]
    public void BindSpace_SameNamesDifferentRestPoseIsRejected()
    {
        var canonical = Fixture(); var part = Fixture();
        var poses = part.bindposes; poses[0] = Matrix4x4.Translate(Vector3.right * .02f); part.bindposes = poses;
        var report = SkinnedNeckValidator.ValidateBindSpace(part, Bones, canonical, Bones);
        Assert.That(report.IsValid, Is.False);
        Assert.That(report.MaxBindPoseDelta, Is.GreaterThan(.01f));
        Assert.That(report.Errors[0], Does.Contain("Neck"));
    }

    [Test]
    public void BindSpace_MissingDuplicateAndMalformedNamesFail()
    {
        var mesh = Fixture();
        Assert.That(SkinnedNeckValidator.ValidateBindSpace(mesh, new[] { "Tail", "Spine" }, mesh, Bones).IsValid, Is.False);
        Assert.That(SkinnedNeckValidator.ValidateBindSpace(mesh, new[] { "Neck", "Neck" }, mesh, Bones).IsValid, Is.False);
        Assert.That(SkinnedNeckValidator.ValidateBindSpace(mesh, new[] { "Neck" }, mesh, Bones).IsValid, Is.False);
    }

    [Test]
    public void ExactJoin_ChangedFaceAboveInterfaceRemainsCompatible()
    {
        var baseHead = Fixture(); var secondHead = Fixture("Different face");
        var vertices = secondHead.vertices; vertices[2] += new Vector3(.04f, .2f, .03f); secondHead.vertices = vertices;
        var report = SkinnedNeckValidator.ValidateExactJoin(baseHead, Bones, secondHead, Bones, 1);
        Assert.That(report.IsValid, Is.True, string.Join(";", report.Errors));
        Assert.That(report.Samples, Is.EqualTo(4));
    }

    [Test]
    public void ExactJoin_MovedInterfaceFailsDespiteCompatibleSkeleton()
    {
        var a = Fixture(); var b = Fixture();
        var vertices = b.vertices; vertices[0] += Vector3.right * .01f; b.vertices = vertices;
        var report = SkinnedNeckValidator.ValidateExactJoin(a, Bones, b, Bones, 1);
        Assert.That(report.IsValid, Is.False);
        Assert.That(report.MaxGap, Is.GreaterThan(.009f));
    }

    [Test]
    public void ExactJoin_DifferentInfluenceAndNormalAreReported()
    {
        var a = Fixture(); var b = Fixture();
        var weights = b.boneWeights; weights[0] = Weight(1); b.boneWeights = weights;
        var normals = b.normals; normals[1] = Vector3.right; b.normals = normals;
        var report = SkinnedNeckValidator.ValidateExactJoin(a, Bones, b, Bones, 1);
        Assert.That(report.IsValid, Is.False);
        Assert.That(report.MaxWeightL1, Is.EqualTo(2).Within(.00001f));
        Assert.That(report.MaxNormalDegrees, Is.EqualTo(90).Within(.01f));
    }

    [Test]
    public void ExactJoin_CoincidentUvSplitCandidatesAreMatchedByAttributes()
    {
        var a = Fixture(); var b = Fixture();
        b.vertices = new[] { b.vertices[0], b.vertices[0], b.vertices[1], b.vertices[2] };
        b.normals = new[] { Vector3.right, Vector3.forward, Vector3.forward, Vector3.forward };
        b.tangents = new[] { new Vector4(1,0,0,1), new Vector4(1,0,0,1), new Vector4(1,0,0,1), new Vector4(1,0,0,1) };
        b.uv = new[] { Vector2.one, Vector2.zero, Vector2.right, Vector2.up };
        b.boneWeights = new[] { Weight(0), Weight(0), Weight(0), Weight(0) };
        b.triangles = new[] { 1, 2, 3 };
        // Both sides contain the same UV seam duplicate; array order differs.
        a.vertices = new[] { a.vertices[0], a.vertices[1], a.vertices[2], a.vertices[0] };
        a.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.right };
        a.tangents = new[] { new Vector4(1,0,0,1), new Vector4(1,0,0,1), new Vector4(1,0,0,1), new Vector4(1,0,0,1) };
        a.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        a.boneWeights = new[] { Weight(0), Weight(0), Weight(0), Weight(0) };
        var report = SkinnedNeckValidator.ValidateExactJoin(a, Bones, b, Bones, 1);
        Assert.That(report.IsValid, Is.True, string.Join(";", report.Errors));
    }

    [Test]
    public void ExactJoin_ReorderedWeightIndicesUseBoneNames()
    {
        var a = Fixture(); var b = Fixture();
        b.bindposes = new[] { a.bindposes[1], a.bindposes[0] };
        b.boneWeights = new[] { Weight(1), Weight(1), Weight(1) };
        Assert.That(SkinnedNeckValidator.ValidateExactJoin(a, Bones, b, new[] { "Spine", "Neck" }, 1).IsValid, Is.True);
    }

    [Test]
    public void NeckUv_CollapsedUvsFailAndValidTrianglePasses()
    {
        var mesh = Fixture();
        Assert.That(SkinnedNeckValidator.ValidateNeckUV(mesh, .9f, 2.1f, 1).IsValid, Is.True);
        mesh.uv = new[] { Vector2.zero, Vector2.zero, Vector2.zero };
        var report = SkinnedNeckValidator.ValidateNeckUV(mesh, .9f, 2.1f, 1);
        Assert.That(report.IsValid, Is.False);
        Assert.That(report.DegenerateUvTriangles, Is.EqualTo(1));
    }

    [Test]
    public void NeckUv_EmptyScopeIsNotCertified()
    {
        Assert.That(SkinnedNeckValidator.ValidateNeckUV(Fixture(), 3, 4, 1).IsValid, Is.False);
    }

    [Test]
    public void BakeUvRepair_RepairsCollapsedTriangleAndPreservesPositionsAndWeights()
    {
        var mesh = Fixture();
        var vertices = mesh.vertices;
        var weights = mesh.boneWeights;
        mesh.uv = new[] { Vector2.zero, Vector2.zero, Vector2.zero };
        Assert.That(SkinnedNeckBaker.RepairNeckUV(mesh,.9f,2.1f,1),Is.GreaterThan(0));
        Assert.That(SkinnedNeckValidator.ValidateNeckUV(mesh,.9f,2.1f,1).IsValid,Is.True);
        Assert.That(mesh.vertexCount,Is.EqualTo(vertices.Length+3));
        for(int i=0;i<vertices.Length;i++)
        {
            Assert.That(mesh.vertices[i],Is.EqualTo(vertices[i]));
            Assert.That(mesh.boneWeights[i],Is.EqualTo(weights[i]));
            Assert.That(mesh.vertices[i+vertices.Length],Is.EqualTo(vertices[i]));
            Assert.That(mesh.boneWeights[i+vertices.Length],Is.EqualTo(weights[i]));
        }
    }

    [Test]
    public void BakeUvRepair_PreservesValidSkinInterfaceOutsideRequestedBand()
    {
        var mesh = Fixture();
        var uv = mesh.uv;
        var tangents = mesh.tangents;
        Assert.That(SkinnedNeckBaker.RepairNeckUV(mesh,3,4,1),Is.EqualTo(0));
        Assert.That(mesh.uv,Is.EqualTo(uv));
        Assert.That(mesh.tangents,Is.EqualTo(tangents));
    }

    [Test]
    public void BakeUvRepair_DoesNotChangeSharedNeighbourOutsideTheNeckBand()
    {
        var mesh=Fixture();
        mesh.vertices=new[]{Vector3.up,new Vector3(.1f,1,0),new Vector3(0,2,0),new Vector3(1,3,0)};
        mesh.normals=new[]{Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward};
        mesh.tangents=new[]{new Vector4(1,0,0,1),new Vector4(1,0,0,1),new Vector4(1,0,0,1),new Vector4(1,0,0,1)};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.right*2,Vector2.up};
        mesh.boneWeights=new[]{Weight(0),Weight(0),Weight(0),Weight(0)};
        mesh.triangles=new[]{0,1,2,1,3,2};
        var uv=mesh.uv;var tangent=mesh.tangents;
        Assert.That(SkinnedNeckBaker.RepairNeckUV(mesh,.9f,1.5f,1),Is.EqualTo(1));
        for(int i=0;i<4;i++)
        {
            Assert.That(mesh.uv[i],Is.EqualTo(uv[i]));
            Assert.That(mesh.tangents[i],Is.EqualTo(tangent[i]));
        }
        Assert.That(new[]{mesh.triangles[3],mesh.triangles[4],mesh.triangles[5]},Is.EqualTo(new[]{1,3,2}));
    }

    [Test]
    public void BakeUvRepair_PreservesSubmeshesWhenPromotingIndexFormat()
    {
        var mesh=Fixture();
        var vertices=new Vector3[65535];
        var normals=new Vector3[65535];
        var uv=new Vector2[65535];
        vertices[0]=Vector3.up; vertices[1]=new Vector3(.1f,1,0); vertices[2]=new Vector3(0,2,0);
        for(int i=0;i<normals.Length;i++) normals[i]=Vector3.forward;
        mesh.Clear(); mesh.vertices=vertices; mesh.normals=normals; mesh.uv=uv;
        var weights=new BoneWeight[vertices.Length];
        for(int i=0;i<weights.Length;i++)weights[i]=Weight(0);
        mesh.boneWeights=weights;
        mesh.subMeshCount=2;
        mesh.SetTriangles(new[]{0,1,2},0);
        mesh.SetTriangles(new[]{0,2,1},1);
        Assert.That(SkinnedNeckBaker.RepairNeckUV(mesh,.9f,2.1f,1),Is.EqualTo(2));
        Assert.That(mesh.indexFormat,Is.EqualTo(UnityEngine.Rendering.IndexFormat.UInt32));
        Assert.That(mesh.subMeshCount,Is.EqualTo(2));
        Assert.That(mesh.GetTriangles(1),Has.Length.EqualTo(3));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExactJoin_TangentDirectionOrHandednessMismatchFails(bool handedness)
    {
        var head=Fixture();var body=Fixture();var tangent=head.tangents;
        tangent[0]=handedness?new Vector4(1,0,0,-1):new Vector4(0,1,0,1);
        head.tangents=tangent;
        var report=SkinnedNeckValidator.ValidateExactJoin(head,Bones,body,Bones,1);
        Assert.That(report.IsValid,Is.False);
        if(handedness)Assert.That(report.TangentHandednessMismatches,Is.GreaterThan(0));
        else Assert.That(report.MaxTangentDegrees,Is.EqualTo(90).Within(.01f));
        SkinnedNeckBaker.AlignSeamTangents(head,body,1);
        Assert.That(SkinnedNeckValidator.ValidateExactJoin(head,Bones,body,Bones,1).IsValid,Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Coverage_GarmentIsDoubleSidedAndDistanceLimited(bool reversed)
    {
        var vertices = new[] { new Vector3(-1, -1, .02f), new Vector3(1, -1, .02f), new Vector3(0, 1, .02f) };
        var triangles = reversed ? new[] { 2, 1, 0 } : new[] { 0, 1, 2 };
        var covered = SkinnedNeckValidator.ValidateCoverage(new[] { Vector3.zero }, new[] { Vector3.forward }, vertices, triangles, .03f);
        Assert.That(covered.IsValid, Is.True);
        Assert.That(covered.MaxCoverageDistance, Is.EqualTo(.02f).Within(.00001f));
        Assert.That(SkinnedNeckValidator.ValidateCoverage(new[] { Vector3.zero }, new[] { Vector3.forward }, vertices, triangles, .01f).IsValid, Is.False);
        Assert.That(SkinnedNeckValidator.ValidateCoverage(new[] { Vector3.right * 2 }, new[] { Vector3.forward }, vertices, triangles, .03f).IsValid, Is.False);
    }

    [Test]
    public void Coverage_InvalidIndicesAndZeroNormalsReturnDiagnostics()
    {
        var vertices = new[] { Vector3.zero, Vector3.up, Vector3.right };
        Assert.That(SkinnedNeckValidator.ValidateCoverage(new[] { Vector3.zero }, new[] { Vector3.forward }, vertices, new[] { 0, 1, 9 }, .1f).IsValid, Is.False);
        Assert.That(SkinnedNeckValidator.ValidateCoverage(new[] { Vector3.zero }, new[] { Vector3.zero }, vertices, new[] { 0, 1, 2 }, .1f).IsValid, Is.False);
    }
}
