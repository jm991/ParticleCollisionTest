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
    public bool isPaused = false;
    public Camera cam;
    public float hitTestAlphaCutoff = 0;

    // TODO: put into a class to create per particle system
    public ParticleSystem particleSys;
    public ParticleSystemRenderer particleSystemRenderer;
    public List<ParticleCollider> particleColliders;

    #region Unity event functions

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
            ReconstructParticleSystem();

            TestSelection();
        }
    }

    void OnDrawGizmos()
    {
        if (isPaused)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Debug.DrawRay(ray.origin, ray.direction.normalized * cam.farClipPlane, Color.green);

            /// Draw bounds based on <see cref="particleColliders"/> 
            Gizmos.color = Color.green;
            Bounds bounds = BoundsHelper.GetGameObjectListBounds(particleColliders.Select(x => x.gameObject).ToList(), this.transform.position);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }

    #endregion


    #region Event functions

    public void Pause()
    {
        if (this.gameObject.activeSelf)
        {
            isPaused = true;
            particleSys.Pause(true);

            CreateParticleColliders();
        }
    }


    public void Play()
    {
        if (this.gameObject.activeSelf)
        {
            isPaused = false;
            particleSys.Play(true);

            ClearParticleColliders();
        }
    }

    #endregion


    #region Methods (private)

    private void CreateParticleColliders()
    {
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[particleSys.particleCount];
        int particleCount = particleSys.GetParticles(particles);

        for (int i = 0; i < particleCount; i++)
        {
            GameObject go = Instantiate(Resources.Load<GameObject>("ParticleCollider"));
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

    private void ClearParticleColliders()
    {
        foreach (ParticleCollider curParticleCollider in particleColliders)
        {
            GameObject curGO = curParticleCollider.gameObject;
            Destroy(curGO);
        }

        particleColliders.Clear();
    }

    private void ReconstructParticleSystem()
    {
        foreach (ParticleCollider curParticleCollider in particleColliders)
        {
            GameObject curGO = curParticleCollider.gameObject;
            ParticleSystem.Particle curParticle = curParticleCollider.particle;

            float rotationCorrection = 1f;
            Vector3 pivot = particleSystemRenderer.pivot;
            Vector3 size = curParticle.GetCurrentSize3D(particleSys);

            // Get hierarchy scale
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
    }

    private void TestSelection()
    {
        // Selection (accounts for transparency)
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit))
                return;

            ParticleCollider hitParticleCollider = hit.transform.gameObject.GetComponent<ParticleCollider>();

            if (hitParticleCollider != null && hitParticleCollider.particleSys == particleSys)
            {
                Renderer rend = hit.transform.GetComponent<Renderer>();
                MeshCollider meshCollider = hit.collider as MeshCollider;
                meshCollider.convex = false;

                if (rend == null || rend.sharedMaterial == null || rend.sharedMaterial.mainTexture == null || meshCollider == null)
                {
                    Debug.Log("HIT! " + hitParticleCollider.particleSys.name, this);
                    return;
                }

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

    #endregion
}
