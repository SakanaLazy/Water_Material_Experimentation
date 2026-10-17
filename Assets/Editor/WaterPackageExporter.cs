using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Package export
public static class WaterPackageExporter
{
    const string Output = "Outputs/WaterSystem/Release/SplitPackages";
    const string ShaderRoot = "Assets/Materials/Water/Shader/";
    const string NotesRoot = "Assets/Water/Documentation/Packages";
    static readonly string[] Styles = { "Realistic", "LightlyStylized", "StronglyStylized", "Graphic" };
    static readonly string[] GraphRoots = {
        ShaderRoot+"Masters", ShaderRoot+"Subgraphs", ShaderRoot+"Includes", ShaderRoot+"Hidden",
        "Assets/Water/Runtime/WaterVolumeBackDepthFeature.cs",
        "Assets/Editor/WaterSystemShaderGUI.cs", "Assets/Editor/WaterVolumeSetup.cs",
        "Assets/Materials/Water/Textures/CausticsAtlas.png",
        "Assets/Water/Documentation/LICENSE.txt", "Assets/Water/Documentation/CREDIT.txt"
    };
    [Serializable] sealed class PackageRecord
    {
        public string file, label, sha256;
        public long bytes;
        public string[] assets, requiredPackages;
    }
    [Serializable] sealed class Catalog
    {
        public string unityVersion;
        public PackageRecord[] packages;
    }

    [MenuItem("Water System/Export Six Packages")]
    public static void ExportMenu() { Debug.Log(ExportAll()); }

