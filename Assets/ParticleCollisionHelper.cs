﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.Rendering;

/// <summary>
/// TODO:
/// * Support for trails (http://wiki.unity3d.com/index.php/TrailRendererWith2DCollider)
/// </summary>
public class ParticleCollisionHelper : MonoBehaviour
{
    #region Variables (private)

    private const string TINT_COLOR = "_TintColor";

    public Camera cam;

    [SerializeField]
    private float hitTestAlphaCutoff = 0;

    [SerializeField]
    [ReadOnly]
    private bool isPaused = false;
    [SerializeField]
    [ReadOnly]
    private List<ParticleSystemCollider> particleSystemColliders;

    public RawImage debugImg;

    #endregion


    #region Classes

    private struct SubUVTextureInfo
    {
        public float columns;
        public float rows;
        public float currentColumn;
        public float currentRow;
        public float currentFrame;
        public float totalFrames;

        public SubUVTextureInfo(ParticleSystem.TextureSheetAnimationModule texModule, ParticleSystem.Particle curParticle)
        {
            columns = texModule.numTilesX;
            rows = texModule.numTilesY;
            totalFrames = columns * rows;

            float curParticleLifeNormalized = (curParticle.startLifetime - curParticle.remainingLifetime) / curParticle.startLifetime;

            float startFrame = texModule.startFrame.Evaluate(curParticleLifeNormalized);    // TODO: might be particleSys.time and might need Mathf.Floor()
            float animation = texModule.frameOverTime.Evaluate(curParticleLifeNormalized);

            currentFrame = startFrame + Mathf.Floor(animation * totalFrames);
            currentColumn = currentFrame % columns;
            currentRow = Mathf.Floor(currentFrame / columns);
        }
    }

    [System.Serializable]
    private class ParticleSystemCollider
    {
        #region Variables (private)

        public Camera cam;

        [SerializeField]
        [ReadOnly]
        private ParticleSystem particleSys;
        [SerializeField]
        [ReadOnly]
        private List<ParticleCollider> particleColliders;
        
        private ParticleSystemRenderer particleSystemRenderer;

        #endregion


        #region Properties (public)

        public ParticleSystem ParticleSys { get { return particleSys; } }

        public Bounds Bounds
        {
            get
            {
                return BoundsHelper.GetGameObjectListBounds(particleColliders.Select(x => x.gameObject).ToList(), particleSys.transform.position);
            }
        }

        #endregion


        #region Constructor

        public ParticleSystemCollider(ParticleSystem particleSys)
        {
            this.particleSys = particleSys;

            particleSystemRenderer = particleSys.GetComponent<ParticleSystemRenderer>();
            particleColliders = new List<ParticleCollider>();

            cam = Camera.main;
        }

        #endregion


        #region Methods (public)

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
                        rend.material.SetColor(TINT_COLOR, particleSystemRenderer.material.GetColor(TINT_COLOR));
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
                newParticleCollider.Init(particles[i], particleSys);

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
                ParticleSystem.Particle curParticle = curParticleCollider.Particle;

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

                // Apply texture sheet animation
                ParticleSystem.TextureSheetAnimationModule texModule = particleSys.textureSheetAnimation;
                if (texModule.enabled)
                {
                    SubUVTextureInfo subUV = new SubUVTextureInfo(texModule, curParticle);

                    switch (texModule.animation)
                    {
                        case ParticleSystemAnimationType.WholeSheet:
                            curParticleCollider.gameObject.GetComponent<MeshRenderer>().material.mainTextureScale = new Vector2(1 / subUV.columns, 1 / subUV.rows);
                            curParticleCollider.gameObject.GetComponent<MeshRenderer>().material.mainTextureOffset = new Vector2(subUV.currentColumn / subUV.columns, (subUV.rows - subUV.currentRow - 1) / subUV.rows);

                            break;
                        case ParticleSystemAnimationType.SingleRow:
                            Debug.Log("Single Row texture sheet animations currently not supported.");

                            break;
                        default:
                            Debug.Log("Unsupported texture sheet animation animation type.");

                            break;
                    }
                }

                // Apply color
                Color particleColor = curParticle.GetCurrentColor(particleSys);
                Material mat = curGO.GetComponent<MeshRenderer>().material;
                Color matColor = particleSystemRenderer.material.GetColor(TINT_COLOR);
                curGO.GetComponent<MeshRenderer>().material.SetColor(TINT_COLOR, particleColor * matColor);
            }
        }

        #endregion
    }

    #endregion


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
                curPSC.ParticleSys.Play(true);
                
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
                    Debug.Log("HIT! " + hitParticleCollider.ParticleSys.name, this);
                    return;
                }

                Texture2D tex = rend.material.mainTexture as Texture2D;
                Vector2 pixelUV = hit.textureCoord;

                // Apply texture sheet animation offset
                ParticleSystem.TextureSheetAnimationModule texModule = hitParticleCollider.ParticleSys.textureSheetAnimation;
                if (texModule.enabled)
                {
                    SubUVTextureInfo subUV = new SubUVTextureInfo(texModule, hitParticleCollider.Particle);
                    pixelUV.x = (subUV.currentColumn / subUV.columns) + (pixelUV.x / subUV.columns);
                    pixelUV.y = (subUV.currentRow / subUV.rows) + ((1 - pixelUV.y) / subUV.rows);
                    pixelUV.y = 1 - pixelUV.y;
                }

                pixelUV.x *= tex.width;
                pixelUV.y *= tex.height;

                Color hitColor = tex.GetPixelForced((int)pixelUV.x, (int)pixelUV.y);
                Debug.Log("Raycast hit color: " + hitColor, this);
                if (debugImg != null)
                {
                    debugImg.color = hitColor;
                }
                if (hitColor.a > hitTestAlphaCutoff)
                {
                    Debug.Log("HIT! " + hitParticleCollider.ParticleSys.name, this);
                }
            }
        }
    }

    #endregion
}
