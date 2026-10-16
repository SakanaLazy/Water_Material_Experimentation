using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Project-local, file-based Editor automation. No server or external dependencies.
[InitializeOnLoad]
public static class WaterSystemAutomation
{
    const string Folder = "Assets/WaterShowcase";
    const float PreviousBubbleScale = 525f;
    const float FoamBubbleScale = 525f * 1.414213562f;
    const float CoastalFoamAmount = 2f / 3f;
    const float PatchFoamAmount = 1.5f / 3f;
    static readonly int[] WaterTextureSets = {4, 1, 3, 2};
    static readonly string Jobs = Path.GetFullPath("Temp/WaterSystem");
    static double nextPoll;
    sealed class Recording
    {
        public Command command; public int frame; public double next; public string folder;
        public Vector3 position; public Quaternion rotation; public float fov;
        public GameObject[] panels; public TextMesh[] labels;
    }
    static Recording recording;
    [Serializable] class Command { public string id; public string action; public string argument; public int width = 1600; public int height = 1000; }
    [Serializable] class Result { public string id; public bool ok; public string message; }
    static WaterSystemAutomation() { EditorApplication.update += Update; Application.logMessageReceived += OnLog; }
    static void OnLog(string message,string trace,LogType type)
    {
        if(type==LogType.Error||type==LogType.Exception)
        {
            if(EditorApplication.isPlaying)SessionState.SetInt("WaterSystemSmokeErrors",SessionState.GetInt("WaterSystemSmokeErrors",0)+1);
            Directory.CreateDirectory("Outputs/WaterSystem");
            File.AppendAllText("Outputs/WaterSystem/editor-diagnostics.log",type+": "+message+"\n"+trace+"\n");
        }
    }
    static void Update()
    {
        if(recording!=null){UpdateRecording();return;}
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + .5;
        string path = Path.Combine(Jobs, "command.json");
        if (!File.Exists(path)) return;
        Command command = null;
        try
        {
            command = JsonUtility.FromJson<Command>(File.ReadAllText(path));
            File.Delete(path);
            string message;
            switch (command.action)
            {
                case "baseline": message = Capture(Camera.main, "baseline", command.width, command.height); break;
                case "build": Build(false); Build(true); EditorSceneManager.OpenScene(Folder+"/WaterShowcase_Planes.unity"); message = "Created two masters, eight material presets, and both showcase scenes."; break;
                case "planes": EditorSceneManager.OpenScene(Folder+"/WaterShowcase_Planes.unity"); if(command.argument!="keepview")Frame(); message = "Opened plane showcase."; break;
                case "volumes": EditorSceneManager.OpenScene(Folder+"/WaterShowcase_Volumes.unity"); if(command.argument!="keepview")Frame(); message = "Opened volume showcase."; break;
                case "capture": message = Capture(Camera.main, string.IsNullOrEmpty(command.argument) ? "showcase" : command.argument, command.width, command.height); break;
                case "validate": message = Validate(); break;
                case "select": Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(command.argument); message = command.argument; break;
                case "frame": Frame(); message = "Framed showcase."; break;
                case "sceneview": EditorWindow.GetWindow<SceneView>().Focus(); message="Focused Scene view without moving it."; break;
                case "foamclose": message=FoamClose(); break;
                case "foamcapture": message=CaptureFoamView(); break;
                case "bubblemacro": message=CaptureBubbleMacro(); break;
                case "organize": message=OrganizeAssets(); break;
                case "appearance": message=ApplyAppearance(); break;
                case "materials": CreateMaterials(); message = "Updated four material presets."; break;
                case "polish": PolishScene(); message = "Updated camera, typography, and environment lighting."; break;
                case "hero": message = Hero(command.argument, command.width, command.height); break;
                case "inspect": message = Inspect(); break;
                case "breakdowns": message = Breakdowns(command.width,command.height); break;
                case "source": message = ExportShaderSource(); break;
                case "setup": InstallVolumeDepth(); message="Rebuilt the renderer with volume depth."; break;
                case "buffers": message=InspectBuffers(); break;
                case "volumecheck": message=CheckVolumeThickness(); break;
                case "panels": message=CapturePanels(command.width,command.height); break;
                case "featurechecks": message=FeatureChecks(); break;
                case "record": StartRecording(command); return;
                case "save": AssetDatabase.SaveAssets(); EditorSceneManager.SaveOpenScenes(); message="Saved all material/assets and open scenes."; break;
                case "graph": OpenGraph(command.argument); message="Opened "+command.argument; break;
                case "clearconsole": typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("Clear",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Invoke(null,null); message="Cleared historical console messages."; break;
                case "bughunt": message=BugHunt(); break;
                case "buildmessages": message=BuildMessages(); break;
                case "encode": message=EncodeMotion(); break;
                case "status": message=Status(); break;
                case "package": message=ExportPackage(); break;
                case "buildcheck": message=BuildCheck(); break;
                case "sanity": message=Sanity(); break;
                case "causticpair": message=CausticPair(); break;
                case "curvedsamples": message=CurvedSamples(); break;
                case "details": message=DetailChecks(); break;
                case "graphdrafts": message=GraphDrafts(false); break;
                case "graphreload": message=GraphDrafts(true); break;
                case "play": SessionState.SetInt("WaterSystemSmokeErrors",0); EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true; message="Starting Play mode.";break;
                case "smoke": message=Smoke();break;
                case "quit": AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();EditorApplication.delayCall+=()=>EditorApplication.Exit(0);message="Saved before Editor restart.";break;
                case "stop": EditorApplication.isPlaying=false;message="Stopped Play mode.";break;
                default: throw new ArgumentException("Unknown water automation command: " + command.action);
            }
            WriteResult(command.id, true, message);
        }
        catch (Exception error) { Debug.LogException(error); WriteResult(command == null ? "unknown" : command.id, false, error.ToString()); }
    }
    static void WriteResult(string id, bool ok, string message)
    {
        Directory.CreateDirectory(Jobs);
        File.WriteAllText(Path.Combine(Jobs, "result.json"), JsonUtility.ToJson(new Result { id = id, ok = ok, message = message }, true));
    }
    static void EnsureFolder() { Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); }
    static string Capture(Camera camera, string name, int width, int height)
    {
        if (!camera) throw new InvalidOperationException("No main camera in the active scene.");
        Directory.CreateDirectory("Outputs/WaterSystem");
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.Create();
        var previous = RenderTexture.active;
        bool asyncCompilation=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false, false);
        try
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            string output = Path.GetFullPath("Outputs/WaterSystem/" + name + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllBytes(output, texture.EncodeToPNG());
            return output;
        }
        finally { ShaderUtil.allowAsyncCompilation=asyncCompilation;RenderTexture.active = previous; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); }
    }
    [MenuItem("Water System/Build Showcase")]
    public static void BuildMenu() { Build(false); Build(true); }
    [MenuItem("Water System/Open Planes")]
    public static void OpenPlanesMenu() { EditorSceneManager.OpenScene(Folder+"/WaterShowcase_Planes.unity");Frame(); }
    [MenuItem("Water System/Open Volumes")]
    public static void OpenVolumesMenu() { EditorSceneManager.OpenScene(Folder+"/WaterShowcase_Volumes.unity");Frame(); }
    [MenuItem("Water System/Save and Check")]
    public static void CheckMenu() { Debug.Log(Sanity()); }
    public static void Build(bool volumes)
    {
        if(SceneManager.GetActiveScene().isDirty&&!SceneManager.GetActiveScene().path.StartsWith(Folder+"/"))throw new InvalidOperationException("Save the current scene before building the water showcase.");
        EnsureFolder();
        CreateMaterials();
        InstallVolumeDepth();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = volumes ? "WaterShowcase_Volumes" : "WaterShowcase_Planes";
        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunSize", .035f); sky.SetFloat("_AtmosphereThickness", .8f); sky.SetColor("_SkyTint", new Color(.42f,.57f,.72f));
        sky.SetColor("_GroundColor", new Color(.19f,.22f,.25f)); sky.SetFloat("_Exposure", 1.1f);
        SaveAsset(sky, Folder + "/CoastalSky.mat");
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/CoastalSky.mat");
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.55f,.67f,.78f);
        RenderSettings.ambientEquatorColor = new Color(.28f,.35f,.4f);
        RenderSettings.ambientGroundColor = new Color(.12f,.14f,.16f);
        RenderSettings.ambientIntensity = 1;
        RenderSettings.fog = false;
        var sun = new GameObject("Sun — warm side light").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 2.2f; sun.color = new Color(1,.91f,.78f);
        sun.transform.rotation = Quaternion.Euler(34,-32,0); sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
        var reflection=new GameObject("Sky Reflection").AddComponent<ReflectionProbe>();
        reflection.transform.position=new Vector3(0,5,0);reflection.mode=UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        reflection.refreshMode=UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;reflection.timeSlicingMode=UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        reflection.size=new Vector3(80,30,80);reflection.resolution=256;reflection.hdr=true;reflection.clearFlags=ReflectionProbeClearFlags.Skybox;
        var cam = new GameObject("Main Camera").AddComponent<Camera>();
        cam.tag = "MainCamera"; cam.transform.position = new Vector3(19,18,-26); cam.transform.LookAt(new Vector3(0,0,1));
        cam.fieldOfView = 46; cam.nearClipPlane = .15f; cam.farClipPlane = 160; cam.allowHDR = true;
        cam.transform.position = new Vector3(15,22,-27); cam.transform.LookAt(Vector3.zero);
        var data = cam.GetUniversalAdditionalCameraData(); data.requiresDepthTexture = true; data.requiresColorTexture = true; data.renderPostProcessing = true;
        var volume = new GameObject("Presentation grading").AddComponent<Volume>(); volume.isGlobal = true;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"/PresentationProfile.asset");
        if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Folder+"/PresentationProfile.asset");}
        profile.name="PresentationProfile";
        profile.components.RemoveAll(c=>!c);
        if(!profile.TryGet<Tonemapping>(out var tone)){tone=profile.Add<Tonemapping>(true);AssetDatabase.AddObjectToAsset(tone,profile);}tone.mode.Override(TonemappingMode.ACES);
        if(!profile.TryGet<Bloom>(out var bloom)){bloom=profile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,profile);}bloom.intensity.Override(.12f);bloom.threshold.Override(1.4f);
        if(!profile.TryGet<ColorAdjustments>(out var adjust)){adjust=profile.Add<ColorAdjustments>(true);AssetDatabase.AddObjectToAsset(adjust,profile);}adjust.postExposure.Override(.15f);adjust.contrast.Override(6);
        volume.sharedProfile=profile;EditorUtility.SetDirty(profile);
        Material stone = Solid("Basalt", new Color(.12f,.15f,.17f), .22f);
        Material sand = Solid("Sand", new Color(.65f,.61f,.52f), .28f);
        AddGrain(stone,false);AddGrain(sand,true);
        Material plinth = Solid("Studio charcoal", new Color(.025f,.034f,.047f), .3f);
        Material type = Solid("Type ivory", new Color(.78f,.84f,.87f), .3f);
        Material marker = Solid("Submerged ivory", new Color(.83f,.78f,.64f), .2f);
        Mesh grid = Grid(160, 12);
        SaveAsset(grid, Folder+"/WaterGrid.asset"); grid = AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/WaterGrid.asset");
        string[] names = { "01  /  REALISTIC", "02  /  LIGHTLY STYLIZED", "03  /  STRONGLY STYLIZED", "04  /  GRAPHIC" };
        string[] files = { "M_Water_Realistic", "M_Water_LightlyStylized", "M_Water_StronglyStylized", "M_Water_Graphic" };
        Mesh seabed=Seabed();SaveAsset(seabed,Folder+"/SlopingSeabed.asset");seabed=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/SlopingSeabed.asset");
        for (int i=0; i<4; i++)
        {
            Vector3 center = new Vector3((i%2 == 0 ? -1 : 1)*6.8f, 0, (i<2 ? 1 : -1)*6.8f);
            var parent = new GameObject(names[i]).transform; parent.position = center;
            var water = new GameObject("Water — "+(volumes?"closed volume":"plane surface")); water.transform.SetParent(parent, false);
            water.AddComponent<MeshFilter>().sharedMesh = volumes ? VolumeBox(90,12,2.2f) : grid;
            if(volumes) water.layer=30;
            water.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+files[i]+(volumes?"_Volume":"_Plane")+".mat");
            var bed = new GameObject("Sloping seabed");bed.transform.SetParent(parent,false);bed.AddComponent<MeshFilter>().sharedMesh=seabed;bed.AddComponent<MeshRenderer>().sharedMaterial=sand;
            Primitive(PrimitiveType.Cube,"Floating display plinth",parent,new Vector3(0,-2.8f,0),new Vector3(12.3f,.6f,12.3f),plinth);
            for(int k=0;k<5;k++)
            {
                float x=-4.9f+k*.43f, z=2.4f+Mathf.Sin(k*2.2f)*1.15f;
                var rock=Primitive(PrimitiveType.Sphere,"Basalt shoreline "+k,parent,new Vector3(x,-.15f,z),new Vector3(1.7f,1.7f+(k%3)*.5f,1.8f),stone);
                rock.transform.localRotation=Quaternion.Euler(k*23,k*39,k*17);
                var rockMesh=Rock(rock.GetComponent<MeshFilter>().sharedMesh,k);SaveAsset(rockMesh,Folder+"/BasaltRock"+k+".asset");rock.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/BasaltRock"+k+".asset");
            }
            Text(names[i],parent,new Vector3(-5.8f,.05f,-6.45f),.06f,type);
            // These opaque stones give transmission a readable reference.
            for(int k=0;k<3;k++)Primitive(PrimitiveType.Cube,"Submerged clarity marker "+k,parent,new Vector3(-.8f+k*1.1f,-.85f-k*.15f,-2.7f),new Vector3(.5f,.22f,.7f),marker);
            if(volumes)
            {
                var drop=Primitive(PrimitiveType.Sphere,"Closed curved volume",parent,new Vector3(3,1.5f,2),new Vector3(2.5f,2.5f,2.5f),water.GetComponent<Renderer>().sharedMaterial);
                drop.layer=30;
                var block=new MaterialPropertyBlock();block.SetFloat("_Volume_Normal_Tiling",1.2f);block.SetFloat("_Swell_Length",1.2f);block.SetFloat("_Swell_Height",.012f);
                block.SetFloat("_Enable_Shoreline_Foam",0);block.SetFloat("_Enable_Coastal_Foam",0);block.SetFloat("_Enable_Crest_Foam",0);block.SetFloat("_Surface_Foam_Amount",.25f);
                drop.GetComponent<Renderer>().SetPropertyBlock(block);drop.AddComponent<WaterCurvedSample>();
                Primitive(PrimitiveType.Cube,"Curved volume clarity marker",parent,new Vector3(3,1.5f,2),Vector3.one*.45f,marker);
            }
        }
        Text("W A T E R   /   F O U R   I D E N T I T I E S",null,new Vector3(-12,.05f,14.3f),.1f,type);
        Text("ONE MODULAR SHADER     •     URP / SHADER GRAPH",null,new Vector3(-12,.05f,13.3f),.045f,type);
        DynamicGI.UpdateEnvironment();
        reflection.RenderProbe();
        cam.gameObject.AddComponent<WaterShowcasePresenter>();
        EditorSceneManager.SaveScene(scene,Folder+"/"+scene.name+".unity");
        AssetDatabase.SaveAssets();
        Frame();
    }
    static void Text(string value, Transform parent, Vector3 position, float size, Material material)
    {
        var go = new GameObject(value); go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localRotation=Quaternion.Euler(90,0,0);
        var text=go.AddComponent<TextMesh>(); text.text=value; text.fontSize=64; text.characterSize=size; text.color=new Color(.72f,.81f,.84f); text.anchor=TextAnchor.MiddleLeft;
    }
    static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    static Material Solid(string name, Color color,float smoothness)
    {
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=name;mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",smoothness);
        string path=Folder+"/"+name+".mat";SaveAsset(mat,path);return AssetDatabase.LoadAssetAtPath<Material>(path);
    }
    static void SaveAsset(UnityEngine.Object asset,string path)
    {
        var existing=AssetDatabase.LoadMainAssetAtPath(path);
        if(existing) { EditorUtility.CopySerialized(asset,existing);UnityEngine.Object.DestroyImmediate(asset);EditorUtility.SetDirty(existing); }
        else AssetDatabase.CreateAsset(asset,path);
    }
    static Mesh Grid(int segments,float size)
    {
        int n=segments+1;var vertices=new Vector3[n*n];var uv=new Vector2[n*n];var normals=new Vector3[n*n];var tangents=new Vector4[n*n];var triangles=new int[segments*segments*6];
        for(int z=0;z<n;z++)for(int x=0;x<n;x++){int j=z*n+x;vertices[j]=new Vector3(((float)x/segments-.5f)*size,0,((float)z/segments-.5f)*size);uv[j]=new Vector2((float)x/segments,(float)z/segments);normals[j]=Vector3.up;tangents[j]=new Vector4(1,0,0,-1);}
        int t=0;for(int z=0;z<segments;z++)for(int x=0;x<segments;x++){int a=z*n+x;triangles[t++]=a;triangles[t++]=a+n;triangles[t++]=a+1;triangles[t++]=a+1;triangles[t++]=a+n;triangles[t++]=a+n+1;}
        var mesh=new Mesh{name="Water grid — 160 subdivisions"};mesh.vertices=vertices;mesh.uv=uv;mesh.normals=normals;mesh.tangents=tangents;mesh.triangles=triangles;mesh.RecalculateBounds();var b=mesh.bounds;b.Expand(2);mesh.bounds=b;return mesh;
    }
    static void CreateMaterials()
    {
        EnsureFolder();
        const string causticPath="Assets/Materials/Water/Textures/CausticsAtlas.png";
        var causticImporter=AssetImporter.GetAtPath(causticPath) as TextureImporter;
        if(causticImporter&&(causticImporter.sRGBTexture||causticImporter.mipmapEnabled||causticImporter.npotScale!=TextureImporterNPOTScale.None||causticImporter.textureCompression!=TextureImporterCompression.Uncompressed))
        {
            causticImporter.sRGBTexture=false;causticImporter.mipmapEnabled=false;causticImporter.npotScale=TextureImporterNPOTScale.None;causticImporter.maxTextureSize=4096;
            causticImporter.textureCompression=TextureImporterCompression.Uncompressed;causticImporter.filterMode=FilterMode.Bilinear;causticImporter.wrapMode=TextureWrapMode.Clamp;causticImporter.SaveAndReimport();
        }
        string[] names={"M_Water_Realistic","M_Water_LightlyStylized","M_Water_StronglyStylized","M_Water_Graphic"};
        Color[] shallow={new Color(.05f,.52f,.48f),new Color(.12f,.62f,.63f),new Color(.045f,.63f,.63f),new Color(.12f,.68f,.73f)};
        Color[] deep={new Color(.012f,.075f,.11f),new Color(.025f,.17f,.28f),new Color(.025f,.12f,.32f),new Color(.025f,.055f,.19f)};
        for(int geometry=0;geometry<2;geometry++)for(int i=0;i<4;i++)
        {
            bool volume=geometry==1;
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(ShaderAsset("SG_Water_"+(volume?"Volume":"Plane")+".shadergraph"));
            if(!shader)throw new InvalidOperationException("Water master graph has not imported yet.");
            string path=Folder+"/"+names[i]+(volume?"_Volume":"_Plane")+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
            mat.SetColor("_Water_Color",shallow[i]);mat.SetColor("_Deep_Color",deep[i]);mat.SetColor("_Foam_Color",new Color(.78f,.85f,.82f));
            mat.SetColor("_Water_Transmission",new Color(.45f,.85f,.95f));
            ApplyMaps(mat,i);
            Set(mat,"_Style",i/3f);Set(mat,"_Wave_1_Scale",1.4f);Set(mat,"_Wave_2_Scale",1.8f);Set(mat,"_Wave_1_Speed",.025f);Set(mat,"_Wave_2_Speed",.018f);
            mat.SetVector("_Wave_1_Direction",new Vector4(1,.35f,0,0));mat.SetVector("_Wave_2_Direction",new Vector4(-.35f,1,0,0));
            Set(mat,"_Wave_1_Height",.05f);Set(mat,"_Wave_2_Height",.08f);
            Set(mat,"_Crest_Foam_Start",.00065f);Set(mat,"_Crest_Foam_Full",.0008f);Set(mat,"_Crest_Foam_Noise_Scale",25);Set(mat,"_Crest_Foam_Noise_Speed",.03f);
            mat.SetVector("_Crest_Foam_Noise_Direction",new Vector4(1,.3f,0,0));Set(mat,"_Crest_Noise_Start",.32f);Set(mat,"_Crest_Noise_Full",.55f);
            Set(mat,"_Foam_Intensity",1);Set(mat,"_Crest_Strength",(i==0?.45f:.85f)*PatchFoamAmount);Set(mat,"_Foam_Smoothness",i<=1?.3f:.18f);Set(mat,"_Foam_Opacity",.98f);Set(mat,"_Water_Opacity",i==0?.72f:1);
            Set(mat,"_Water_Specular",i==0?.025f:.06f);
            mat.SetVector("_Water_Emission_Intensity",Vector4.zero);
            Set(mat,"_Detail_Scale",30);Set(mat,"_Detail_Strength",i==0?.85f:.35f);Set(mat,"_Depth_Density",i==0?.42f:.8f);Set(mat,"_Contact_Width",i==0?.14f:.22f);
            Set(mat,"_Normal_Strength",i==0?.045f:.10f);
            Set(mat,"_Foam_Bump_Strength",i==0?.10f:i==1?.075f:0);Set(mat,"_Foam_Bubble_Scale",FoamBubbleScale);
            Set(mat,"_Surface_Foam_Amount",i==0?.40f:.48f);Set(mat,"_Coastal_Width",1.0f);Set(mat,"_Refraction_Strength",i==0?.006f:.004f);
            Set(mat,"_Surface_Foam_Scale",.55f);Set(mat,"_Surface_Foam_Speed",.08f);Set(mat,"_Surface_Foam_Stretch",2.2f);Set(mat,"_Surface_Foam_Intensity",(i==0?.55f:1)*PatchFoamAmount);
            mat.SetVector("_Surface_Foam_Direction",new Vector4(1,.3f,0,0));
            Set(mat,"_Contact_Scale",2.2f);Set(mat,"_Contact_Speed",.9f);Set(mat,"_Contact_Intensity",.85f);
            Set(mat,"_Coastal_Speed",1.1f);Set(mat,"_Coastal_Frequency",9);Set(mat,"_Coastal_Scale",.6f);Set(mat,"_Coastal_Intensity",(i==0?.48f:.8f)*CoastalFoamAmount);
            Set(mat,"_Swell_Height",i==0?.12f:.18f);Set(mat,"_Swell_Length",4.2f);Set(mat,"_Swell_Speed",.85f);
            if(volume){Set(mat,"_Swell_Height",.055f);Set(mat,"_Swell_Length",3.5f);Set(mat,"_Volume_Fallback_Thickness",.5f);}
            foreach(string feature in new[]{"Crest_Foam","Surface_Foam","Shoreline_Foam","Coastal_Foam"})Set(mat,"_Enable_"+feature,1);
            Set(mat,"_Enable_Depth",1);Set(mat,"_Enable_Refraction",i<=1?1:0);
            mat.SetTexture("_Caustics_Atlas",AssetDatabase.LoadAssetAtPath<Texture2D>(causticPath));
            Set(mat,"_Enable_Caustics",i<=1?1:0);Set(mat,"_Caustics_Scale",.25f);Set(mat,"_Caustics_Speed",.7f);Set(mat,"_Caustics_Strength",i==0?.28f:i==1?.10f:0);Set(mat,"_Caustics_Fade_Depth",3);
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
    }
    static void ApplyMaps(Material material,int style)
    {
        int map=WaterTextureSets[style];string folder="Assets/Textures/Water_"+map+"/";
        var water=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Water_"+map+".png");
        var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Water_Normal_"+map+".png");
        if(!water||!normal)throw new Exception("Missing water map pair "+map);
        material.SetTexture("_Water_Map",water);material.SetTexture("_Water_Normal",normal);
    }
    static string ApplyAppearance()
    {
        int count=0;
        foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{Folder}))
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if(!material.HasProperty("_Style"))continue;
            int style=Mathf.RoundToInt(material.GetFloat("_Style")*3);
            ApplyMaps(material,style);
            material.SetFloat("_Foam_Bubble_Scale",FoamBubbleScale);
            material.SetFloat("_Foam_Bump_Strength",style==0?.10f:style==1?.075f:0);
            material.SetFloat("_Foam_Intensity",1);
            material.SetFloat("_Surface_Foam_Intensity",(style==0?.55f:1)*PatchFoamAmount);
            material.SetFloat("_Crest_Strength",(style==0?.45f:.85f)*PatchFoamAmount);
            material.SetFloat("_Coastal_Intensity",(style==0?.48f:.8f)*CoastalFoamAmount);
            EditorUtility.SetDirty(material);count++;
        }
        AssetDatabase.SaveAssets();return "Updated "+count+" presets: coastal bands 2/3; crest and drifting patches 1/2; shoreline contact unchanged; finer bubbles with smooth joins and softer bump.";
    }
    static void Set(Material mat,string name,float value){if(mat.HasProperty(name))mat.SetFloat(name,value);}
    static string Validate()
    {
        AssetDatabase.Refresh();
        string output="";bool errors=false;
        foreach(string name in new[]{"Plane","Volume"})
        {
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(ShaderAsset("SG_Water_"+name+".shadergraph"));
            if(!shader)throw new Exception("Missing "+name+" shader.");
            var messages=ShaderUtil.GetShaderMessages(shader);
            output+=name+" supported: "+shader.isSupported+"\n"+string.Join("\n",messages.Select(m=>m.severity+": "+m.message+" ("+m.file+":"+m.line+")"))+"\n";
            errors|=messages.Any(m=>m.severity.ToString()=="Error");
        }
        var back=Shader.Find("Hidden/Water/VolumeBackDepth");
        if(!back)throw new Exception("Missing volume back-depth shader.");
        var backMessages=ShaderUtil.GetShaderMessages(back);output+="Volume back depth supported: "+back.isSupported+"\n"+string.Join("\n",backMessages.Select(m=>m.severity+": "+m.message))+"\n";
        errors|=backMessages.Any(m=>m.severity.ToString()=="Error");
        Directory.CreateDirectory("Outputs/WaterSystem");File.WriteAllText("Outputs/WaterSystem/validation.txt",output);
        if(errors)throw new Exception(output);
        return output;
    }
    static void Frame(){if(SceneView.lastActiveSceneView){SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,1),Quaternion.Euler(35,-25,0),25);SceneView.lastActiveSceneView.Repaint();}}
    static string FoamClose()
    {
        var scene=SceneView.lastActiveSceneView?SceneView.lastActiveSceneView:EditorWindow.GetWindow<SceneView>();
        scene.Focus();scene.maximized=true;scene.in2DMode=false;scene.showGrid=false;scene.drawGizmos=false;
        scene.LookAt(new Vector3(-11,.05f,3.8f),Quaternion.Euler(55,-20,0),1.25f);scene.Repaint();
        return "Realistic water framed close to the shoreline bubbles; Scene view maximized, grid and gizmos hidden.";
    }
    static string CaptureFoamView()
    {
        var scene=SceneView.lastActiveSceneView;if(!scene)throw new Exception("No Scene view for the close-up.");
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        try{camera.transform.SetPositionAndRotation(scene.camera.transform.position,scene.camera.transform.rotation);camera.fieldOfView=scene.camera.fieldOfView;return Capture(camera,"realistic-bubbles-closeup",2560,1700);}
        finally{camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
    }
    static string CaptureBubbleMacro()
    {
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        try
        {
            Vector3 focus=new Vector3(-11,.04f,3.8f);
            camera.transform.position=focus+new Vector3(.65f,1.1f,-.8f);camera.transform.LookAt(focus);camera.fieldOfView=46;
            return Capture(camera,"realistic-bubbles-closeup",2560,1700);
        }
        finally{camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
    }
    static void PolishScene()
    {
        var camera=Camera.main;camera.transform.position=new Vector3(15,22,-27);camera.transform.LookAt(Vector3.zero);camera.fieldOfView=46;
        foreach(var text in UnityEngine.Object.FindObjectsByType<TextMesh>())text.characterSize=text.text.StartsWith("W A T E R")?.1f:text.text.StartsWith("ONE MODULAR")?.045f:.06f;
        DynamicGI.UpdateEnvironment();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
    static string Hero(string name,int width,int height)
    {
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        try
        {
            Vector3 focus=new Vector3(-6.8f,-.35f,6.8f);
            camera.transform.position=focus+new Vector3(8,6,-9);camera.transform.LookAt(focus);camera.fieldOfView=49;
            return Capture(camera,string.IsNullOrEmpty(name)?"hero":name,width,height);
        }
        finally{camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
    }
    static string Inspect()
    {
        string result="Scene: "+SceneManager.GetActiveScene().path+"\nPipeline: "+GraphicsSettings.currentRenderPipeline+"\nSky: "+RenderSettings.skybox+"\nSun: "+RenderSettings.sun+"\n";
        foreach(var material in AssetDatabase.FindAssets("t:Material",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m=>m.HasProperty("_Style")))result+=material.name+" | Style="+material.GetFloat("_Style")+" | Color="+material.GetColor("_Water_Color")+" | Deep="+material.GetColor("_Deep_Color")+"\n";
        File.WriteAllText("Outputs/WaterSystem/inspection.txt",result);return result;
    }
    static string Breakdowns(int width,int height)
    {
        bool volume=SceneManager.GetActiveScene().name.Contains("Volumes");
        var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/M_Water_Realistic_"+(volume?"Volume":"Plane")+".mat");
        var panels=SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g.name.Length>2&&char.IsDigit(g.name[0])).OrderBy(g=>g.name).ToArray();
        var labels=UnityEngine.Object.FindObjectsByType<TextMesh>();foreach(var label in labels)label.gameObject.SetActive(false);for(int i=0;i<panels.Length;i++)panels[i].SetActive(i==0);
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        float original=material.GetFloat("_Debug_View");
        try
        {
            Vector3 focus=panels[0].transform.position+Vector3.down*.8f;camera.transform.position=focus+new Vector3(10,9,-14);camera.transform.LookAt(focus);camera.fieldOfView=44;
            string[] names={"beauty","crest","surface","contact","coastal","thickness","normals","body-color","transmission","caustics"};
            for(int i=0;i<names.Length;i++){material.SetFloat("_Debug_View",i);Capture(camera,(volume?"volume-":"plane-")+names[i],width,height);}
            return "Captured ten isolated effect views.";
        }
        finally{material.SetFloat("_Debug_View",original);foreach(var panel in panels)panel.SetActive(true);foreach(var label in labels)label.gameObject.SetActive(true);camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
    }
    static string ExportShaderSource()
    {
        var type=System.Reflection.Assembly.Load("Unity.ShaderGraph.Editor").GetType("UnityEditor.ShaderGraph.ShaderGraphImporter",true);
        var method=type.GetMethods(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).First(m=>m.Name=="GetShaderText"&&m.GetParameters().Length==4&&m.GetParameters()[3].IsOut);
        foreach(string name in new[]{"Plane","Volume"})
        {
            object[] args={ShaderAsset("SG_Water_"+name+".shadergraph"),null,null,null};
            File.WriteAllText("Outputs/WaterSystem/"+name+".generated.shader",(string)method.Invoke(null,args));
        }
        return "Exported generated shader sources for inspection.";
    }
    static string InspectBuffers()
    {
        Capture(Camera.main,"buffer-check",800,600);
        string diagnostic="Enqueued="+WaterVolumeBackDepthFeature.Enqueued+" Recorded="+WaterVolumeBackDepthFeature.Recorded+" Shader="+Shader.Find("Hidden/Water/VolumeBackDepth")+" Renderer="+Camera.main.GetUniversalAdditionalCameraData().scriptableRenderer+". ";
        var global=WaterVolumeBackDepthFeature.LastDepth;
        if(!global)return diagnostic+"No global volume back-depth texture bound.";
        var tex=new Texture2D(global.width,global.height,TextureFormat.RGBAFloat,false,true);
        var active=RenderTexture.active;
        try
        {
            RenderTexture.active=global;tex.ReadPixels(new Rect(0,0,global.width,global.height),0,0);tex.Apply();
            var pixels=tex.GetPixels();float min=pixels.Min(p=>p.r),max=pixels.Max(p=>p.r);int nonzero=pixels.Count(p=>p.r>.01f);
            var display=new Texture2D(global.width,global.height,TextureFormat.RGB24,false);display.SetPixels(pixels.Select(p=>new Color(p.r/80,p.r/80,p.r/80)).ToArray());display.Apply();File.WriteAllBytes("Outputs/WaterSystem/back-depth.png",display.EncodeToPNG());UnityEngine.Object.DestroyImmediate(display);
            return diagnostic+"Volume back depth: "+global.width+"x"+global.height+" min="+min+" max="+max+" nonzero="+nonzero;
        }
        finally{RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(tex);}
    }
    static string CheckVolumeThickness()
    {
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/M_Water_Realistic_Volume.mat");
        float fallback=mat.GetFloat("_Volume_Fallback_Thickness");float view=mat.GetFloat("_Debug_View");
        try{mat.SetFloat("_Volume_Fallback_Thickness",0);mat.SetFloat("_Debug_View",5);return Hero("volume-thickness-no-fallback",1200,800);}
        finally{mat.SetFloat("_Volume_Fallback_Thickness",fallback);mat.SetFloat("_Debug_View",view);}
    }
    static string CapturePanels(int width,int height)
    {
        var roots=SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g.name.Length>2&&char.IsDigit(g.name[0])).OrderBy(g=>g.name).ToArray();
        var labels=UnityEngine.Object.FindObjectsByType<TextMesh>();foreach(var label in labels)label.gameObject.SetActive(false);
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        try
        {
            bool volumes=SceneManager.GetActiveScene().name.Contains("Volumes");
            for(int i=0;i<roots.Length;i++)
            {
                for(int j=0;j<roots.Length;j++)roots[j].SetActive(i==j);
                Vector3 focus=roots[i].transform.position+new Vector3(0,-.8f,0);
                camera.transform.position=focus+new Vector3(10,9,-14);camera.transform.LookAt(focus);camera.fieldOfView=44;
                Capture(camera,(volumes?"volume":"plane")+"-style-"+i,width,height);
            }
            return "Captured four isolated "+(volumes?"volume":"plane")+" styles.";
        }
        finally{foreach(var root in roots)root.SetActive(true);foreach(var label in labels)label.gameObject.SetActive(true);camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
    }
    static string FeatureChecks()
    {
        bool volume=SceneManager.GetActiveScene().name.Contains("Volumes");string geometry=volume?"Volume":"Plane";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/M_Water_Realistic_"+geometry+".mat");
        float view=mat.GetFloat("_Debug_View");Shader.SetGlobalFloat("_WaterCaptureFreeze",1);Shader.SetGlobalFloat("_WaterCaptureTime",34);string[] features={"Crest_Foam","Surface_Foam","Shoreline_Foam","Coastal_Foam","Depth","Refraction","Caustics"};
        string result=geometry+" feature image checks\n";
        try
        {
            for(int i=0;i<features.Length;i++)
            {
                string property="_Enable_"+features[i];float original=mat.GetFloat(property);
                try
                {
                    mat.SetFloat("_Debug_View",i<4?i+1:i==4?7:i==6?9:0);
                    mat.SetFloat(property,1);string on=Hero(geometry+"-check-"+features[i]+"-on",640,440);
                    mat.SetFloat(property,0);string off=Hero(geometry+"-check-"+features[i]+"-off",640,440);
                    var a=new Texture2D(2,2);a.LoadImage(File.ReadAllBytes(on));var b=new Texture2D(2,2);b.LoadImage(File.ReadAllBytes(off));
                    var ap=a.GetPixels32();var bp=b.GetPixels32();int changed=0;
                    for(int j=0;j<ap.Length;j++)if(Mathf.Abs(ap[j].r-bp[j].r)+Mathf.Abs(ap[j].g-bp[j].g)+Mathf.Abs(ap[j].b-bp[j].b)>18)changed++;
                    UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);
                    result+=features[i]+": "+changed+" changed pixels / "+ap.Length+"\n";
                    if(changed<30)throw new Exception(geometry+" "+features[i]+" did not visibly change its output.\n"+result);
                }
                finally{mat.SetFloat(property,original);}
            }
            File.WriteAllText("Outputs/WaterSystem/"+geometry+"-feature-checks.txt",result);return result;
        }
        finally{mat.SetFloat("_Debug_View",view);Shader.SetGlobalFloat("_WaterCaptureFreeze",0);}
    }
    static void StartRecording(Command command)
    {
        bool volumes=SceneManager.GetActiveScene().name.Contains("Volumes");
        var camera=Camera.main;
        var panels=SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g.name.Length>2&&char.IsDigit(g.name[0])).OrderBy(g=>g.name).ToArray();
        recording=new Recording{command=command,folder="Animation"+(volumes?"Volume":"Plane"),position=camera.transform.position,rotation=camera.transform.rotation,fov=camera.fieldOfView,panels=panels,labels=UnityEngine.Object.FindObjectsByType<TextMesh>()};
        foreach(var label in recording.labels)label.gameObject.SetActive(false);for(int i=0;i<panels.Length;i++)panels[i].SetActive(i==0);
        Vector3 focus=panels[0].transform.position+Vector3.down*.8f;camera.transform.position=focus+new Vector3(10,9,-14);camera.transform.LookAt(focus);camera.fieldOfView=44;
    }
    static void UpdateRecording()
    {
        if(EditorApplication.timeSinceStartup<recording.next)return;
        var state=recording;
        try
        {
            Capture(Camera.main,state.folder+"/frame_"+state.frame.ToString("D3"),state.command.width,state.command.height);state.frame++;state.next=EditorApplication.timeSinceStartup+1.0/12.0;
            if(state.frame<36)return;
            EndRecording();WriteResult(state.command.id,true,"Captured 36 animation frames in Outputs/WaterSystem/"+state.folder+".");
        }
        catch(Exception error){EndRecording();WriteResult(state.command.id,false,error.ToString());}
    }
    static void EndRecording()
    {
        var state=recording;recording=null;foreach(var panel in state.panels)panel.SetActive(true);foreach(var label in state.labels)label.gameObject.SetActive(true);
        Camera.main.transform.SetPositionAndRotation(state.position,state.rotation);Camera.main.fieldOfView=state.fov;
    }
    static string Smoke()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play mode has not started.");
        var presenter=UnityEngine.Object.FindAnyObjectByType<WaterShowcasePresenter>();if(!presenter)throw new Exception("Missing showcase presenter.");
        for(int i=0;i<4;i++){presenter.Focus(i);Capture(Camera.main,"play-style-"+i,640,440);}presenter.Focus(-1);
        int errors=SessionState.GetInt("WaterSystemSmokeErrors",0);string result="Play mode active; four camera selections rendered; runtime errors: "+errors;
        File.WriteAllText("Outputs/WaterSystem/"+SceneManager.GetActiveScene().name+"-play-test.txt",result);if(errors>0)throw new Exception(result);return result;
    }
    static void AddGrain(Material material,bool sand)
    {
        const int size=256;var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float a=Mathf.PerlinNoise(x*.06f,y*.06f),b=Mathf.PerlinNoise(x*.33f+17,y*.33f+23);
            float v=sand?.78f+a*.16f+b*.06f:.48f+a*.32f+b*.2f;
            pixels[y*size+x]=new Color(v,v,v,1);
        }
        string path=Folder+"/"+(sand?"SandGrain":"RockGrain")+".png";
        var texture=new Texture2D(size,size,TextureFormat.RGB24,true);texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));material.SetTextureScale("_BaseMap",Vector2.one*(sand?4:2));EditorUtility.SetDirty(material);
    }
    static Mesh Rock(Mesh source,int seed)
    {
        var mesh=UnityEngine.Object.Instantiate(source);mesh.name="BasaltRock"+seed;var vertices=mesh.vertices;
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 p=vertices[i];float noise=Mathf.PerlinNoise(p.x*3+seed*7+31,p.y*4+p.z*2+19);
            p*=.78f+noise*.6f;p.y=Mathf.Max(p.y,-.36f);vertices[i]=p;
        }
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }
    static Mesh Seabed()
    {
        const int segments=48;int n=segments+1;var vertices=new Vector3[n*n];var uv=new Vector2[n*n];var indices=new int[segments*segments*6];
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            float u=(float)x/segments,v=(float)z/segments;int j=z*n+x;
            float height=Mathf.Lerp(.14f,-2.18f,Mathf.SmoothStep(0,1,u))+.06f*Mathf.PerlinNoise(u*7+11,v*7+29);
            // Inset the opaque cutaway from the water walls. Coplanar side
            // skirts otherwise intersect the displaced volume in long stripes.
            vertices[j]=new Vector3((u-.5f)*11.88f,height,(v-.5f)*11.88f);uv[j]=new Vector2(u,v);
        }
        int t=0;for(int z=0;z<segments;z++)for(int x=0;x<segments;x++){int a=z*n+x;indices[t++]=a;indices[t++]=a+n;indices[t++]=a+1;indices[t++]=a+1;indices[t++]=a+n;indices[t++]=a+n+1;}
        var allVertices=vertices.ToList();var allUV=uv.ToList();var allIndices=indices.ToList();
        int[][] boundaries={Enumerable.Range(0,n).Select(x=>x).ToArray(),Enumerable.Range(0,n).Select(z=>z*n+segments).ToArray(),Enumerable.Range(0,n).Select(x=>segments*n+segments-x).ToArray(),Enumerable.Range(0,n).Select(z=>(segments-z)*n).ToArray()};
        foreach(var edge in boundaries)
        {
            int start=allVertices.Count;for(int j=0;j<n;j++){Vector3 p=vertices[edge[j]];allVertices.Add(p);allVertices.Add(new Vector3(p.x,-2.5f,p.z));allUV.Add(new Vector2((float)j/segments,1));allUV.Add(new Vector2((float)j/segments,0));}
            for(int j=0;j<segments;j++){int a=start+j*2;allIndices.AddRange(new[]{a,a+2,a+1,a+2,a+3,a+1});}
        }
        var mesh=new Mesh{name="SlopingSeabed"};mesh.SetVertices(allVertices);mesh.SetUVs(0,allUV);mesh.SetTriangles(allIndices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }
    static void InstallVolumeDepth()
    {
        var tagAsset=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
        if(tagAsset){var serialized=new SerializedObject(tagAsset);var layer=serialized.FindProperty("layers").GetArrayElementAtIndex(30);if(string.IsNullOrEmpty(layer.stringValue)){layer.stringValue="Water Volume";serialized.ApplyModifiedPropertiesWithoutUndo();}}
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        if(renderer && !renderer.rendererFeatures.Any(f=>f is WaterVolumeBackDepthFeature))
        {
            var feature=ScriptableObject.CreateInstance<WaterVolumeBackDepthFeature>();feature.name="Water volume back-face depth";feature.Create();
            AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssets();
        }
        if(renderer)
        {
            renderer.SetDirty();
            typeof(ScriptableRendererData).GetMethod("ValidateRendererFeatures",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(renderer,null);
            typeof(ScriptableRendererData).GetMethod("UpdateMap",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(renderer,null);
            EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssets();
        }
    }
    static Mesh VolumeBox(int segments,float size,float height)
    {
        var vertices=new System.Collections.Generic.List<Vector3>();var normals=new System.Collections.Generic.List<Vector3>();var uv=new System.Collections.Generic.List<Vector2>();var tangents=new System.Collections.Generic.List<Vector4>();var indices=new System.Collections.Generic.List<int>();
        Vector3[] n={Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};
        Vector3[] u={Vector3.right,Vector3.right,Vector3.forward,Vector3.back,Vector3.right,Vector3.left};
        for(int face=0;face<6;face++)
        {
            Vector3 v=Vector3.Cross(n[face],u[face]);
            int start=vertices.Count, steps=segments;
            for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
            {
                Vector3 p=n[face]*.5f+u[face]*((float)x/steps-.5f)+v*((float)y/steps-.5f);
                p=new Vector3(p.x*size,(p.y-.5f)*height,p.z*size);vertices.Add(p);normals.Add(n[face]);uv.Add(new Vector2((float)x/steps,(float)y/steps));tangents.Add(new Vector4(u[face].x,u[face].y,u[face].z,1));
            }
            for(int y=0;y<steps;y++)for(int x=0;x<steps;x++){int a=start+y*(steps+1)+x;indices.AddRange(new[]{a,a+1,a+steps+1,a+1,a+steps+2,a+steps+1});}
        }
        var mesh=new Mesh{name="WaterVolumeBox"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTangents(tangents);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        SaveAsset(mesh,Folder+"/WaterVolumeBox.asset");return AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/WaterVolumeBox.asset");
    }
    static string BugHunt()
    {
        AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
        string report="Water shader bug hunt\n";int passes=0;
        var shaderPaths=AssetDatabase.FindAssets("t:Shader",new[]{"Assets/Materials/Water/Shader"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();
        foreach(string path in shaderPaths)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);if(!shader||!shader.isSupported)throw new Exception("Unsupported shader: "+path);
            var material=new Material(shader);
            try{for(int pass=0;pass<material.passCount;pass++){ShaderUtil.CompilePass(material,pass,true);passes++;}}
            finally{UnityEngine.Object.DestroyImmediate(material);}
            var messages=ShaderUtil.GetShaderMessages(shader);
            report+=Path.GetFileName(path)+": "+messages.Length+" compiler messages\n";
            if(messages.Any(m=>m.severity.ToString()=="Error"||m.severity.ToString()=="Warning"))throw new Exception(path+"\n"+string.Join("\n",messages.Select(m=>m.severity+": "+m.message)));
        }
        int subgraphs=0;
        foreach(string path in AssetDatabase.FindAssets("t:SubGraphAsset",new[]{"Assets/Materials/Water/Shader/Subgraphs"}).Select(AssetDatabase.GUIDToAssetPath))
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            var asset=AssetDatabase.LoadMainAssetAtPath(path);
            if(!(bool)asset.GetType().GetField("isValid").GetValue(asset))throw new Exception("Invalid subgraph: "+path);
            subgraphs++;
        }
        if(subgraphs!=34)throw new Exception("Expected 34 valid subgraphs; found "+subgraphs);
        report+="Compiled "+passes+" shader passes across "+shaderPaths.Length+" shaders; "+subgraphs+" valid subgraphs.\n";
        report+=Sanity();File.WriteAllText("Outputs/WaterSystem/bug-hunt.txt",report);return report;
    }
    static string Status()
    {
        var method=typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public);
        object[] counts={0,0,0};method.Invoke(null,counts);
        string report="Playing="+EditorApplication.isPlaying+"; compiling="+EditorApplication.isCompiling+"; shaders compiling="+ShaderUtil.anythingCompiling+"; scene dirty="+SceneManager.GetActiveScene().isDirty+"; capture freeze="+Shader.GetGlobalFloat("_WaterCaptureFreeze")+"\n";
        report+=string.Join("; ",method.GetParameters().Select((p,i)=>p.Name+"="+counts[i]));
        return report;
    }
    static string ExportPackage()
    {
        AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
        string folder="Outputs/WaterSystem/Release";Directory.CreateDirectory(folder);
        string output=folder+"/WaterMaterial_Review.unitypackage";
        string[] paths={"Assets/Materials/Water/Shader/Masters","Assets/Materials/Water/Shader/Subgraphs","Assets/Materials/Water/Shader/Includes","Assets/Materials/Water/Shader/Hidden","Assets/Water/Runtime","Assets/WaterShowcase","Assets/Editor/WaterSystemShaderGUI.cs","Assets/Editor/WaterVolumeSetup.cs","Assets/Textures/Water_1"};
        var expanded=paths.SelectMany(p=>AssetDatabase.IsValidFolder(p)?AssetDatabase.FindAssets("",new[]{p}).Select(AssetDatabase.GUIDToAssetPath).Append(p):new[]{p}).Distinct().ToArray();
        var dependencies=AssetDatabase.GetDependencies(expanded,true).Where(p=>p.StartsWith("Assets/")&&!p.EndsWith("WaterSystemAutomation.cs")).Distinct().ToArray();
        UnityEditor.AssetPackage.Package.Export(new UnityEditor.AssetPackage.ExportPackageParameters(dependencies,output,null,ExportPackageOptions.Default));
        return Path.GetFullPath(output)+" ("+new FileInfo(output).Length+" bytes)";
    }
    static string BuildMessages()
    {
        var report=UnityEditor.Build.Reporting.BuildReport.GetLatestReport();
        return string.Join("\n",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning||m.type==LogType.Error).Select(m=>m.type+": "+m.content));
    }
    static string EncodeMotion()
    {
        string result="";
        foreach(string geometry in new[]{"Plane","Volume"})
        {
            string[] frames=Directory.GetFiles("Outputs/WaterSystem/Animation"+geometry,"frame_*.png").OrderBy(p=>p).ToArray();
            if(frames.Length!=36)throw new Exception("Expected 36 motion frames for "+geometry);
            string output="Outputs/WaterSystem/"+geometry.ToLower()+"-motion.mp4";
            var attributes=new UnityEditor.Media.VideoTrackEncoderAttributes(new UnityEditor.Media.H264EncoderAttributes{gopSize=12,numConsecutiveBFrames=0}){frameRate=new UnityEditor.Media.MediaRational(12),width=1600,height=1000,includeAlpha=false,targetBitRate=8000000};
            using(var encoder=new UnityEditor.Media.MediaEncoder(output,attributes))
            {
                var texture=new Texture2D(1600,1000,TextureFormat.RGBA32,false);var loaded=new Texture2D(2,2);
                try{foreach(string frame in frames){loaded.LoadImage(File.ReadAllBytes(frame));if(loaded.width!=1600||loaded.height!=1000)throw new Exception("Unexpected motion frame dimensions.");texture.SetPixels32(loaded.GetPixels32());texture.Apply();if(!encoder.AddFrame(texture))throw new Exception("Could not encode "+frame);}}
                finally{UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(loaded);}
            }
            result+=output+" ("+new FileInfo(output).Length+" bytes)\n";
        }
        return result;
    }
    static string BuildCheck()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play mode before the build check.");
        AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
        var report=UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Folder+"/WaterShowcase_Planes.unity",Folder+"/WaterShowcase_Volumes.unity"},locationPathName="Outputs/WaterSystem/ValidationBuild/WaterShowcase.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development|BuildOptions.StrictMode});
        var summary=report.summary;string result="Windows development build: "+summary.result+"; errors="+summary.totalErrors+"; warnings="+summary.totalWarnings+"; size="+summary.totalSize+"\n"+BuildMessages();
        File.WriteAllText("Outputs/WaterSystem/player-build-check.txt",result);
        if(summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception(result);
        return result;
    }
    static string Sanity()
    {
        string result=Validate();int missing=0;
        foreach(string sceneName in new[]{"Planes","Volumes"})
        {
            var scene=EditorSceneManager.OpenScene(Folder+"/WaterShowcase_"+sceneName+".unity");
            var roots=scene.GetRootGameObjects();int panels=roots.Count(g=>g.name.Length>2&&char.IsDigit(g.name[0]));
            foreach(var root in roots)foreach(var transform in root.GetComponentsInChildren<Transform>(true))
            {
                missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                var filter=transform.GetComponent<MeshFilter>();if(filter&&!filter.sharedMesh)missing++;
                var renderer=transform.GetComponent<MeshRenderer>();if(renderer)missing+=renderer.sharedMaterials.Count(m=>!m||!m.shader);
            }
            if(panels!=4)throw new Exception(sceneName+" has "+panels+" panels; expected 4.");
            result+=sceneName+": four panels; camera, meshes and material references checked.\n";
        }
        var materials=AssetDatabase.FindAssets("t:Material",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m=>m.HasProperty("_Style")).ToArray();
        if(materials.Length!=8)throw new Exception("Expected eight water materials, found "+materials.Length);
        if(materials.Any(m=>m.GetFloat("_Style")>.5f&&m.GetFloat("_Foam_Bump_Strength")!=0))throw new Exception("Bump is allowed only on Realistic and Lightly Stylized presets.");
        if(materials.Any(m=>m.GetFloat("_Debug_View")!=0))throw new Exception("A material was left in a debug view.");
        if(materials.Any(m=>m.GetFloat("_Enable_Caustics")!=(m.GetFloat("_Style")<.5f?1:0)))throw new Exception("Caustics must be enabled only on Realistic and Lightly Stylized presets.");
        foreach(var material in materials)
        {
            int map=WaterTextureSets[Mathf.RoundToInt(material.GetFloat("_Style")*3)];
            if(AssetDatabase.GetAssetPath(material.GetTexture("_Water_Map"))!="Assets/Textures/Water_"+map+"/Water_"+map+".png"||AssetDatabase.GetAssetPath(material.GetTexture("_Water_Normal"))!="Assets/Textures/Water_"+map+"/Water_Normal_"+map+".png")throw new Exception("Incorrect map pair on "+material.name);
            if(Mathf.Abs(material.GetFloat("_Foam_Bubble_Scale")-FoamBubbleScale)>.001f)throw new Exception("Incorrect bubble density on "+material.name);
            bool realistic=material.GetFloat("_Style")<.1f;
            if(material.GetFloat("_Foam_Intensity")!=1||material.GetFloat("_Contact_Intensity")!=.85f)throw new Exception("Shoreline contact amount changed on "+material.name);
            float patch=(realistic?.55f:1)*PatchFoamAmount;
            if(Mathf.Abs(material.GetFloat("_Surface_Foam_Intensity")-patch)>.001f)throw new Exception("Incorrect drifting foam amount on "+material.name);
            if(Mathf.Abs(material.GetFloat("_Crest_Strength")-(realistic?.45f:.85f)*PatchFoamAmount)>.001f)throw new Exception("Incorrect crest foam amount on "+material.name);
            if(Mathf.Abs(material.GetFloat("_Coastal_Intensity")-(realistic?.48f:.8f)*CoastalFoamAmount)>.001f)throw new Exception("Incorrect coastal foam amount on "+material.name);
        }
        result+="Distinct map pairs on all four styles; coastal bands 2/3, crest and drifting patches 1/2, shoreline contact amount unchanged. Bubble scale "+FoamBubbleScale+" doubles bump count from scale 525; smooth overlapping shapes, bump enabled only on Realistic and Lightly Stylized.\n";
        result+="Eight water presets; all debug views reset. Missing references/scripts: "+missing+"\n";
        if(missing!=0)throw new Exception(result);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
        File.WriteAllText("Outputs/WaterSystem/sanity-check.txt",result);return result;
    }
    static string CurvedSamples()
    {
        var scene=EditorSceneManager.OpenScene(Folder+"/WaterShowcase_Volumes.unity");int before=0,count=0;
        foreach(var root in scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Closed curved volume"))
        {
            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);if(block.GetFloat("_Swell_Height")==0)before++;
            var sample=renderer.GetComponent<WaterCurvedSample>();if(!sample)sample=renderer.gameObject.AddComponent<WaterCurvedSample>();sample.Apply();EditorUtility.SetDirty(sample);count++;
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        scene=EditorSceneManager.OpenScene(scene.path);int checkedCount=0;
        foreach(var root in scene.GetRootGameObjects())foreach(var sample in root.GetComponentsInChildren<WaterCurvedSample>(true))
        {
            var block=new MaterialPropertyBlock();sample.GetComponent<Renderer>().GetPropertyBlock(block);
            if(Mathf.Abs(block.GetFloat("_Swell_Height")-.012f)>.0001f||block.GetFloat("_Enable_Crest_Foam")!=0)throw new Exception("Curved overrides did not survive reload.");checkedCount++;
        }
        if(count!=4||checkedCount!=4)throw new Exception("Expected four persistent curved samples.");
        string result="Curved sample overrides missing before repair: "+before+"; saved and reopened: "+checkedCount+" / 4 correct.\n";File.WriteAllText("Outputs/WaterSystem/curved-reload-check.txt",result);return result;
    }
    static string DetailChecks()
    {
        bool volume=SceneManager.GetActiveScene().name.Contains("Volumes");string geometry=volume?"Volume":"Plane";
        var panels=SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g.name.Length>2&&char.IsDigit(g.name[0])).OrderBy(g=>g.name).ToArray();
        var labels=UnityEngine.Object.FindObjectsByType<TextMesh>();foreach(var label in labels)label.gameObject.SetActive(false);
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        Shader.SetGlobalFloat("_WaterCaptureFreeze",1);Shader.SetGlobalFloat("_WaterCaptureTime",34);string result="";
        try
        {
            for(int i=0;i<2;i++)
            {
                for(int j=0;j<panels.Length;j++)panels[j].SetActive(i==j);
                string style=i==0?"Realistic":"LightlyStylized";
                var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/M_Water_"+style+"_"+geometry+".mat");
                Vector3 focus=panels[i].transform.position+new Vector3(-1,-.1f,-1.8f);
                camera.transform.position=focus+new Vector3(2.5f,3.6f,-4.4f);camera.transform.LookAt(focus);camera.fieldOfView=46;
                string prefix=geometry.ToLower()+"-"+style.ToLower()+"-";
                foreach(string property in new[]{"_Foam_Bump_Strength","_Enable_Caustics","_Foam_Bubble_Scale"})
                {
                    float value=material.GetFloat(property);string feature=property.Contains("Bump")?"foam-bump":property.Contains("Bubble")?"bubble-density":"caustics";
                    try
                    {
                        material.SetFloat(property,property.Contains("Bubble")?PreviousBubbleScale:0);string off=Capture(camera,prefix+feature+"-off",1600,1100);
                        material.SetFloat(property,value);string on=Capture(camera,prefix+feature+"-on",1600,1100);
                        var a=new Texture2D(2,2);a.LoadImage(File.ReadAllBytes(on));var b=new Texture2D(2,2);b.LoadImage(File.ReadAllBytes(off));var ap=a.GetPixels32();var bp=b.GetPixels32();int changed=0;
                        for(int k=0;k<ap.Length;k++)if(Mathf.Abs(ap[k].r-bp[k].r)+Mathf.Abs(ap[k].g-bp[k].g)+Mathf.Abs(ap[k].b-bp[k].b)>9)changed++;
                        UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);
                        if(changed<30)throw new Exception(style+" "+feature+" has no visible change.");
                        result+=geometry+" "+style+" "+feature+": "+changed+" changed pixels; value="+value+"\n";
                    }
                    finally{material.SetFloat(property,value);}
                }
            }
            File.WriteAllText("Outputs/WaterSystem/"+geometry+"-detail-checks.txt",result);return result;
        }
        finally
        {
            Shader.SetGlobalFloat("_WaterCaptureFreeze",0);foreach(var panel in panels)panel.SetActive(true);foreach(var label in labels)label.gameObject.SetActive(true);camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;
        }
    }
    static string CausticPair()
    {
        bool volume=SceneManager.GetActiveScene().name.Contains("Volumes");
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/M_Water_Realistic_"+(volume?"Volume":"Plane")+".mat");
        string[] properties={"_Enable_Crest_Foam","_Enable_Surface_Foam","_Enable_Shoreline_Foam","_Enable_Coastal_Foam","_Enable_Caustics"};var saved=properties.Select(mat.GetFloat).ToArray();
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        try
        {
            for(int i=0;i<4;i++)mat.SetFloat(properties[i],0);
            Vector3 focus=new Vector3(-6.8f,-.55f,6.8f)+new Vector3(0,0,-2);
            camera.transform.position=focus+new Vector3(1.8f,4,-4.8f);camera.transform.LookAt(focus);camera.fieldOfView=48;
            string prefix=(volume?"volume":"plane")+"-caustics-detail-";
            mat.SetFloat("_Enable_Caustics",0);Capture(camera,prefix+"off",1400,1000);
            mat.SetFloat("_Enable_Caustics",1);Capture(camera,prefix+"on",1400,1000);
            return "Captured ground caustics on/off with all foam disabled.";
        }
        finally{for(int i=0;i<properties.Length;i++)mat.SetFloat(properties[i],saved[i]);camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
    }
    static string GraphDrafts(bool reload)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        string result="";
        foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w=>w.GetType().Name=="MaterialGraphEditWindow"))
        {
            var type=window.GetType();string guid=(string)type.GetProperty("selectedGuid",flags).GetValue(window);string path=AssetDatabase.GUIDToAssetPath(guid);
            if(string.IsNullOrEmpty(path))continue;
            if(reload)
            {
                type.GetField("m_AssetMaybeChangedOnDisk",flags).SetValue(window,false);
                type.GetProperty("graphObject",flags).SetValue(window,null);
            }
            else
            {
                string backup="Outputs/WaterSystem/GraphDrafts/"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"/"+Path.GetFileName(path);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Copy(path,backup,true);
                if(!path.EndsWith("SG_Water_Plane.shadergraph")&&!path.EndsWith("SG_Water_Volume.shadergraph"))type.GetMethod("SaveAsset",flags).Invoke(window,null);
                type.GetProperty("graphObject",flags).SetValue(window,null);
                typeof(EditorWindow).GetProperty("hasUnsavedChanges",flags).SetValue(window,false);window.Close();
            }
            result+=(reload?"Reloaded ":"Preserved ")+path+"\n";
        }
        return result;
    }
    [Serializable] class AssetLayout { public AssetMove[] moves; }
    [Serializable] class AssetMove { public string source; public string destination; }
    static string ShaderAsset(string filename)
    {
        string[] paths=AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(filename),new[]{"Assets/Materials/Water/Shader"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>Path.GetFileName(p)==filename).ToArray();
        if(paths.Length!=1)throw new Exception("Expected one shader asset: "+filename+"; found "+paths.Length);
        return paths[0];
    }
    static void AssetFolder(string folder)
    {
        if(AssetDatabase.IsValidFolder(folder))return;
        string parent=Path.GetDirectoryName(folder).Replace('\\','/');AssetFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));
    }
    static string OrganizeAssets()
    {
        var layout=JsonUtility.FromJson<AssetLayout>(File.ReadAllText("Automation/water-asset-layout.json"));
        string result="";
        foreach(var move in layout.moves)
        {
            if(!File.Exists(move.source)&&File.Exists(move.destination))continue;
            AssetFolder(Path.GetDirectoryName(move.destination).Replace('\\','/'));
            string guid=AssetDatabase.AssetPathToGUID(move.source);
            string error=AssetDatabase.MoveAsset(move.source,move.destination);
            if(!string.IsNullOrEmpty(error))throw new Exception(error);
            if(AssetDatabase.AssetPathToGUID(move.destination)!=guid)throw new Exception("Asset ID changed: "+move.source);
            result+=move.source+" -> "+move.destination+"\n";
        }
        AssetDatabase.SaveAssets();File.WriteAllText("Outputs/WaterSystem/asset-organization.txt",result);return result;
    }
    static void OpenGraph(string filename)
    {
        const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w=>w.GetType().Name=="MaterialGraphEditWindow"))if(window.maximized)window.maximized=false;
        string path=ShaderAsset(filename);
        AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(path));
        EditorApplication.delayCall+=()=>
        {
            string guid=AssetDatabase.AssetPathToGUID(path);
            var window=Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().Name=="MaterialGraphEditWindow"&&(string)w.GetType().GetProperty("selectedGuid",flags).GetValue(w)==guid);
            if(!window)return;
            window.maximized=true;window.Focus();
            var editor=window.GetType().GetProperty("graphEditorView",flags).GetValue(window);
            var settings=editor.GetType().GetProperty("viewSettings",flags).GetValue(editor);
            foreach(string field in new[]{"isBlackboardVisible","isInspectorVisible","isPreviewVisible"})settings.GetType().GetField(field).SetValue(settings,false);
            editor.GetType().GetMethod("UpdateSubWindowsVisibility",flags).Invoke(editor,null);
            var view=(UnityEditor.Experimental.GraphView.GraphView)editor.GetType().GetProperty("graphView",flags).GetValue(editor);
            window.rootVisualElement.schedule.Execute(()=>view.FrameAll()).StartingIn(450);
            window.rootVisualElement.schedule.Execute(()=>view.FrameAll()).StartingIn(1300);
        };
    }
}
