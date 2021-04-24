using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SSAO_Compute : MonoBehaviour
{
    public ComputeShader ssaoShader;
    public float Radius = 1.0f;
    public float Bias = 0.002f;
    public int kernelSize = 64;
    public Texture NormalMap;
    private RenderTexture renderTarget;
    private float[] kernelSamples;
    private float[] noiseBuffer;
    public GameObject Geometry;
    private Camera camera;
    // Start is called before the first frame update
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        int kernel = ssaoShader.FindKernel("deptSsao");
        renderTarget = CreateRenderTexture(source);
        RenderTexture pos = CreateRenderTexture(source);
     //   RenderTexture normal = CreateRenderTexture(source);
        RenderTexture gTex = new RenderTexture(4, 4, 0);

        SetShadersParameters();

        RenderTexture gDepthMap = RenderGeometry(source);
        ssaoShader.SetTexture(kernel, "_gPosition", pos);
        ssaoShader.SetTexture(kernel, "_gNormal", gDepthMap);
        ssaoShader.SetTexture(kernel, "_gDepth", gDepthMap);
        ssaoShader.SetTexture(kernel, "_Results", renderTarget);
        ssaoShader.Dispatch(kernel, Screen.width / 8, Screen.height / 8, 1);

        Graphics.Blit(renderTarget, destination);
    }

    private void SetShadersParameters()
    {
        ssaoShader.SetFloat("gAspectRatio", camera.aspect);
        ssaoShader.SetFloat("gTanHalfFOV", Mathf.Tan( camera.fieldOfView / 2.0f * Mathf.Deg2Rad));
        ssaoShader.SetFloat("radius", Radius);
        ssaoShader.SetMatrix("projection", camera.projectionMatrix);
        ssaoShader.SetMatrix("projection", camera.projectionMatrix);
        ssaoShader.SetFloat("bias", Bias);
        ssaoShader.SetFloats("samples", kernelSamples);
        ssaoShader.SetInt("kernelSize", kernelSize);
        ssaoShader.SetFloats("texNoise", noiseBuffer);
        ssaoShader.SetMatrix("view", camera.worldToCameraMatrix);
        ssaoShader.SetVector("noiseScale", new Vector4(Screen.width / 3, Screen.height / 3, 0, 0));
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
        var meshes =  Geometry.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < meshes.Length; i++)
        {
            var mesh = meshes[i].sharedMesh;
            Graphics.DrawMeshNow(mesh, meshes[i].transform.localToWorldMatrix);
        }
        Graphics.SetRenderTarget(null);
        ssaoShader.SetTexture(kernel, "Positions", gDepthMap);
        ssaoShader.SetTexture(kernel, "depthTexture", meshTexture);
        ssaoShader.Dispatch(kernel, Screen.width / 8, Screen.height / 8, 1);
        return meshTexture;

    }

    private void Awake()
    {
        camera = GetComponent<Camera>();
        float[] noisebuffer = noise();
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
