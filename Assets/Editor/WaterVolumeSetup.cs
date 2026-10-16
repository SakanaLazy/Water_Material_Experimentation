using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class WaterVolumeSetup
{
    [MenuItem("Water/Set Up Active URP Renderer")]
    public static void Install()
    {
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if(!pipeline)throw new InvalidOperationException("Select a URP pipeline before setting up water.");
        string layerName=LayerMask.LayerToName(30);
        if(!string.IsNullOrEmpty(layerName)&&layerName!="Water Volume")
            throw new InvalidOperationException("Layer 30 is already in use. Configure a dedicated volume layer and add WaterVolumeBackDepthFeature manually.");
        var settings=new SerializedObject(pipeline);
        var renderers=settings.FindProperty("m_RendererDataList");
        var targets=Enumerable.Range(0,renderers.arraySize).Select(i=>renderers.GetArrayElementAtIndex(i).objectReferenceValue).OfType<UniversalRendererData>().ToArray();
        if(targets.Length==0)throw new InvalidOperationException("The active pipeline needs a Universal Renderer for water volumes.");
        var shader=Shader.Find("Hidden/Water/VolumeBackDepth");
        if(!shader)throw new InvalidOperationException("Import the volume back-depth shader first.");
        var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        tags.FindProperty("layers").GetArrayElementAtIndex(30).stringValue="Water Volume";
        tags.ApplyModifiedProperties();
        Undo.RecordObject(pipeline,"Set up water buffers");
        pipeline.supportsCameraDepthTexture=true;pipeline.supportsCameraOpaqueTexture=true;
        EditorUtility.SetDirty(pipeline);
        foreach(var renderer in targets)
        {
            var feature=renderer.rendererFeatures.OfType<WaterVolumeBackDepthFeature>().FirstOrDefault();
            if(!feature)
            {
                feature=ScriptableObject.CreateInstance<WaterVolumeBackDepthFeature>();feature.name="Water volume back-face depth";
                AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);
            }
            feature.volumeLayer=1<<30;
            var serialized=new SerializedObject(feature);serialized.FindProperty("backDepthShader").objectReferenceValue=shader;serialized.ApplyModifiedPropertiesWithoutUndo();
            feature.Create();EditorUtility.SetDirty(feature);renderer.SetDirty();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(ScriptableRendererData).GetMethod("UpdateMap",flags)?.Invoke(renderer,null);
            EditorUtility.SetDirty(renderer);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Water ready: depth and opaque textures enabled; volume renderers use layer 30 / Water Volume.");
    }
}
