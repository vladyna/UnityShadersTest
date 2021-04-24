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
    private RenderTexture ssao;
    private RenderTexture blur;
    private float[] kernelSamples;
    private float[] noiseBuffer;
    private Camera camera;
    private bool isDepth = false;
    // Start is called before the first frame update
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        int deptSsaoKernel = isDepth ? ssaoShader.FindKernel("deptSsao") : ssaoShader.FindKernel("SSAO");
        int BlurKernel = ssaoShader.FindKernel("Blur");
        int LightingKernel = ssaoShader.FindKernel("Lighting");
        renderTarget = CreateRenderTexture(source);
        position = CreateRenderTexture(source);

        SetShadersParameters();

        normal = isDepth ? RenderGeometry(source) : RenderGeometryBasic(source);
        if (isDepth)
        {
            ssaoShader.SetTexture(deptSsaoKernel, "_gPosition", position);
            ssaoShader.SetTexture(deptSsaoKernel, "_gNormal", normal);
            ssaoShader.SetTexture(deptSsaoKernel, "_gDepth", normal);
            ssaoShader.SetTexture(deptSsaoKernel, "_Results", renderTarget);
            ssaoShader.Dispatch(deptSsaoKernel, Screen.width / 8, Screen.height / 8, 1);
            ssao = CreateRenderTexture(renderTarget);
        }
        else
        {
          //  var tex = new RenderTexture();
    
            ssaoShader.SetTexture(deptSsaoKernel, "_gPosition", position);
            ssaoShader.SetTexture(deptSsaoKernel, "_gNormal", normalBasic);
            ssaoShader.SetTexture(deptSsaoKernel, "_Results", renderTarget);
            ssaoShader.Dispatch(deptSsaoKernel, Screen.width / 8, Screen.height / 8, 1);
            ssao = CreateRenderTexture(renderTarget);
        }
        ssaoShader.SetTexture(BlurKernel, "_Results", renderTarget);
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
        ssaoShader.SetVector("lightPosition", lightPosition);
        //ssaoShader.Dispatch(LightingKernel, Screen.width / 8, Screen.height / 8, 1);

        Graphics.Blit(renderTarget, destination);
        ReleaseRenderTargets();
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
        ssaoShader.SetVector("noiseScale", new Vector4(Screen.width / 3, Screen.height / 3, 0, 0));
    }

    private void ReleaseRenderTargets()
    {
        renderTarget.Release();
        position.Release();
        normal.Release();
        ssao.Release();
        blur.Release();
    }

    private RenderTexture RenderGeometryBasic(RenderTexture source)
    {
        int kernel = ssaoShader.FindKernel("GeometryBasic");
        // pass model matrix
        // pass camera info

        // receive the glposition, Normal and albedo
        RenderTexture meshTexture = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        RenderTexture gDepthMap = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        normalBasic = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        albedoBasic = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        positionBasic = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        gDepthMap.enableRandomWrite = true;
        normalBasic.enableRandomWrite = true;
        albedoBasic.enableRandomWrite = true;
        positionBasic.enableRandomWrite = true;
        meshTexture.enableRandomWrite = true;
        var normalTexture = Geometry.GetComponentInChildren<Renderer>().materials[0].GetTexture("_BumpMap");
        // var mesh = Geometry.GetComponentInChildren<Renderer>().materials[0].GetTexture("_BumpMap");
        Graphics.SetRenderTarget(meshTexture);
        var meshes = Geometry.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < meshes.Length; i++)
        {
            var mesh = meshes[i].sharedMesh;
            Graphics.DrawMeshNow(mesh, meshes[i].transform.localToWorldMatrix);
        }
        Graphics.SetRenderTarget(null);
        ssaoShader.SetTexture(kernel, "gPosition", positionBasic);
        ssaoShader.SetMatrix("model", model.transform.localToWorldMatrix);
        ssaoShader.SetTexture(kernel, "gNormal", normalBasic);
        ssaoShader.SetTexture(kernel, "gAlbedo", albedoBasic);
        ssaoShader.SetTexture(kernel, "depthTexture", meshTexture);
        ssaoShader.Dispatch(kernel, Screen.width / 8, Screen.height / 8, 1);
        return meshTexture;

    }

    private RenderTexture RenderGeometry(RenderTexture source)
    {
        int kernel = ssaoShader.FindKernel("Geometry");
        // pass model matrix
        // pass camera info

        // receive the glposition, Normal and albedo
        RenderTexture meshTexture = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        RenderTexture gDepthMap = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        gDepthMap.enableRandomWrite = true;
        meshTexture.enableRandomWrite = true;
        var normalTexture = Geometry.GetComponentInChildren<Renderer>().materials[0].GetTexture("_BumpMap");
       // var mesh = Geometry.GetComponentInChildren<Renderer>().materials[0].GetTexture("_BumpMap");
        Graphics.SetRenderTarget(meshTexture);
        var meshes = Geometry.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < meshes.Length; i++)
        {
            var mesh = meshes[i].sharedMesh;
            Graphics.DrawMeshNow(mesh, meshes[i].transform.localToWorldMatrix);
        }
        Graphics.SetRenderTarget(null);
        ssaoShader.SetTexture(kernel, "Positions", gDepthMap);
        ssaoShader.SetTexture(kernel, "depthTexture", meshTexture);
        ssaoShader.Dispatch(kernel, Screen.width / 8, Screen.height / 8, 1);
        return gDepthMap;

    }

    private void Awake()
    {
        camera = GetComponent<Camera>();
        noiseBuffer = noise();
        kernelSamples = getKernels();
    }

    void Start()
    {
        
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
        float[] kernelBuffer = new float[8 * 8 * 3];
        
        for (int i = 0; i < 64; i++)
        {
            Vector3 sample = new Vector3(
                    (GetRandomFloat() * 2.0f - 1.0f), 
                    (GetRandomFloat() * 2.0f - 1.0f),
                    (GetRandomFloat()));
            sample.Normalize();
            sample = sample * GetRandomFloat();
            float scale = (float)i / 64.0f;
            scale = Mathf.Lerp(0.1f, 1.0f, scale * scale);
            sample = sample * scale;
            int position = i * 3;
            kernelBuffer.SetValue(sample.x, position);
            kernelBuffer.SetValue(sample.y, position + 1);
            kernelBuffer.SetValue(sample.z, position + 2);
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
        return Random.Range(0.0f, 1.0f);
    }
}
