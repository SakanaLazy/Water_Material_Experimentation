using UnityEngine;

// Curved showcase settings must survive scene reloads.
[ExecuteAlways, RequireComponent(typeof(Renderer))]
public sealed class WaterCurvedSample : MonoBehaviour
{
    void OnEnable() { Apply(); }
    void OnValidate() { Apply(); }
    public void Apply()
    {
        var renderer=GetComponent<Renderer>();
        var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
        block.SetFloat("_Volume_Normal_Tiling",1.2f);
        block.SetFloat("_Swell_Length",1.2f);block.SetFloat("_Swell_Height",.012f);
        block.SetFloat("_Enable_Shoreline_Foam",0);block.SetFloat("_Enable_Coastal_Foam",0);
        block.SetFloat("_Enable_Crest_Foam",0);block.SetFloat("_Surface_Foam_Amount",.25f);
        renderer.SetPropertyBlock(block);
    }
}
