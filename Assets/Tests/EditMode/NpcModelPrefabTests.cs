using Game.AI;
using Game.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Structure guard for spec-npc-base-hero-model: <c>NPC_base Variant</c> nests the <c>Humanoid_BaseHero</c> model
/// (base-hero avatar, body + head SMRs, player sockets, fist hitbox, 11-body ragdoll) and its root wiring points
/// into that model.
/// </summary>
public class NpcModelPrefabTests
{
    private const string VARIANT = "Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab";
    private const string MODEL = "Assets/_Game/Prefabs/Characters/Humanoid_BaseHero.prefab";
    private const string HERO_FBX = "Assets/_Game/Art/Characters/Humanoids/models/test base hero/rigged test hero with textures.fbx";

    private static readonly string[] Sockets =
        { "WeaponSocket", "LeftWeaponSocket", "UndrawnWeaponSocket", "BackWeaponSocket", "HelmetSocket" };

    private GameObject _root;
    private Transform _character;

    [SetUp]
    public void SetUp()
    {
        _root = PrefabUtility.LoadPrefabContents(VARIANT);
        _character = _root.transform.Find("Character");
        Assert.IsNotNull(_character, "NPC_base Variant has no Character child");
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null) PrefabUtility.UnloadPrefabContents(_root);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private Object ReadRef(Component component, string path) =>
        new SerializedObject(component).FindProperty(path).objectReferenceValue;

    [Test]
    public void Character_IsTheBaseHeroModelPrefab()
    {
        Assert.IsNotNull(_character, "Character child missing");
        Object source = PrefabUtility.GetCorrespondingObjectFromSource(_character.gameObject);
        Assert.AreEqual(MODEL, AssetDatabase.GetAssetPath(source));

        var animator = _character.GetComponent<Animator>();
        Assert.IsNotNull(animator);
        Assert.AreEqual(HERO_FBX, AssetDatabase.GetAssetPath(animator.avatar));
        Assert.IsFalse(animator.applyRootMotion);
        Assert.IsNotNull(animator.runtimeAnimatorController);
        Assert.IsNotNull(_character.GetComponent<EntityAnimationEventReceiver>());
    }

    [Test]
    public void Character_HasBodyAndHeadRenderers()
    {
        var body = _character.Find("base_hero")?.GetComponent<SkinnedMeshRenderer>();
        var head = _character.Find("Head")?.GetComponent<SkinnedMeshRenderer>();
        Assert.IsNotNull(body);
        Assert.IsNotNull(head);
        Assert.AreEqual("base_hero_body_baked", body.sharedMesh != null ? body.sharedMesh.name : null);
        Assert.AreEqual("base_hero_head_baked", head.sharedMesh != null ? head.sharedMesh.name : null);
        foreach (SkinnedMeshRenderer smr in new[] { body, head })
        {
            Assert.IsNotNull(smr.sharedMaterial, $"{smr.name} has no material");
            foreach (Transform bone in smr.bones)
                Assert.IsTrue(bone != null && bone.IsChildOf(_character), $"{smr.name} bone not bound to the NPC skeleton");
        }
        Assert.IsNull(FindDeep(_character, "Beta_Surface"), "Mixamo dummy still present");
    }

    [Test]
    public void Character_HasPlayerSocketsAndFistHitbox()
    {
        foreach (string socket in Sockets)
            Assert.IsNotNull(FindDeep(_character, socket), $"{socket} missing");

        Transform unarmed = FindDeep(_character, "UnarmedHitbox");
        Assert.IsNotNull(unarmed);
        Assert.AreEqual("WeaponSocket", unarmed.parent.name);
        Assert.IsNotNull(unarmed.GetComponent<WeaponHitbox>());
        Assert.IsNull(unarmed.GetComponent<Rigidbody>(), "A Rigidbody under the Animator joins the ragdoll");
    }

    [Test]
    public void Character_HasElevenBodyRagdoll()
    {
        Rigidbody[] bodies = _character.GetComponentsInChildren<Rigidbody>(true);
        Assert.AreEqual(11, bodies.Length);
        foreach (Rigidbody body in bodies)
        {
            Assert.IsNotNull(body.GetComponent<Collider>(), $"{body.name} has no collider");
            Assert.IsTrue(body.isKinematic, $"{body.name} must be authored kinematic (ragdoll is enabled only at death)");
            var joint = body.GetComponent<CharacterJoint>();
            if (joint != null)
                Assert.IsTrue(joint.connectedBody != null && joint.connectedBody.transform.IsChildOf(_character),
                    $"{body.name} joint not connected inside the model");
        }
    }

    [Test]
    public void RootWiring_PointsIntoTheNewModel()
    {
        var bridge = _root.GetComponent<Game.Animations.HumanoidAnimationBridge>();
        Assert.AreEqual(_character.GetComponent<Animator>(), ReadRef(bridge, "_animator"));

        var attacker = _root.GetComponent<EntityMeleeAttacker>();
        Assert.AreEqual(1, attacker.Hitboxes.Count);
        Assert.AreEqual("Unarmed", attacker.Hitboxes[0].Id);
        Assert.AreEqual(FindDeep(_character, "UnarmedHitbox").GetComponent<WeaponHitbox>(), attacker.Hitboxes[0].Hitbox);

        var driver = _root.GetComponent<Game.Animations.HumanoidAIAnimationDriver>();
        var pins = new SerializedObject(driver).FindProperty("_transformsToPinToHips");
        Assert.AreEqual(2, pins.arraySize);
        Assert.AreEqual(_root.transform.Find("InteractionCollider"), pins.GetArrayElementAtIndex(0).objectReferenceValue);
        Assert.AreEqual(_character.Find("Hitbox"), pins.GetArrayElementAtIndex(1).objectReferenceValue);
        Assert.AreEqual(7, _character.Find("Hitbox").gameObject.layer);
    }
}
