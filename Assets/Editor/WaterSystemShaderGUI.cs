using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class WaterSystemShaderGUI : ShaderGUI
{
    readonly Dictionary<string,bool> expanded = new Dictionary<string,bool>();
    static string Category(string name)
    {
        if(name=="_Style"||name=="_Debug_View")return "Art direction and diagnostics";
        if(name.StartsWith("_Wave_")||name.StartsWith("_Swell_"))return "Waves and displacement";
        if(name.StartsWith("_Crest_")||name.StartsWith("_Detail_")||name=="_Enable_Crest_Foam")return "Crest foam";
        if(name.StartsWith("_Surface_Foam")||name=="_Enable_Surface_Foam")return "Drifting surface foam";
        if(name.StartsWith("_Contact")||name=="_Enable_Shoreline_Foam")return "Contact foam";
        if(name.StartsWith("_Coastal")||name=="_Enable_Coastal_Foam")return "Coastal breakers";
        if(name.Contains("Foam"))return "Foam composition";
        if(name.Contains("Depth")||name.Contains("Deep")||name.Contains("Fallback"))return "Depth and absorption";
        if(name.Contains("Refraction"))return "Refraction";
        if(name.Contains("Caustics"))return "Submerged caustics";
        return "Water surface";
    }
    public override void OnGUI(MaterialEditor editor,MaterialProperty[] properties)
    {
        var material=(Material)editor.target;
        bool volume=material.shader.name.Contains("Volume");
        EditorGUILayout.LabelField(volume?"WATER / CLOSED VOLUME":"WATER / PLANE SURFACE",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Four identities • shared modular water system",EditorStyles.miniLabel);
        EditorGUILayout.Space(8);
        foreach(var group in properties.Where(p=>(p.propertyFlags&ShaderPropertyFlags.HideInInspector)==0).Where(p=>volume||!p.name.StartsWith("_Volume_")).GroupBy(p=>Category(p.name)))
        {
            if(!expanded.ContainsKey(group.Key))expanded[group.Key]=true;
            expanded[group.Key]=EditorGUILayout.BeginFoldoutHeaderGroup(expanded[group.Key],group.Key);
            if(expanded[group.Key])
            {
                foreach(var property in group)
                {
                    if(property.name=="_Debug_View")
                    {
                        EditorGUI.showMixedValue=property.hasMixedValue;
                        EditorGUI.BeginChangeCheck();
                        int view=EditorGUILayout.Popup("Debug view",Mathf.RoundToInt(property.floatValue),new[]{"Final material","Crest foam","Surface foam","Contact foam","Coastal foam","Thickness / absorption","Surface normals","Unlit body color","Transmitted background","Submerged caustics"});
                        if(EditorGUI.EndChangeCheck())property.floatValue=view;
                        EditorGUI.showMixedValue=false;
                    }
                    else editor.ShaderProperty(property,property.displayName);
                }
                EditorGUILayout.Space(6);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        EditorGUILayout.Space(8);
        editor.RenderQueueField();
        editor.EnableInstancingField();
    }
}
