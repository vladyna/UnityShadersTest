using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[Serializable]
[PostProcess(typeof(SSAORenderer), PostProcessEvent.AfterStack, "Custom/SSAO")]
public sealed class SSAO : PostProcessEffectSettings
{
    [Range(0f, 1f), Tooltip("SSAO effect intensity.")]
    public FloatParameter blend = new FloatParameter { value = 0.5f };
    public FloatParameter intensity = new FloatParameter { value = 0.5f };
    public ColorParameter color = new ColorParameter { value = Color.white };
}

public sealed class SSAORenderer : PostProcessEffectRenderer<SSAO>
{
    public override void Render(PostProcessRenderContext context)
    {
        var sheet = context.propertySheets.Get(Shader.Find("Hidden/Custom/SSAO_Surface"));
        sheet.properties.SetFloat("_Blend", settings.blend);
        sheet.properties.SetColor("_Intesity", Color.HSVToRGB(0, 0, settings.intensity));
        sheet.properties.SetColor("_Color", settings.color);
        context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
    }
}