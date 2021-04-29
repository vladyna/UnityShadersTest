using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
public enum SsaoTypes
{
    OpenGlSSAO,
    URPSSAO
}
public class SSAO_Compute : MonoBehaviour
{
    #region Public Variables

    public ComputeShader ssaoShader;
    [Range(0.01f, 2.5f)] public float Radius = 1.0f;
    public float Bias = 0.002f;
    [Range(0, 2)] public float Intensity = 1.0f;
    [Range(0.01f, 5)] public float Area = 0.55f;
    [Range(0, 1)] public float BrightnessCorrection = 1.0f;
    public SsaoTypes SsaoType = SsaoTypes.OpenGlSSAO;
    public Texture NoiseTexture;
    #endregion

    #region Private variables

    private RenderTexture renderTarget;
    private RenderTexture normal;
    private RenderTexture depth;
    private RenderTexture normalnormal;
    private int kernelSize = 64;
    private float[] kernelSamples;
    private float[] noiseBuffer;
    private Camera camera;
    private CommandBuffer normalCommandBuffer;
    private CommandBuffer depthcommandBuffer;
    private Material mat;
    private Vector4[] sampleSphere;
    #endregion

    private int createdCommandBuffers = 0;

    private void Awake()
    {
        noiseBuffer = noise();
        kernelSamples = getKernels();
        camera = GetComponent<Camera>();
        camera.depthTextureMode = DepthTextureMode.DepthNormals;

        sampleSphere = new Vector4[]{ new Vector4(0.5381f, 0.1856f,-0.4319f), new Vector4(0.1379f, 0.2486f, 0.4430f),
                new Vector4(0.3371f, 0.5679f,-0.0057f), new Vector4(-0.6999f,-0.0451f,-0.0019f),
                new Vector4(0.0689f,-0.1598f,-0.8547f), new Vector4(0.0560f, 0.0069f,-0.1843f),
                new Vector4(-0.0146f, 0.1402f, 0.0762f), new Vector4(0.0100f,-0.1924f,-0.0344f),
                new Vector4(-0.3577f,-0.5301f,-0.4358f), new Vector4(-0.3169f, 0.1063f, 0.0158f),
                new Vector4(0.0103f,-0.5869f, 0.0046f), new Vector4(-0.0897f,-0.4940f, 0.3287f),
                new Vector4(0.7119f,-0.0154f,-0.0918f), new Vector4(-0.0533f, 0.0596f,-0.5411f),
                new Vector4(0.0352f,-0.0631f, 0.5460f), new Vector4(-0.4776f, 0.2847f,-0.0271f)};
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


    private void OnPreRender()
    {
        mat = new Material(Shader.Find("Hidden/SceenDepthNormal"));
        Shader.SetGlobalMatrix(Shader.PropertyToID("UNITY_MATRIX_IV"), camera.cameraToWorldMatrix.inverse);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        int deptSsaoKernel = SsaoType == SsaoTypes.OpenGlSSAO ? ssaoShader.FindKernel("SSAO") : ssaoShader.FindKernel("SSAO2");
        renderTarget = CreateRenderTexture(source);
        normalnormal = CreateRenderTexture(source);
        SetShadersParameters();
      //  Graphics.Blit(source, normalnormal, mat);
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
        /*Texture2D noiseTexture = new Texture2D(4, 4);
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
        Graphics.Blit(noiseTexture, noiseRender);*/
        RenderTexture noiseRender = new RenderTexture(NoiseTexture.width, NoiseTexture.height, 0);
        noiseRender.wrapModeU = TextureWrapMode.Repeat;
        noiseRender.wrapModeV = TextureWrapMode.Repeat;
        noiseRender.wrapMode = TextureWrapMode.Repeat;
        noiseRender.enableRandomWrite = true;
        Graphics.Blit(NoiseTexture, noiseRender);

        return noiseRender;
    }

    private void Update()
    {
        ssaoShader.SetFloat("_Step", 2.0f / camera.aspect);

    }
    private void SetShadersParameters()
    {

        var ProjectionA = camera.farClipPlane / (camera.farClipPlane - camera.nearClipPlane);
        var ProjectionB = (-camera.farClipPlane * camera.nearClipPlane) / (camera.farClipPlane - camera.nearClipPlane);




        ssaoShader.SetFloat("radius", Radius);
        ssaoShader.SetFloat("bias", Bias);
        ssaoShader.SetFloat("intensity", Intensity);
        ssaoShader.SetFloats("samples", kernelSamples);
        ssaoShader.SetMatrix("projection", camera.projectionMatrix);
        ssaoShader.SetMatrix("projectionInverse", camera.projectionMatrix.inverse);
        ssaoShader.SetInt("kernelSize", kernelSize);
        ssaoShader.SetFloat("screenWidth", Screen.width);
        ssaoShader.SetFloat("screenHeight", Screen.height);
        ssaoShader.SetFloat("ProjectionA", ProjectionA);
        ssaoShader.SetFloat("ProjectionB", ProjectionB);
        ssaoShader.SetVector("cameraPosition", camera.transform.position);
        ssaoShader.SetVectorArray("sample_sphere", sampleSphere);
        ssaoShader.SetFloat("brightnessCorrection", BrightnessCorrection);
        ssaoShader.SetFloat("area", Area);

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
        return Random.Range(0f, 1.0f);
    }
}
