using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainScript : MonoBehaviour
{
    // Final members
    private const int NUM_RETRIES = 60;
    private const float X_RANGE = 38.0f;
    private const float Z_RANGE = 38.0f;

    // Unity editor set variables
    public ComputeShader RayTracerShader;
    private RenderTexture texture;
    public Texture SkyBox;
    public Light Light;

    // Unity editor set slider variables
    [Range(1, 34)]
    public int numSpheres = 2;
    [Range(0.25f, 2.0f)]
    public float sphereRadiusMin = 1.0f;
    [Range(3.0f, 4.0f)]
    public float sphereRadiusMax = 4.0f;

    // Privately set variables
    private Camera mainCam;
    private uint sampleCount = 0;
    private Material AATexture;
    private ComputeBuffer spheres;

    public struct Sphere
    {
        public Vector3 origin;
        public float radius;
        public Vector4 color;
        public Vector4 specular;
    }
    private bool isValid(Sphere s, List<Sphere> created)
    {
        foreach(Sphere e in created)
        {
            if (Mathf.Sqrt(Mathf.Pow(s.origin.x - e.origin.x, 2) + Mathf.Pow(s.origin.z - e.origin.z, 2)) < (s.radius + e.radius))
            {
                return false;
            }
        }
        return true;
    }
    
    /// <summary>
    /// Randomly generate different types of spheres to be passed to the compute shader.
    /// </summary>
    private void GetSpheres()
    {
        List<Sphere> created = new List<Sphere>();
        for (int i = 0; i < numSpheres; i++)
        {
            int numTries = 0;

            // Bounds for origin generation in x-z plane
            float maxx = -sphereRadiusMax + 0.5f * X_RANGE;
            float minx = -0.5f * X_RANGE + sphereRadiusMax;
            float maxz = Z_RANGE - sphereRadiusMax;
            float minz = sphereRadiusMax;

            // Random sphere feature generation
            Sphere sphere;
            sphere.origin = new Vector3(-1.0f, -1.0f, -1.0f);
            sphere.color = new Vector4(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), 1.0f);
            sphere.specular = Random.Range(0.0f,1.0f) > 0.5f ? new Vector4(0.0f,0.0f,0.0f,1.0f) : new Vector4(0.6f, 0.6f, 0.6f, 1.0f);

            // Keep trying to add spheres with random origin values while numTries is not too high.
            while (numTries < NUM_RETRIES)
            {
                sphere.radius = Random.Range(sphereRadiusMin, sphereRadiusMax);
                sphere.origin = new Vector3(Random.Range(minx, maxx), sphere.radius, Random.Range(minz, maxz));
                //Debug.Log(sphere.origin);
                if (isValid(sphere, created))
                {
                    created.Add(sphere);
                    numTries = 0;
                    break;
                }
                numTries++;
            }
        }
        this.spheres = new ComputeBuffer(created.Count, 48);
        this.spheres.SetData(created);
    }

    private void Awake()
    {
        mainCam = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        GetSpheres();
    }

    private void OnDisable()
    {
        if (spheres != null)
        {
            spheres.Release();
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RayTracerShader.SetMatrix("Global", mainCam.cameraToWorldMatrix);
        RayTracerShader.SetMatrix("InverseProjection", mainCam.projectionMatrix.inverse);
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        if (texture == null)
        {
            texture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            texture.enableRandomWrite = true;
            sampleCount = 0;
            texture.Create();
        }

        int kernel = RayTracerShader.FindKernel("CSMain");
        RayTracerShader.SetVector("PixelOffset", new Vector2(Random.value, Random.value));
        Vector4 lightVec = new Vector4(Light.transform.forward.x, Light.transform.forward.y, Light.transform.forward.z, Light.intensity);
        RayTracerShader.SetVector("lightVec", lightVec);
        RayTracerShader.SetBuffer(kernel, "Spheres", spheres);
        RayTracerShader.SetTexture(kernel, "Result", texture);
        RayTracerShader.SetTexture(kernel, "SkyBox", SkyBox);
        RayTracerShader.Dispatch(kernel, Mathf.CeilToInt(Screen.width / 8.0f), Mathf.CeilToInt(Screen.height / 8.0f), 1);

        // Blit the result texture to the screen
        if (AATexture == null)
        {
            AATexture = new Material(Shader.Find("Hidden/AAShader"));
        }
        AATexture.SetFloat("sampleNum", sampleCount);
        Graphics.Blit(texture, destination, AATexture);
        sampleCount++;
    }
    private void Update()
    {
        if (transform.hasChanged || Light.transform.hasChanged)
        {
            sampleCount = 0;
            transform.hasChanged = false;
            Light.transform.hasChanged = false;
        }
    }
}