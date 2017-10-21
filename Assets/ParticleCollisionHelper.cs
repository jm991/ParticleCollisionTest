using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// TODO:
/// * Support for trails (http://wiki.unity3d.com/index.php/TrailRendererWith2DCollider)
/// * Support for nested particle systems
/// </summary>
public class ParticleCollisionHelper : MonoBehaviour
{
    public class ParticleCollider : MonoBehaviour
    {
        public ParticleSystem.Particle particle;
        public ParticleSystem particleSys;

        public ParticleCollider(ParticleSystem.Particle particle, ParticleSystem particleSys)
        {
            this.particle = particle;
            this.particleSys = particleSys;
        }
    }

    public bool isPaused = false;
    public ParticleSystem particleSys;
    public ParticleSystemRenderer particleSystemRenderer;
    public Camera cam;
    public List<ParticleCollider> particleColliders;
    public ParticleSystem.Particle[] particles;
    public Material material;
    public float hitTestAlphaCutoff = 0;

    // Use this for initialization
    void Start()
    {
        particleSys = this.GetComponent<ParticleSystem>();
        particleSystemRenderer = particleSys.GetComponent<ParticleSystemRenderer>();
        cam = Camera.main;
        particleColliders = new List<ParticleCollider>();
    }
    
    // Update is called once per frame
    void Update()
    {
        if (isPaused)
        {
            foreach (ParticleCollider curParticleCollider in particleColliders)
            {
                GameObject curGO = curParticleCollider.gameObject;
                ParticleSystem.Particle curParticle = curParticleCollider.particle;

                float rotationCorrection = 1f;
                Vector3 pivot = particleSystemRenderer.pivot;
                Vector3 size = curParticle.GetCurrentSize3D(particleSys);

                Transform curParent = this.transform;
                float uniformScale = 1f;
                while (curParent != null)
                {
                    uniformScale *= curParent.localScale.x;
                    curParent = curParent.parent;
                }

                // Apply position
                switch (particleSys.main.simulationSpace)
                {
                    case ParticleSystemSimulationSpace.Local:
                        curGO.transform.SetParent(particleSys.gameObject.transform);
                        curGO.transform.localPosition = curParticle.position;

                        pivot *= uniformScale;
                        break;
                    case ParticleSystemSimulationSpace.World:
                        curGO.transform.SetParent(null);
                        curGO.transform.position = curParticle.position;

                        size *= uniformScale;
                        break;
                }

                switch (particleSystemRenderer.renderMode)
                {
                    case ParticleSystemRenderMode.Billboard:
                        // Billboard to camera
                        curGO.transform.LookAt(curGO.transform.position + cam.transform.rotation * Vector3.forward, cam.transform.rotation * Vector3.up);

                        rotationCorrection = -1f;
                        break;
                    case ParticleSystemRenderMode.Mesh:
                        curGO.transform.rotation = Quaternion.identity;

                        // For mesh pivots, Z is Y and Y is Z
                        pivot.z = particleSystemRenderer.pivot.y * -1f;
                        pivot.y = particleSystemRenderer.pivot.z * -1f;

                        pivot *= curParticle.GetCurrentSize(particleSys);
                        break;
                    default:
                        Debug.LogError("Unsupported render mode.", this);
                        break;
                }
                
                // Apply rotation
                curGO.transform.Rotate(new Vector3(curParticle.rotation3D.x, curParticle.rotation3D.y, curParticle.rotation3D.z * rotationCorrection));

                // Apply scale
                curGO.transform.localScale = size;

                // Apply pivot
                pivot = new Vector3(pivot.x * size.x, pivot.y * size.y, pivot.z * size.z);
                curGO.transform.position += (curGO.transform.right * pivot.x);
                curGO.transform.position += (curGO.transform.up * pivot.y);
                curGO.transform.position += (curGO.transform.forward * pivot.z * -1f);
            }
            

            // Selection (accounts for transparency)
            if (Input.GetMouseButton(0))
            {
                RaycastHit hit;
                if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit))
                    return;
                
                ParticleCollider hitParticleCollider = hit.transform.gameObject.GetComponent<ParticleCollider>();

                if (hitParticleCollider != null && hitParticleCollider.particleSys == particleSys)
                {
                    Renderer rend = hit.transform.GetComponent<Renderer>();
                    MeshCollider meshCollider = hit.collider as MeshCollider;

                    if (rend == null || rend.sharedMaterial == null || rend.sharedMaterial.mainTexture == null || meshCollider == null)
                        return;

                    Texture2D tex = rend.material.mainTexture as Texture2D;
                    Vector2 pixelUV = hit.textureCoord;
                    pixelUV.x *= tex.width;
                    pixelUV.y *= tex.height;

                    Color hitColor = tex.GetPixelForced((int)pixelUV.x, (int)pixelUV.y);
                    Debug.Log("Raycast hit color: " + hitColor, this);
                    if (hitColor.a > hitTestAlphaCutoff)
                    {
                        Debug.Log("HIT! " + hitParticleCollider.particleSys.name, this);
                    }
                }
            }
        }
    }

    public void Pause()
    {
        if (this.gameObject.activeSelf)
        {
            isPaused = true;
            particleSys.Pause(true);

            particles = new ParticleSystem.Particle[particleSys.particleCount];
            int particleCount = particleSys.GetParticles(particles);

            for (int i = 0; i < particleCount; i++)
            {
                GameObject go = Instantiate(Resources.Load<GameObject>("Quad"));
                MeshFilter meshFilter = go.GetComponent<MeshFilter>();
                MeshCollider meshCollider = go.GetComponent<MeshCollider>();
                Renderer rend = go.GetComponent<Renderer>();
                rend.material = particleSystemRenderer.material;

                switch (particleSystemRenderer.renderMode)
                {
                    case ParticleSystemRenderMode.Billboard:
                        break;
                    case ParticleSystemRenderMode.Mesh:
                        meshFilter.mesh = particleSystemRenderer.mesh;
                        meshCollider.sharedMesh = particleSystemRenderer.mesh;
                        break;
                    default:
                        Debug.LogError("Unsupported render mode.", this);
                        break;
                }

                ParticleCollider newParticleCollider = go.AddComponent<ParticleCollider>();
                newParticleCollider.particle = particles[i];
                newParticleCollider.particleSys = particleSys;

                particleColliders.Add(newParticleCollider);
            }
        }
    }

    public void Play()
    {
        if (this.gameObject.activeSelf)
        {
            isPaused = false;
            particleSys.Play(true);

            foreach (ParticleCollider curParticleCollider in particleColliders)
            {
                GameObject curGO = curParticleCollider.gameObject;
                Destroy(curGO);
            }

            particleColliders.Clear();
        }
    }

    void OnDrawGizmos()
    {
        if (isPaused)
        {
            /// Draw bounds based on <see cref="particleColliders"/> 
            Gizmos.color = Color.green;
            Bounds bounds = BoundsHelper.GetGameObjectListBounds(particleColliders.Select(x => x.gameObject).ToList());
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}


public static class Extensions
{
    /// <summary>
    /// From this Unity support article:
    /// https://support.unity3d.com/hc/en-us/articles/206486626-How-can-I-get-pixels-from-unreadable-textures-
    /// </summary>
    /// <param name="texture"></param>
    public static Color GetPixelForced(this Texture2D texture, int x, int y)
    {
        // Create a temporary RenderTexture of the same size as the texture
        RenderTexture tmp = RenderTexture.GetTemporary(
                            texture.width,
                            texture.height,
                            0,
                            RenderTextureFormat.Default,
                            RenderTextureReadWrite.Linear);

        // Blit the pixels on texture to the RenderTexture
        Graphics.Blit(texture, tmp);

        // Backup the currently set RenderTexture
        RenderTexture previous = RenderTexture.active;

        // Set the current RenderTexture to the temporary one we created
        RenderTexture.active = tmp;

        // Create a new readable Texture2D to copy the pixels to it
        Texture2D myTexture2D = new Texture2D(texture.width, texture.height);

        // Copy the pixels from the RenderTexture to the new Texture
        myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
        myTexture2D.Apply();

        // Reset the active RenderTexture
        RenderTexture.active = previous;

        // Release the temporary RenderTexture
        RenderTexture.ReleaseTemporary(tmp);

        // "myTexture2D" now has the same pixels from "texture" and it's readable.
        return myTexture2D.GetPixel(x, y);
    }
}
