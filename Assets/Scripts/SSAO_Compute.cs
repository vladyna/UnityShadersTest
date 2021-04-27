using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SSAO_Compute : MonoBehaviour
{
    #region Public Variables

    public ComputeShader ssaoShader;
    public float Radius = 1.0f;
    public float Bias = 0.002f;

    #endregion

    #region Private variables

    private RenderTexture renderTarget;
    private RenderTexture position;
    private RenderTexture normal;
    private RenderTexture ssao;
    private int kernelSize = 64;
    private float[] kernelSamples;
    private float[] noiseBuffer;
    private Camera camera;
    private Material mat;
    private Material normalTexture;
    private CommandBuffer normalCommandBuffer;
    private CommandBuffer prelightcommanBuffer;
    #endregion



    private void Awake()
    {
        noiseBuffer = noise();
        kernelSamples = getKernels();
        camera = GetComponent<Camera>();
        camera.depthTextureMode = DepthTextureMode.DepthNormals;

    }

    void OnEnable()
    {
        normalCommandBuffer = CreateCommandBuffer("NormalDepth", "_gDepthNormals", BuiltinRenderTextureType.DepthNormals);

        prelightcommanBuffer = CreateCommandBuffer("prelight", "_gPrelight", BuiltinRenderTextureType.Depth);

        camera.AddCommandBuffer(CameraEvent.AfterDepthNormalsTexture, normalCommandBuffer);
        camera.AddCommandBuffer(CameraEvent.AfterDepthTexture, prelightcommanBuffer);
    }

    void OnDisable()
    {
        Camera.main.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, normalCommandBuffer);
    }

    void Start()
    {

        mat = new Material(Shader.Find("Hidden/SceenDepthNormal"));
    }

    private void OnPreRender()
    {
        Shader.SetGlobalMatrix(Shader.PropertyToID("UNITY_MATRIX_IV"), camera.cameraToWorldMatrix.inverse);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        int deptSsaoKernel = ssaoShader.FindKernel("SSAO");
        renderTarget = CreateRenderTexture(source);

        //TODO(): Find way to get pure position texture before light pass
        position = CreateRenderTexture(source);
        normal = CreateRenderTexture(source);
        ssao = CreateRenderTexture(source);
       // var globaltexture = CreateRenderTexture(source);
      //  globaltexture.SetGlobalShaderProperty("_glowMap");
        SetShadersParameters();

        RenderTexture noiseRender = CreateNoiseTextureFromArray(noiseBuffer);

        // Gets normal and depth from camera
        // TODO(): Find a better way to extract normal and depth from camera
/*        cmdBuffer = new CommandBuffer();
        cmdBuffer.name = "cmdBuffer";
        int TempID = Shader.PropertyToID("_Temp1");
        cmdBuffer.GetTemporaryRT(TempID, -1, -1, 24, FilterMode.Bilinear);
        cmdBuffer.SetRenderTarget(TempID);
        cmdBuffer.ClearRenderTarget(true, true, Color.black);
        cmdBuffer.SetGlobalTexture("_GlowMap", TempID);
        cmdBuffer.Blit(BuiltinRenderTextureType.DepthNormals, BuiltinRenderTextureType.PropertyName, normalTexture);
        camera.AddCommandBuffer(CameraEvent.AfterDepthNormalsTexture, cmdBuffer);*/
   

        /*        CommandBuffer commandbuffer = new CommandBuffer();
                commandbuffer.Blit(BuiltinRenderTextureType.DepthNormals, BuiltinRenderTextureType.CameraTarget);
                camera.AddCommandBuffer(CameraEvent.AfterDepthNormalsTexture, commandbuffer);*/
        // Graphics.Blit(camera.targetTexture, normal);
        // camera.RemoveCommandBuffer(CameraEvent.AfterDepthNormalsTexture, commandbuffer);
        // Graphics.Blit(source, normal, mat);
       // normal.SetGlobalShaderProperty("_GlowMap");

       // ssaoShader.SetTexture(deptSsaoKernel, "_gPosition", position);
        ssaoShader.SetTexture(deptSsaoKernel, "_noiseTexture", noiseRender);
        ssaoShader.SetTexture(deptSsaoKernel, "_Results", renderTarget);
        
        ssaoShader.SetTextureFromGlobal(deptSsaoKernel, "_gNormal", "_gDepthNormals");
        ssaoShader.SetTextureFromGlobal(deptSsaoKernel, "_gPosition", "_gPrelight");
        ssaoShader.GetKernelThreadGroupSizes(deptSsaoKernel, out uint groupX, out uint groupY, out uint groupZ);
        ssaoShader.Dispatch(deptSsaoKernel, Screen.width / (int)groupX, Screen.height / (int)groupY, (int)groupZ);

        Graphics.Blit(renderTarget, ssao);





        //TODO(): Pass SSAO further
        Graphics.Blit(renderTarget, destination);
        ReleaseRenderTargets();
    }

    private CommandBuffer CreateCommandBuffer(string name, string globalTextureName, BuiltinRenderTextureType source)
    {
        var cmdBuffer = new CommandBuffer();
        cmdBuffer.name = name;
        int TempID = Shader.PropertyToID("_Temp1");
        cmdBuffer.GetTemporaryRT(TempID, -1, -1, 24, FilterMode.Bilinear);
        cmdBuffer.SetRenderTarget(TempID);
        cmdBuffer.ClearRenderTarget(true, true, Color.black);
        cmdBuffer.SetGlobalTexture(globalTextureName, TempID);
        cmdBuffer.Blit(source, TempID);
        return cmdBuffer;
    }

    private void ShowArray<T>(T[] array)
    {
        for (var i = 0; i < array.Length; i ++)
        {
            Debug.Log($"{array[i]}");
        }

    }

    private RenderTexture CreateNoiseTextureFromArray(float[] noiseBuffer)
    {
        Texture2D noiseTexture = new Texture2D(4, 4);
        noiseTexture.wrapMode = TextureWrapMode.Repeat;
        noiseTexture.wrapModeU = TextureWrapMode.Repeat;
        noiseTexture.wrapModeV = TextureWrapMode.Repeat;
        noiseTexture.SetPixelData(noiseBuffer, 0);
        noiseTexture.Apply();
        RenderTexture noiseRender = new RenderTexture(4, 4, 0);
        noiseRender.wrapModeU = TextureWrapMode.Repeat;
        noiseRender.wrapModeV = TextureWrapMode.Repeat;
        noiseRender.wrapMode = TextureWrapMode.Repeat;
        noiseRender.enableRandomWrite = true;
        Graphics.Blit(noiseTexture, noiseRender);
        return noiseRender;
    }

    private void SetShadersParameters()
    {
        ssaoShader.SetFloat("gAspectRatio", camera.aspect);
        ssaoShader.SetFloat("gTanHalfFOV", Mathf.Tan( camera.fieldOfView / 2.0f * Mathf.Deg2Rad));
        ssaoShader.SetFloat("radius", Radius);
        ssaoShader.SetMatrix("projection", camera.projectionMatrix);
 
        ssaoShader.SetFloat("bias", Bias);
        ssaoShader.SetFloats("samples", kernelSamples);
        ssaoShader.SetInt("kernelSize", kernelSize);

        ssaoShader.SetFloats("noiseBuffer", noiseBuffer);
        ssaoShader.SetMatrix("viewMatrix", camera.cameraToWorldMatrix);
        ssaoShader.SetVector("noiseScale", new Vector4(Screen.width / 4, Screen.height / 4, 0, 0));
    }

    private void ReleaseRenderTargets()
    {
        renderTarget.Release();
        ssao.Release();
        normal.Release();
        position.Release();
    }

    private RenderTexture CreateRenderTexture(RenderTexture source)
    {
        RenderTexture renderTexture = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        Graphics.Blit(source, renderTexture);
        return renderTexture;
    }

    // ToDO(): Ability to dynamically  change kernels
    private float[] getKernels()
    {
        float[] kernelBuffer = new float[kernelSize * 3];
        
        for (int i = 0; i < kernelSize; i++)
        {
            Vector3 sample = new Vector3(
                    (GetRandomFloat() * 2.0f - 1.0f), 
                    (GetRandomFloat() * 2.0f - 1.0f),
                    GetRandomFloat());
            sample.Normalize();
            sample *= GetRandomFloat();
            float scale = (float)i / kernelSize;
            scale = Mathf.Lerp(0.1f, 1.0f, scale * scale);
            sample *= scale;
            var pos = i * 3;
            kernelBuffer.SetValue(sample.x, pos);
            kernelBuffer.SetValue(sample.y, pos + 1);
            kernelBuffer.SetValue(sample.z, pos + 2);
        }
        return kernelBuffer;
    }

    private float[] noise()
    {
        float[] noiseBuffer = new float[4 * 4 * 3];
        for (int i = 0; i < 16; i++)
        {
            Vector3 sample = new Vector3(
                GetRandomFloat() * 2.0f - 1.0f,
                GetRandomFloat() * 2.0f - 1.0f, 
                0);
            int position = i * 3;
            noiseBuffer.SetValue(sample.x, position);
            noiseBuffer.SetValue(sample.y, position + 1);
            noiseBuffer.SetValue(sample.z, position + 2);
        }
        return noiseBuffer;
    }

    private float GetRandomFloat()
    {
        return Random.Range(0.1f, 1.0f);
    }
}
