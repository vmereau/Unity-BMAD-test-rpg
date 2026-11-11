// Unity MCP execute_code method body. Read this file and pass its contents as code.
// Runs in an isolated preview scene; does not modify prefabs, clips or materials.
bool closeups = false;
bool bare = false;
bool alternateHead = false;
bool neutral = false;
var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab");
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
Camera camera = null;
RenderTexture rt = null;
Mesh headMesh = null, bodyMesh = null, armorMesh = null, alternateMesh = null;
Material neutralMaterial = null, alternateMaterial = null;
try
{
var root = new GameObject("NeckVerification");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
var character = UnityEngine.Object.Instantiate(player.transform.Find("Character").gameObject, root.transform);
character.name = "Character";
foreach (var oldAnimator in character.GetComponentsInChildren<Animator>()) UnityEngine.Object.DestroyImmediate(oldAnimator);
var animator = root.AddComponent<Animator>();
animator.avatar = player.GetComponent<Animator>().avatar;
animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
animator.applyRootMotion = false;
animator.fireEvents = false;
var renderers = character.GetComponentsInChildren<SkinnedMeshRenderer>();
var body = System.Array.Find(renderers, r => r.name == "base_hero");
var head = System.Array.Find(renderers, r => r.name == "Head");
var outfit = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
    "Assets/_Game/Prefabs/Items/Armors/CompanyGruntArmor_Visual.prefab"), character.transform);
var part = outfit.GetComponent<Game.Inventory.SkinnedBodyPart>();
if(neutral)
{
    neutralMaterial = new Material(part.Renderer.sharedMaterial);
    neutralMaterial.SetTexture("_BaseMap",null);
    neutralMaterial.SetTexture("_BumpMap",null);
    neutralMaterial.DisableKeyword("_NORMALMAP");
    neutralMaterial.SetColor("_BaseColor",Color.gray);
    head.sharedMaterial=neutralMaterial;
    body.sharedMaterial=neutralMaterial;
    part.Renderer.sharedMaterial=neutralMaterial;
}
if (!Game.Inventory.SkinnedMeshRebinder.TryBind(part.Renderer, part.BoneNames, part.RootBoneName,
    Game.Inventory.SkinnedMeshRebinder.IndexBones(body.bones, body.rootBone), body.rootBone, new System.Collections.Generic.List<string>()))
    throw new System.Exception("Could not bind verification outfit.");
foreach (var r in character.GetComponentsInChildren<SkinnedMeshRenderer>()) { r.updateWhenOffscreen = true; r.forceMatrixRecalculationPerRender = true; }
body.enabled = bare;
outfit.SetActive(!bare);
if (alternateHead)
{
    alternateMesh = UnityEngine.Object.Instantiate(head.sharedMesh);
    var vertices = alternateMesh.vertices;
    for(int i=0;i<vertices.Length;i++)
    {
        float blend=Mathf.Clamp01((vertices[i].y-1.65f)/.05f);
        vertices[i].x *= 1+.12f*blend;
        vertices[i].y -= .012f*blend;
    }
    alternateMesh.vertices=vertices;
    alternateMesh.RecalculateBounds();
    head.sharedMesh=alternateMesh;
    alternateMaterial=new Material(head.sharedMaterial);
    alternateMaterial.SetColor("_BaseColor",new Color(.74f,.82f,.90f));
    head.sharedMaterial=alternateMaterial;
}
foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
var cameraObject = new GameObject("NeckCamera");
cameraObject.transform.SetParent(root.transform);
cameraObject.layer = 31;
camera = cameraObject.AddComponent<Camera>();
camera.scene = scene;
camera.overrideSceneCullingMask = UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(scene);
camera.cullingMask = 1 << 31;
camera.clearFlags = CameraClearFlags.SolidColor;
camera.backgroundColor = new Color(.11f, .12f, .14f);
camera.nearClipPlane = .01f;
camera.farClipPlane = 20;
camera.fieldOfView = 30;
var lightObject = new GameObject("NeckLight");
lightObject.transform.SetParent(root.transform);
lightObject.transform.rotation = Quaternion.Euler(30, 145, 0);
var light = lightObject.AddComponent<Light>();
light.type = LightType.Directional;
light.intensity = 2;
light.cullingMask = 1 << 31;
var fillObject = new GameObject("NeckFill");
fillObject.transform.SetParent(root.transform);
fillObject.transform.rotation = Quaternion.Euler(20,-35,0);
var fill = fillObject.AddComponent<Light>();
fill.type = LightType.Directional;
fill.intensity = .7f;
fill.cullingMask = 1 << 31;
rt = new RenderTexture(320, 320, 24);
camera.targetTexture = rt;

string folder="Temp/CompanyArmorAudit"; System.IO.Directory.CreateDirectory(folder);
var clips=new[]{"Idle.fbx","Walking.fbx","Running.fbx","Combat/one handed/AttackOverhead.fbx","Combat/dodge roll.fbx"};
foreach(var clipPath in clips){
var clip=AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/Characters/Humanoids/Animations/"+clipPath).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
var graph=UnityEngine.Playables.PlayableGraph.Create("CompanyPreview");
try{
var output=UnityEngine.Animations.AnimationPlayableOutput.Create(graph,"out",animator);
var playable=UnityEngine.Animations.AnimationClipPlayable.Create(graph,clip);
playable.SetApplyFootIK(false);
UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output,playable);
var sheet=new Texture2D(1280,1280,TextureFormat.RGB24,false);
try{
for(int frame=0;frame<4;frame++){
UnityEngine.Playables.PlayableExtensions.SetTime(playable,clip.length*frame/4);graph.Evaluate(0);
var hip=animator.GetBoneTransform(HumanBodyBones.Hips).position;
var target=closeups ? animator.GetBoneTransform(HumanBodyBones.Neck).position : new Vector3(hip.x,.95f+hip.y-1f,hip.z);
var offsets=new[]{new Vector3(0,.15f,3.9f),new Vector3(3.9f,.15f,0),new Vector3(0,.15f,-3.9f),new Vector3(-3.9f,.15f,0)};
if(closeups) for(int v=0;v<offsets.Length;v++) offsets[v]*=.19f;
for(int view=0;view<4;view++){
camera.transform.position=target+offsets[view];camera.transform.LookAt(target);camera.Render();
var old=RenderTexture.active;try{RenderTexture.active=rt;sheet.ReadPixels(new Rect(0,0,320,320),frame*320,(3-view)*320);}finally{RenderTexture.active=old;}
}}
sheet.Apply();System.IO.File.WriteAllBytes(folder+(closeups ? "/final_neck_" : "/final_full_")+System.IO.Path.GetFileNameWithoutExtension(clipPath)+".png",sheet.EncodeToPNG());
}finally{UnityEngine.Object.DestroyImmediate(sheet);}
}finally{graph.Destroy();}
}return folder;
}finally{
if(camera!=null)camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);
if(alternateMesh!=null)UnityEngine.Object.DestroyImmediate(alternateMesh);
if(neutralMaterial!=null)UnityEngine.Object.DestroyImmediate(neutralMaterial);
if(alternateMaterial!=null)UnityEngine.Object.DestroyImmediate(alternateMaterial);
if(scene.IsValid())UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}


