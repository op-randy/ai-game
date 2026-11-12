using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runtime materials for effects (muzzle flash, sparks, tracers, bullet holes, red dot) that work in both
/// URP and the built-in render pipeline.
/// </summary>
public static class FxMaterials
{
    static bool Urp => GraphicsSettings.currentRenderPipeline != null;

    /// <summary>Glowing, additive (adds light): muzzle flash, sparks, tracers.</summary>
    public static Material Additive(Texture tex, Color color)
    {
        Material m;
        var urp = Urp ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : null;
        if (urp != null)
        {
            m = new Material(urp);
            m.SetFloat("_Surface", 1f);     // transparent
            m.SetFloat("_Blend", 2f);       // additive
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
        }
        else
        {
            m = new Material(Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default"));
            if (tex) m.mainTexture = tex;
            m.SetColor("_TintColor", color);
            m.color = color;
        }
        return m;
    }

    /// <summary>Alpha-blended unlit: bullet holes.</summary>
    public static Material AlphaBlended(Texture tex, Color color)
    {
        Material m;
        var urp = Urp ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : null;
        if (urp != null)
        {
            m = new Material(urp);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);       // alpha
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
        }
        else
        {
            m = new Material(Shader.Find("Sprites/Default"));
            if (tex) m.mainTexture = tex;
            m.color = color;
        }
        return m;
    }

    /// <summary>Flat unlit colour: the red dot.</summary>
    public static Material Unlit(Color color)
    {
        var s = Urp ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Color");
        var m = new Material(s ?? Shader.Find("Unlit/Color"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        m.color = color;
        return m;
    }
}
