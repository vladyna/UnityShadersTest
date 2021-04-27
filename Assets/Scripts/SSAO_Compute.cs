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
    public float Intensity = 1.0f;
    #endregion

    #region Private variables

    private RenderTexture renderTarget;
    private RenderTexture normal;
    private RenderTexture depth;
    private int kernelSize = 64;
    private float[] kernelSamples;
    private float[] noiseBuffer;
    private Camera camera;
    private CommandBuffer normalCommandBuffer;
    private CommandBuffer depthcommandBuffer;
    #endregion

    private int createdCommandBuffers = 0;

    private void Awake()
    {
        noiseBuffer = noise();
        kernelSamples = getKernels();
        camera = GetComponent<Camera>();
        camera.depthTextureMode = DepthTextureMode.DepthNormals;

    }

    void OnEnable()
    {
        var basicTexture = new RenderTexture(Screen.width, Screen.height, 0);
        normal = CreateRenderTexture(basicTexture);
        depth = CreateRenderTexture(basicTexture);
        normalCommandBuffer = CreateCommandBuffer("NormalDepth", "_gDepthNormals", BuiltinRenderTextureType.DepthNormals, normal);

        depthcommandBuffer = CreateCommandBuffer("depth", "_gDepth", BuiltinRenderTextureType.Depth, depth);

        camera.AddCommandBuffer(CameraEvent.AfterDepthNormalsTexture, normalCommandBuffer);
        camera.AddCommandBuffer(CameraEvent.AfterDepthTexture, depthcommandBuffer);
    }

    void OnDisable()
    {
        Camera.main.RemoveCommandBuffer(CameraEvent.AfterDepthTexture, normalCommandBuffer);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        int deptSsaoKernel = ssaoShader.FindKernel("SSAO");
        renderTarget = CreateRenderTexture(source);
        SetShadersParameters();

        RenderTexture noiseRender = CreateNoiseTextureFromArray(noiseBuffer);

        ssaoShader.SetTexture(deptSsaoKernel, "_noiseTexture", noiseRender);
        ssaoShader.SetTexture(deptSsaoKernel, "_Results", renderTarget);


        ssaoShader.SetTextureFromGlobal(deptSsaoKernel, "_gNormal", "_gDepthNormals");
        ssaoShader.SetTextureFromGlobal(deptSsaoKernel, "_gPosition", "_gDepth");
        ssaoShader.GetKernelThreadGroupSizes(deptSsaoKernel, out uint groupX, out uint groupY, out uint groupZ);
        ssaoShader.Dispatch(deptSsaoKernel, Screen.width / (int)groupX, Screen.height / (int)groupY, (int)groupZ);



        //TODO(): Pass SSAO further
        Graphics.Blit(renderTarget, destination);
        ReleaseRenderTargets();
    }

    private CommandBuffer CreateCommandBuffer(string name, string globalTextureName, BuiltinRenderTextureType source, RenderTexture texture)
    {
        createdCommandBuffers++;
        var cmdBuffer = new CommandBuffer();
        cmdBuffer.name = name;
        int TempID = Shader.PropertyToID($"_Temp{createdCommandBuffers}");
        Debug.Log(TempID);
        cmdBuffer.GetTemporaryRT(TempID, -1, -1, 24, FilterMode.Bilinear);
        cmdBuffer.SetRenderTarget(texture);
        cmdBuffer.ClearRenderTarget(true, true, Color.black);
        cmdBuffer.SetGlobalTexture(globalTextureName, texture);
        cmdBuffer.Blit(source, texture);
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

    private void Update()
    {
        ssaoShader.SetFloat("_Step", 2.0f / camera.aspect);

    }
    private void SetShadersParameters()
    {
        ssaoShader.SetFloat("radius", Radius);
        ssaoShader.SetFloat("bias", Bias);
        ssaoShader.SetFloat("intensity", Intensity);
        ssaoShader.SetFloats("samples", kernelSamples);
        ssaoShader.SetInt("kernelSize", kernelSize);
        ssaoShader.SetFloat("screenWidth", Screen.width);
        ssaoShader.SetFloat("screenHeight", Screen.height);

        ssaoShader.SetFloats("noiseBuffer", noiseBuffer);
        ssaoShader.SetVector("noiseScale", new Vector4(Screen.width / 4, Screen.height / 4, 0, 0));
    }

    private void ReleaseRenderTargets()
    {
        renderTarget?.Release();
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