    public static string ExportAll()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before exporting water packages.");
        AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
        Directory.CreateDirectory(Output);Directory.CreateDirectory(NotesRoot);
        var required=RequiredPackages();
        File.WriteAllText(NotesRoot+"/Dependencies.json",JsonUtility.ToJson(new Catalog{unityVersion=Application.unityVersion,packages=new[]{new PackageRecord{label="Unity Package Manager prerequisites",requiredPackages=required}}},true)+"\n");
        AssetDatabase.ImportAsset(NotesRoot+"/Dependencies.json",ImportAssetOptions.ForceSynchronousImport);
        var records=new List<PackageRecord>();
        foreach(string style in Styles)
        {
            string name="WaterSystem_"+style;
            var roots=GraphRoots.Concat(new[]{
                "Assets/WaterShowcase/M_Water_"+style+"_Plane.mat",
                "Assets/WaterShowcase/M_Water_"+style+"_Volume.mat"
            });
            records.Add(Export(name,style+" / plane and volume",roots,required,false));
        }
        records.Add(Export("WaterSystem_GraphsOnly","Graphs and required shader support",GraphRoots,required,false));
        var allPresets=Styles.SelectMany(style=>new[]{
            "Assets/WaterShowcase/M_Water_"+style+"_Plane.mat",
            "Assets/WaterShowcase/M_Water_"+style+"_Volume.mat"
        });
        records.Add(Export("WaterSystem_Complete","All four styles / all eight presets",GraphRoots.Concat(allPresets),required,true));
        var catalog=new Catalog{unityVersion=Application.unityVersion,packages=records.ToArray()};
        File.WriteAllText(Output+"/package-catalog.json",JsonUtility.ToJson(catalog,true)+"\n");
        File.WriteAllText(Output+"/SHA256SUMS.txt",string.Join("\n",records.Select(r=>r.sha256+"  "+r.file))+"\n");
        File.WriteAllText(Output+"/README.md",ReleaseNotes(records,required));
        AssetDatabase.SaveAssets();
        return "Exported six water packages to "+Path.GetFullPath(Output)+"\n"+string.Join("\n",records.Select(r=>r.file+": "+r.assets.Length+" assets, "+r.bytes+" bytes"));
    }

    static PackageRecord Export(string name,string label,IEnumerable<string> roots,string[] required,bool complete)
    {
        string note=NotesRoot+"/"+name+".txt";
        string contents=label+"\n\n"+ImportNotes(required)+"\n";
        contents+=complete?"Includes all eight water presets.\n":"";
        contents+="Includes required shader dependencies. No demo scenes or project renderer settings are included.\n";
        if(name.EndsWith("GraphsOnly"))contents+="No preset materials are included. Required HLSL, default textures, caustic atlas, Inspector and volume renderer support accompany the graphs. Create a material from Water System/SG_Water_Plane or Water System/SG_Water_Volume.\n";
        else if(!complete)contents+="Includes the plane and volume presets for this style. The shared graphs expose all style controls, but the other three styles' material presets are not included.\n";
        contents+="Packages retain shared GUIDs and can be imported together. Importing another package updates the shared assets instead of duplicating them.\n";
        contents+="The current system supports viewing water from outside. Underwater camera rendering is not implemented.\n";
        contents+="Free to use, modify and redistribute. Commercial use requires visible credit: Water system by LazySakana — https://lazysakana.co.in. Public credit is optional for noncommercial use. See Assets/Water/Documentation/LICENSE.txt.\n";
        File.WriteAllText(note,contents);AssetDatabase.ImportAsset(note,ImportAssetOptions.ForceSynchronousImport);
        var expanded=roots.Concat(new[]{note,NotesRoot+"/Dependencies.json"}).SelectMany(Expand).Distinct().ToArray();
        var assets=AssetDatabase.GetDependencies(expanded,true).Where(IsLocal).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        foreach(string asset in assets)
        {
            if(!File.Exists(asset)&&!AssetDatabase.IsValidFolder(asset))throw new FileNotFoundException("Missing package dependency",asset);
            if(asset.Contains("/Deprecated/")||asset.Contains("/References/")||asset.StartsWith("Assets/Settings/")||asset.EndsWith("WaterSystemAutomation.cs")||asset.EndsWith("WaterPackageExporter.cs"))throw new Exception("Unexpected dependency: "+asset);
        }
        foreach(string asset in AssetDatabase.GetDependencies(assets,true).Where(IsLocal))
            if(!assets.Contains(asset))throw new Exception("Incomplete dependency closure: "+asset);
        int presets=assets.Count(p=>p.StartsWith("Assets/WaterShowcase/M_Water_")&&p.EndsWith(".mat"));
        if(presets!=(complete?8:name.EndsWith("GraphsOnly")?0:2))throw new Exception("Incorrect material scope in "+name+": "+presets);
        if(assets.Any(p=>p.EndsWith(".unity")))throw new Exception("Unexpected scene in "+name);
        if(name.EndsWith("GraphsOnly")&&assets.Any(p=>p.EndsWith(".mat")))throw new Exception("Unexpected material in graphs package.");
        string output=Output+"/"+name+".unitypackage";
        UnityEditor.AssetPackage.Package.Export(new UnityEditor.AssetPackage.ExportPackageParameters(assets,output,null,ExportPackageOptions.Default));
        return new PackageRecord{file=Path.GetFileName(output),label=label,bytes=new FileInfo(output).Length,sha256=Hash(output),assets=assets,requiredPackages=required};
    }

    static IEnumerable<string> Expand(string asset)
    {
        if(AssetDatabase.IsValidFolder(asset))return AssetDatabase.FindAssets("",new[]{asset}).Select(AssetDatabase.GUIDToAssetPath).Append(asset);
        if(!File.Exists(asset))throw new FileNotFoundException("Missing export root",asset);
        return new[]{asset};
    }
    static bool IsLocal(string path) { return path.StartsWith("Assets/",StringComparison.Ordinal); }
    static string Hash(string path)
    {
        using(var hash=SHA256.Create())using(var input=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();
    }
    static string[] RequiredPackages()
    {
        var installed=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
        var resolved=new SortedDictionary<string,string>(StringComparer.Ordinal);
        var queue=new Queue<string>(new[]{"com.unity.render-pipelines.universal","com.unity.shadergraph"});
        while(queue.Count>0)
        {
            string name=queue.Dequeue();if(resolved.ContainsKey(name))continue;
            var package=installed.FirstOrDefault(p=>p.name==name);
            if(package==null)throw new Exception("Missing Unity package: "+name);
            resolved[name]=package.version;
            foreach(var dependency in package.dependencies)queue.Enqueue(dependency.name);
        }
        return resolved.Select(p=>p.Key+"@"+p.Value).ToArray();
    }
    static string ImportNotes(string[] required)
    {
        var text=new StringBuilder();
        text.AppendLine("Exported with Unity "+Application.unityVersion+". Install Universal RP and Shader Graph in Package Manager before importing; Unity resolves their dependencies.");
        text.AppendLine("Resolved package versions:\n"+string.Join("\n",required));
        text.AppendLine("Unity's Package Manager assets are prerequisites, not copied Library/PackageCache files.");
        text.AppendLine("After import choose Water > Set Up Active URP Renderer. This enables opaque/depth textures, assigns layer 30 / Water Volume if available, and installs the back-face depth renderer feature.");
        text.AppendLine("If layer 30 is occupied, the helper stops; configure a dedicated volume layer and renderer feature manually. Put closed water renderers on that layer. Use the plane master for surface meshes and the volume master for separate closed meshes.");
        text.AppendLine("Choose your URP asset in Graphics/Quality settings before running setup. No project pipeline or layer settings are overwritten during import.");
        return text.ToString();
    }
    static string ReleaseNotes(IEnumerable<PackageRecord> records,string[] required)
    {
        var text=new StringBuilder("# Water System packages\n\n");
        foreach(var record in records)text.AppendLine("- `"+record.file+"`: "+record.label+".");
        text.AppendLine("\n"+ImportNotes(required));
        text.AppendLine("Style packages contain both plane and volume presets plus all required local dependencies. GraphsOnly contains the editable graphs and their required HLSL, textures, Inspector and renderer support; it has no preset materials. Complete includes all four styles and all eight water materials. None of the packages include demo scenes.");
        text.AppendLine("\nPackages share GUIDs and can be imported together. Source asset lists, external package requirements and SHA-256 hashes are recorded in package-catalog.json and SHA256SUMS.txt. Export tools and deprecated graphs are excluded.");
        text.AppendLine("\nThe water materials do not require an input package. Underwater camera rendering is not supported.");
        text.AppendLine("\nFree to use, modify and redistribute. Commercial use requires visible credit: Water system by LazySakana — https://lazysakana.co.in. Public credit is optional for noncommercial use. The attribution licence is included in every package.");
        return text.ToString();
    }
}
