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
    // spec-base-body-regen: the v2 avatar (flattened v2 rig, all five fingers) replaces the old FBX avatar.
    private const string HERO_AVATAR = "Assets/_Game/Art/Characters/Humanoids/models/base hero v2/BaseHeroV2Avatar.asset";
    private const string PLAYER = "Assets/_Game/Prefabs/Player/Player.prefab";

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
        Assert.AreEqual(HERO_AVATAR, AssetDatabase.GetAssetPath(animator.avatar));
        Assert.IsFalse(animator.applyRootMotion);
        Assert.IsNotNull(animator.runtimeAnimatorController);
        Assert.IsNotNull(_character.GetComponent<EntityAnimationEventReceiver>());
    }

    private static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
    private static readonly string[] Phalanges = { "Proximal", "Intermediate", "Distal" };

    [Test]
    public void Avatar_MapsAllThirtyFingerBones()
    {
        var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(HERO_AVATAR);
        Assert.IsNotNull(avatar);
        Assert.IsTrue(avatar.isValid && avatar.isHuman);
        var mapped = new System.Collections.Generic.HashSet<string>();
        foreach (HumanBone bone in avatar.humanDescription.human) mapped.Add(bone.humanName);
        foreach (string side in new[] { "Left", "Right" })
            foreach (string finger in Fingers)
                foreach (string phalanx in Phalanges)
                    Assert.IsTrue(mapped.Contains($"{side} {finger} {phalanx}"), $"{side} {finger} {phalanx} not mapped");
    }

    [Test]
    public void Character_AnimatorResolvesEveryFingerBone()
    {
        // The avatar must bind to the prefab hierarchy (Hips directly under the model root), not only be valid.
        var animator = _character.GetComponent<Animator>();
        animator.Rebind();
        foreach (string side in new[] { "Left", "Right" })
            foreach (string finger in Fingers)
                foreach (string phalanx in Phalanges)
                {
                    var bone = (HumanBodyBones)System.Enum.Parse(typeof(HumanBodyBones), side + finger + phalanx);
                    Assert.IsNotNull(animator.GetBoneTransform(bone), $"{bone} does not resolve on the NPC model");
                }
    }

    [Test]
    public void Player_UsesTheV2AvatarOnBothAnimators()
    {
        var player = PrefabUtility.LoadPrefabContents(PLAYER);
        try
        {
            // The root Animator and the Character model's Animator; nothing may keep the old three-finger avatar.
            int v2 = 0;
            foreach (var animator in player.GetComponentsInChildren<Animator>(true))
            {
                string path = animator.avatar != null ? AssetDatabase.GetAssetPath(animator.avatar) : "";
                Assert.IsFalse(path.EndsWith("rigged test hero with textures.fbx"), $"{animator.name} keeps the old avatar");
                if (path == HERO_AVATAR) v2++;
            }
            Assert.GreaterOrEqual(v2, 2);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
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

        // spec-unarmed-combo-rework: left fist for the lead jab / uppercut
        Transform unarmedLeft = FindDeep(_character, "UnarmedHitbox_L");
        Assert.IsNotNull(unarmedLeft);
        Assert.AreEqual("LeftWeaponSocket", unarmedLeft.parent.name);
        Assert.IsNotNull(unarmedLeft.GetComponent<WeaponHitbox>());
        Assert.IsNull(unarmedLeft.GetComponent<Rigidbody>(), "A Rigidbody under the Animator joins the ragdoll");
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
        Assert.AreEqual(2, attacker.Hitboxes.Count);
        Assert.AreEqual("Unarmed", attacker.Hitboxes[0].Id);
        Assert.AreEqual(FindDeep(_character, "UnarmedHitbox").GetComponent<WeaponHitbox>(), attacker.Hitboxes[0].Hitbox);
        Assert.AreEqual("UnarmedLeft", attacker.Hitboxes[1].Id);
        Assert.AreEqual(FindDeep(_character, "UnarmedHitbox_L").GetComponent<WeaponHitbox>(), attacker.Hitboxes[1].Hitbox);

        var driver = _root.GetComponent<Game.Animations.HumanoidAIAnimationDriver>();
        var pins = new SerializedObject(driver).FindProperty("_transformsToPinToHips");
        Assert.AreEqual(2, pins.arraySize);
        Assert.AreEqual(_root.transform.Find("InteractionCollider"), pins.GetArrayElementAtIndex(0).objectReferenceValue);
        Assert.AreEqual(_character.Find("Hitbox"), pins.GetArrayElementAtIndex(1).objectReferenceValue);
        Assert.AreEqual(7, _character.Find("Hitbox").gameObject.layer);
    }
}
