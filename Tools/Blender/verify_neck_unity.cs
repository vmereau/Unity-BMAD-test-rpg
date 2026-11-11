// Unity MCP execute_code method body. Read this file and pass its contents as code.
// Runs in an isolated preview scene; does not modify prefabs, clips or materials.
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
    "Assets/_Game/Prefabs/Items/Armors/LeatherArmor_Visual.prefab"), character.transform);
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
foreach (var r in character.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
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
headMesh = new Mesh();
bodyMesh = new Mesh();
armorMesh = new Mesh();
var restHead = head.sharedMesh.vertices;
var restBody = body.sharedMesh.vertices;
var pairs = new System.Collections.Generic.List<(int head, int body)>();
for (int i = 0; i < restBody.Length; i++)
{
    if (Mathf.Abs(restBody[i].y - 1.585f) > .00001f) continue;
    int best = 0;
    float distance = float.MaxValue;
    for (int j = 0; j < restHead.Length; j++)
    {
        float d = (restHead[j] - restBody[i]).sqrMagnitude;
        if (d < distance) { distance = d; best = j; }
    }
    pairs.Add((best, i));
}
// Weld positional UV splits for geometric boundary discovery only.
var geometric = new System.Collections.Generic.Dictionary<Vector3Int, int>();
var ids = new int[restHead.Length];
var representative = new System.Collections.Generic.List<int>();
for (int i = 0; i < restHead.Length; i++)
{
    var p = restHead[i] * 100000;
    var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
    if (!geometric.TryGetValue(key, out int id)) { id = geometric.Count; geometric.Add(key, id); representative.Add(i); }
    ids[i] = id;
}
var edges = new System.Collections.Generic.Dictionary<(int, int), int>();
var ht = head.sharedMesh.triangles;
for (int i = 0; i < ht.Length; i += 3)
    for (int k = 0; k < 3; k++)
    {
        int a = ids[ht[i + k]], b = ids[ht[i + (k + 1) % 3]];
        if (a == b) continue;
        var edge = a < b ? (a, b) : (b, a);
        edges.TryGetValue(edge, out int count); edges[edge] = count + 1;
    }
var boundarySet = new System.Collections.Generic.HashSet<int>();
foreach (var edge in edges)
    if (edge.Value == 1)
        foreach (int id in new[] { edge.Key.Item1, edge.Key.Item2 })
        {
            int index = representative[id];
            if (restHead[index].y < 1.5849f) boundarySet.Add(index);
        }
var boundary = new System.Collections.Generic.List<int>(boundarySet);
boundary.Sort((a,b) => Mathf.Atan2(restHead[a].z, restHead[a].x).CompareTo(Mathf.Atan2(restHead[b].z, restHead[b].x)));
if (boundary.Count > 0 && boundary.Count < 12) throw new System.Exception("Incomplete hidden neck boundary: " + boundary.Count);
if(pairs.Count<3 || head.sharedMesh.bounds.min.y>1.481f)
    throw new System.Exception("Missing canonical neck interface or extended stump.");
var canonicalNames=System.Array.ConvertAll(body.bones,b=>b.name);
var restJoin=Game.Editor.Characters.SkinnedNeckValidator.ValidateExactJoin(head.sharedMesh,canonicalNames,body.sharedMesh,canonicalNames,1.585f);
if(!restJoin.IsValid) throw new System.Exception(string.Join(";",restJoin.Errors));
var samples = new System.Collections.Generic.List<int>();
for (int i = 0; i < 24 && boundary.Count > 0; i++) samples.Add(boundary[i * boundary.Count / 24]);
var offsets = new[] { new Vector3(0,.03f,.75f), new Vector3(.75f,.03f,0), new Vector3(0,.03f,-.75f), new Vector3(-.75f,.03f,0) };
string folder = "Temp/ArmorSeamAudit"+(neutral?"/neutral":bare?"/bare":alternateHead?"/alternate":"");
System.IO.Directory.CreateDirectory(folder);
var report = new System.Text.StringBuilder();
report.AppendLine("Open hidden neck boundary vertices: " + boundary.Count + "; sampled per pose: " + samples.Count + "; exposed join pairs: " + pairs.Count);
if(boundary.Count==0) report.AppendLine("Head neck stump is geometrically closed; no lower open boundary requires garment occlusion.");
var clips = new[] { "Idle.fbx", "Walking.fbx", "Running.fbx", "Combat/one handed/AttackOverhead.fbx", "Combat/dodge roll.fbx" };
var restArmor=part.Renderer.sharedMesh.vertices;
var armorUV=part.Renderer.sharedMesh.uv;
var rim=new System.Collections.Generic.List<int>();
for(int i=0;i<restArmor.Length;i++)
    // Generated outer/inner rim rows have explicit atlas coordinates. Height
    // alone also selects unrelated source garment vertices; radius would hide
    // exactly the outward flare this check must detect.
    if(Mathf.Abs(restArmor[i].y-1.56f)<.00001f &&
       (Mathf.Abs(armorUV[i].y-(.80f+.13f*3/7))<.00001f ||
        Mathf.Abs(armorUV[i].y-(.80f+.13f*5/7))<.00001f)) rim.Add(i);
rim.Sort((a,b)=>Mathf.Atan2(restArmor[a].z,restArmor[a].x).CompareTo(Mathf.Atan2(restArmor[b].z,restArmor[b].x)));
if(rim.Count<12)throw new System.Exception("Missing expected collar rim for deformation scan.");
var rimPairs=new System.Collections.Generic.List<(int armor,int head,float restDistance)>();
for(int i=0;i<24;i++)
{
    int index=rim[i*rim.Count/24],best=0;float distance=float.PositiveInfinity;
    for(int j=0;j<restHead.Length;j++)
    {
        float d=Vector3.Distance(part.Renderer.transform.TransformPoint(restArmor[index]),head.transform.TransformPoint(restHead[j]));
        if(d<distance){distance=d;best=j;}
    }
    rimPairs.Add((index,best,distance));
}
var headWeights=head.sharedMesh.boneWeights;
var armorWeights=part.Renderer.sharedMesh.boneWeights;
foreach(int index in rim)
{
    if(new Vector2(restArmor[index].x,restArmor[index].z).magnitude>.106f)
        throw new System.Exception("Collar rim exceeds neck silhouette; possible chin sampling regression.");
    var w=armorWeights[index];
    float headInfluence=0;
    foreach(var influence in new[]{(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)})
        if(part.Renderer.bones[influence.Item1].name.Split(':').Last()=="Head")headInfluence+=influence.Item2;
    if(headInfluence>.0001f)throw new System.Exception("Garment rim follows Head instead of the neck.");
}
Matrix4x4[] WorldSkinMatrices(SkinnedMeshRenderer renderer)
{
    var bind=renderer.sharedMesh.bindposes;var result=new Matrix4x4[bind.Length];
    for(int i=0;i<result.Length;i++)result[i]=renderer.bones[i].localToWorldMatrix*bind[i];
    return result;
}
Vector3 SkinPoint(int index,Vector3[] vertices,BoneWeight[] weights,Matrix4x4[] matrices)
{
    var w=weights[index];var p=vertices[index];
    return matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+
        matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+
        matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+
        matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
}
float maximumGap = 0;
int totalCoverageFailures = 0;
int poseCount = 0;
    foreach (string clipPath in clips)
    {
        AnimationClip clip = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/Characters/Humanoids/Animations/" + clipPath))
            if (asset is AnimationClip c && !c.name.StartsWith("__preview")) { clip = c; break; }
        if (clip == null) throw new System.Exception("Missing clip: " + clipPath);
        animator.Rebind();
        var graph = UnityEngine.Playables.PlayableGraph.Create("NeckPose");
        var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "Pose", animator);
        var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, playable);
        graph.Play();
        var sheet = new Texture2D(1600, 1280, TextureFormat.RGB24, false);
        try
        {
            int sourceFrames=Mathf.Max(1,Mathf.CeilToInt(clip.length*clip.frameRate));
            float peakTime=0,peakChange=-1;int peakFrame=0;
            for(int sourceFrame=0;sourceFrame<=sourceFrames;sourceFrame++)
            {
                float time=Mathf.Min(clip.length,sourceFrame/clip.frameRate);
                UnityEngine.Playables.PlayableExtensions.SetTime(playable,time);graph.Evaluate(0);
                var hm=WorldSkinMatrices(head);var am=WorldSkinMatrices(part.Renderer);
                float change=0;
                foreach(var pair in rimPairs)
                {
                    float distance=Vector3.Distance(SkinPoint(pair.armor,restArmor,armorWeights,am),SkinPoint(pair.head,restHead,headWeights,hm));
                    change=Mathf.Max(change,Mathf.Abs(distance-pair.restDistance));
                }
                if(change>peakChange){peakChange=change;peakTime=time;peakFrame=sourceFrame;}
            }
            report.AppendLine(clipPath+" full-frame scan="+(sourceFrames+1)+" maximum rim-pair deformation="+peakChange.ToString("F6")+" at frame="+peakFrame+" time="+peakTime.ToString("F4")+"; captured in last column. This proxy selects extrema; it is not an intersection certificate.");
            var times=new[]{0,clip.length*.25f,clip.length*.5f,clip.length*.75f,peakTime};
            for (int frame = 0; frame < 5; frame++)
            {
                float time = times[frame];
                UnityEngine.Playables.PlayableExtensions.SetTime(playable, time);
                graph.Evaluate(0);
                head.BakeMesh(headMesh);
                body.BakeMesh(bodyMesh);
                part.Renderer.BakeMesh(armorMesh);
                var hp = headMesh.vertices;
                var bp = bodyMesh.vertices;
                for(int i=0;i<hp.Length;i++) hp[i]=head.transform.TransformPoint(hp[i]);
                for(int i=0;i<bp.Length;i++) bp[i]=body.transform.TransformPoint(bp[i]);
                foreach (var pair in pairs) maximumGap = Mathf.Max(maximumGap, Vector3.Distance(hp[pair.head], bp[pair.body]));
                var points = new Vector3[samples.Count];
                var normals = new Vector3[samples.Count];
                var weights = head.sharedMesh.boneWeights;
                var bind = head.sharedMesh.bindposes;
                var matrices = new Matrix4x4[head.bones.Length];
                for(int i=0;i<matrices.Length;i++) matrices[i] = head.transform.worldToLocalMatrix*head.bones[i].localToWorldMatrix*bind[i];
                for (int i = 0; i < samples.Count; i++)
                {
                    int index=samples[i];points[i]=hp[index];
                    // Coverage probes point laterally out of the neck, transformed
                    // by the same skinning matrices. Old shoulder shading normals
                    // point up through the collar opening and are not fit probes.
                    var radial=new Vector3(restHead[index].x,0,restHead[index].z).normalized;
                    var w=weights[index];
                    normals[i]=matrices[w.boneIndex0].MultiplyVector(radial)*w.weight0+
                        matrices[w.boneIndex1].MultiplyVector(radial)*w.weight1+
                        matrices[w.boneIndex2].MultiplyVector(radial)*w.weight2+
                        matrices[w.boneIndex3].MultiplyVector(radial)*w.weight3;
                }
                var armorPoints=armorMesh.vertices;
                for(int i=0;i<armorPoints.Length;i++) armorPoints[i]=part.Renderer.transform.TransformPoint(armorPoints[i]);
                for(int i=0;i<normals.Length;i++) normals[i]=head.transform.TransformDirection(normals[i]);
                var coverage = points.Length==0 ? new Game.Editor.Characters.SkinnedNeckValidator.Report() :
                    Game.Editor.Characters.SkinnedNeckValidator.ValidateCoverage(points, normals, armorPoints, armorMesh.triangles, .15f);
                poseCount++;
                totalCoverageFailures += coverage.FailedSamples;
                report.AppendLine(clipPath + " t=" + time.ToString("F3") + (points.Length==0 ?
                    " lower boundary closed (coverage rays not applicable)" : " coverage=" + coverage.IsValid + " failures=" + coverage.FailedSamples + " depth=" + coverage.MaxCoverageDistance.ToString("F4") + " " + string.Join(";", coverage.Errors)));
                var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                var target = neck != null ? neck.position + Vector3.up * .05f : headMesh.bounds.center - Vector3.up * .1f;
                for (int view = 0; view < 4; view++)
                {
                    camera.transform.position = target + offsets[view];
                    camera.transform.LookAt(target);
                    camera.Render();
                    var old = RenderTexture.active;
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0,0,320,320), frame * 320, (3-view)*320);
                    RenderTexture.active = old;
                }
            }
            sheet.Apply();
            string name = System.IO.Path.GetFileNameWithoutExtension(clipPath);
            System.IO.File.WriteAllBytes(folder + "/poses_" + name + ".png", sheet.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(sheet); graph.Destroy(); }
    }
    report.AppendLine("Poses=" + poseCount + "; maximum exposed join gap mm=" + maximumGap*1000 + "; hidden-boundary coverage failures=" + totalCoverageFailures);
    System.IO.File.WriteAllText(folder + "/pose-report.txt", report.ToString());
    if(maximumGap>.0001f || totalCoverageFailures>0)
        throw new System.Exception("Neck verification failed; diagnostics saved to "+folder+"/pose-report.txt");
    return report.ToString();
}
finally
{
    if(camera!=null) camera.targetTexture = null;
    UnityEngine.Object.DestroyImmediate(rt);
    UnityEngine.Object.DestroyImmediate(headMesh);
    UnityEngine.Object.DestroyImmediate(bodyMesh);
    UnityEngine.Object.DestroyImmediate(armorMesh);
    if(alternateMesh!=null)UnityEngine.Object.DestroyImmediate(alternateMesh);
    if(alternateMaterial!=null)UnityEngine.Object.DestroyImmediate(alternateMaterial);
    if(neutralMaterial!=null)UnityEngine.Object.DestroyImmediate(neutralMaterial);
    if(scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
}
