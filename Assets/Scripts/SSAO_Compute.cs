using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SSAO_Compute : MonoBehaviour
{
    public ComputeShader ssaoShader;
    public float Radius = 1.0f;
    public float Bias = 0.002f;
    public int kernelSize = 64;
    public float Diffuse = 1;
    public GameObject Geometry;
    public Light directionalLight;


    private RenderTexture positionBasic;
    private RenderTexture normalBasic;
    private RenderTexture albedoBasic;
    public GameObject model;


    private RenderTexture renderTarget;
    private RenderTexture position;
    private RenderTexture normal;
    private RenderTexture depth;
    private RenderTexture ssao;
    private RenderTexture blur;
    private float[] kernelSamples;
    private float[] noiseBuffer;
    private Color32[] Noises;
    private Camera camera;
    private Material mat;
    private bool isDepth = false;
    // Start is called before the first frame update

    private void OnPreRender()
    {
        Shader.SetGlobalMatrix(Shader.PropertyToID("UNITY_MATRIX_IV"), camera.cameraToWorldMatrix.inverse);

    }
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        int deptSsaoKernel = isDepth ? ssaoShader.FindKernel("deptSsao") : ssaoShader.FindKernel("SSAO");
        int BlurKernel = ssaoShader.FindKernel("Blur");
        int LightingKernel = ssaoShader.FindKernel("Lighting");
        renderTarget = CreateRenderTexture(source);
        position = CreateRenderTexture(source);
        normal = CreateRenderTexture(source);
        depth = CreateRenderTexture(source);
        SetShadersParameters();

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
        //Graphics.Blit(normal, destination);
        Graphics.Blit(source, normal, mat);
        if (isDepth)
        {

        }
        else
        {
          //  var tex = new RenderTexture();
    
            ssaoShader.SetTexture(deptSsaoKernel, "_gPosition", position);
            ssaoShader.SetTexture(deptSsaoKernel, "_gNormal", normal);
            ssaoShader.SetTexture(deptSsaoKernel, "_noiseTexture", noiseRender);
            ssaoShader.SetTexture(deptSsaoKernel, "_Results", renderTarget);

            ssaoShader.GetKernelThreadGroupSizes(deptSsaoKernel, out uint groupX, out uint groupY, out uint groupZ);
            ssaoShader.Dispatch(deptSsaoKernel, Screen.width / (int)groupX, Screen.height / (int)groupY, (int)groupZ);
           // ssao = CreateRenderTexture(renderTarget);
        }
/*        ssaoShader.SetTexture(BlurKernel, "_Results", renderTarget);
        ssaoShader.SetTexture(BlurKernel, "_gPosition", position);
        ssaoShader.SetTexture(BlurKernel, "_gDepth", normal);
        ssaoShader.SetTexture(BlurKernel, "_gSSAO", ssao);
        ssaoShader.SetTexture(BlurKernel, "Positions", normal);
      //  ssaoShader.Dispatch(BlurKernel, Screen.width / 8, Screen.height / 8, 1);
        blur = CreateRenderTexture(renderTarget);
        ssaoShader.SetTexture(LightingKernel, "_Results", renderTarget);
        ssaoShader.SetTexture(LightingKernel, "_gPosition", position);
        ssaoShader.SetTexture(LightingKernel, "_gSSAO", ssao);
        ssaoShader.SetTexture(LightingKernel, "_gBlur", blur);
        ssaoShader.SetTexture(LightingKernel, "Positions", position);
        ssaoShader.SetTexture(LightingKernel, "depthTexture", normal);
        ssaoShader.SetTexture(LightingKernel, "_gDepth", normal);
        ssaoShader.SetFloat("AmbientIntensity", directionalLight.intensity);
        ssaoShader.SetFloat("DiffuseIntensity", Diffuse);
        var lightMatrix = directionalLight.transform.localToWorldMatrix;
        Vector4 lightDirection = new Vector4(lightMatrix.m20, lightMatrix.m21, lightMatrix.m23);
        Vector3 lightPosition = directionalLight.transform.position;
        ssaoShader.SetVector("Direction", lightDirection);
        ssaoShader.SetVector("Color", directionalLight.color);
        ssaoShader.SetVector("lightPosition", lightPosition);*/
        //ssaoShader.Dispatch(LightingKernel, Screen.width / 8, Screen.height / 8, 1);


        Graphics.Blit(renderTarget, destination);
        ReleaseRenderTargets();
    }

    private void ShowArray<T>(T[] array)
    {
        for (var i = 0; i < array.Length; i ++)
        {
            Debug.Log($"{array[i]}");
        }

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
       // position.Release();
       // normal.Release();
       // ssao.Release();
        //blur.Release();
    }

    private void Awake()
    {
   
        noiseBuffer = noise();
        kernelSamples = getKernels();
        Noises = Noise();
        ShowArray(kernelSamples);
       // ShowArray(kernelSamples);


    }

    void Start()
    {
        camera = GetComponent<Camera>();
        camera.depthTextureMode = DepthTextureMode.DepthNormals;
        mat = new Material(Shader.Find("Hidden/SceenDepthNormal"));
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private RenderTexture CreateRenderTexture(RenderTexture source)
    {
        RenderTexture renderTexture = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        Graphics.Blit(source, renderTexture);
        return renderTexture;
    }

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

    private Color32[] Noise()
    {
        Color32[] noiseBuffer = new Color32[16];
        for (int i = 0; i < 16; i++)
        {
            Color32 sample = new Color32(
                (byte)Random.Range(1, 256),
                (byte)Random.Range(1, 256),
                0,
                0);
            noiseBuffer.SetValue(sample, i);

        }
        return noiseBuffer;
    }

    private float GetRandomFloat()
    {
        return Random.Range(0.1f, 1.0f);
    }
}
