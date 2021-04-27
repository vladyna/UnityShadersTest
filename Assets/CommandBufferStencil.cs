using UnityEngine;
using UnityEngine.Rendering;

public class CommandBufferStencil : MonoBehaviour
{
    private CommandBuffer cmdBuffer;
    public Material stencil_is_on_mat;

    void OnEnable()
    {
        if (cmdBuffer == null)
        {
            cmdBuffer = new CommandBuffer();
            cmdBuffer.name = "cmdBuffer";
            cmdBuffer.Blit(BuiltinRenderTextureType.None, BuiltinRenderTextureType.None, stencil_is_on_mat); 
            Camera.main.AddCommandBuffer(CameraEvent.BeforeImageEffects, cmdBuffer); 
        }
    }

    void OnDisable()
    {
        if (cmdBuffer != null)
            Camera.main.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, cmdBuffer);
        cmdBuffer = null;
    }
}