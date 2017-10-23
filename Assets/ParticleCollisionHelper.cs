using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// TODO:
/// * Support for trails (http://wiki.unity3d.com/index.php/TrailRendererWith2DCollider)
/// </summary>
public class ParticleCollisionHelper : MonoBehaviour
{
    public bool isPaused = false;
    public Camera cam;
    public float hitTestAlphaCutoff = 0;
    public List<ParticleSystemCollider> particleSystemColliders;

    public class ParticleSystemCollider
    {
        public ParticleSystem particleSys;
        public ParticleSystemRenderer particleSystemRenderer;
        public List<ParticleCollider> particleColliders;
        public Camera cam;

        public Bounds Bounds
        {
            get
            {
                return BoundsHelper.GetGameObjectListBounds(particleColliders.Select(x => x.gameObject).ToList(), particleSys.transform.position);
            }
        }

        public ParticleSystemCollider(ParticleSystem particleSys)
        {
            this.particleSys = particleSys;

            particleSystemRenderer = particleSys.GetComponent<ParticleSystemRenderer>();
            particleColliders = new List<ParticleCollider>();

            cam = Camera.main;
        }
        
        public void CreateParticleColliders()
        {
            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[particleSys.particleCount];
            int particleCount = particleSys.GetParticles(particles);

            for (int i = 0; i < particleCount; i++)
            {
                GameObject go = Instantiate(Resources.Load<GameObject>("ParticleCollider"));
                go.name = particleSys.name + " collider " + i;
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
                        Debug.LogError("Unsupported render mode " + particleSystemRenderer.renderMode);
                        break;
                }

                ParticleCollider newParticleCollider = go.AddComponent<ParticleCollider>();
                newParticleCollider.particle = particles[i];
                newParticleCollider.particleSys = particleSys;

                particleColliders.Add(newParticleCollider);
            }
        }

        public void ClearParticleColliders()
        {
            foreach (ParticleCollider curParticleCollider in particleColliders)
            {
                GameObject curGO = curParticleCollider.gameObject;
                Destroy(curGO);
            }

            particleColliders.Clear();
        }

        public void ReconstructParticleSystem()
        {
            foreach (ParticleCollider curParticleCollider in particleColliders)
            {
                GameObject curGO = curParticleCollider.gameObject;
                ParticleSystem.Particle curParticle = curParticleCollider.particle;

                float rotationCorrection = 1f;
                Vector3 pivot = particleSystemRenderer.pivot;
                Vector3 size = curParticle.GetCurrentSize3D(particleSys);

                // Get hierarchy scale
                Vector3 transformScale = particleSys.transform.localScale;
                
                // Apply position
                switch (particleSys.main.simulationSpace)
                {
                    case ParticleSystemSimulationSpace.Local:
                        curGO.transform.SetParent(particleSys.gameObject.transform);
                        curGO.transform.localPosition = curParticle.position;

                        pivot = Vector3.Scale(pivot, transformScale);
                        break;
                    case ParticleSystemSimulationSpace.World:
                        curGO.transform.SetParent(null);
                        curGO.transform.position = curParticle.position;

                        size = Vector3.Scale(size, transformScale);
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
                        Debug.LogError("Unsupported render mode " + particleSystemRenderer.renderMode);
                        break;
                }

                // Apply rotation
                curGO.transform.Rotate(new Vector3(curParticle.rotation3D.x, curParticle.rotation3D.y, curParticle.rotation3D.z * rotationCorrection));

                // Apply scale
                curGO.transform.localScale = size;

                // Apply pivot
                pivot = Vector3.Scale(pivot, size);
                curGO.transform.position += (curGO.transform.right * pivot.x);
                curGO.transform.position += (curGO.transform.up * pivot.y);
                curGO.transform.position += (curGO.transform.forward * pivot.z * -1f);
            }
        }
    }


    #region Unity event functions

    // Use this for initialization
    void Start()
    {
        cam = Camera.main;

        particleSystemColliders = new List<ParticleSystemCollider>();
    }
    
    // Update is called once per frame
    void Update()
    {
        if (isPaused && particleSystemColliders.Count > 0)
        {
            foreach (ParticleSystemCollider curPSC in particleSystemColliders)
            {
                curPSC.ReconstructParticleSystem();
            }

            TestSelection();
        }
    }

    void OnDrawGizmos()
    {
        if (isPaused)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Debug.DrawRay(ray.origin, ray.direction.normalized * cam.farClipPlane, Color.green);

            Gizmos.color = Color.green;

            foreach (ParticleSystemCollider curPSC in particleSystemColliders)
            {
                Bounds bounds = curPSC.Bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
    }

    #endregion


    #region Event functions

    public void Pause()
    {
        if (this.gameObject.activeSelf)
        {
            isPaused = true;

            ParticleSystem[] particleSystems = this.GetComponentsInChildren<ParticleSystem>();

            foreach (ParticleSystem curPS in particleSystems)
            {
                curPS.Pause(true);

                ParticleSystemCollider newPSC = new ParticleSystemCollider(curPS);
                particleSystemColliders.Add(newPSC);
                newPSC.CreateParticleColliders();
            }
        }
    }


    public void Play()
    {
        if (this.gameObject.activeSelf)
        {
            isPaused = false;

            foreach (ParticleSystemCollider curPSC in particleSystemColliders)
            {
                curPSC.particleSys.Play(true);
                
                curPSC.ClearParticleColliders();
            }

            particleSystemColliders.Clear();
        }
    }

    #endregion


    #region Methods (private)

    private void TestSelection()
    {
        // Selection (accounts for transparency)
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit))
                return;

            ParticleCollider hitParticleCollider = hit.transform.gameObject.GetComponent<ParticleCollider>();

            if (hitParticleCollider != null)
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
