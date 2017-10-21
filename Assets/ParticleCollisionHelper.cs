using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ParticleCollisionHelper : MonoBehaviour
{
    public bool isPaused = false;
    public ParticleSystem particleSys;
    public ParticleSystemRenderer particleSystemRenderer;
    public Camera cam;
    public Dictionary<GameObject, ParticleSystem.Particle> gameObjects;
    public ParticleSystem.Particle[] particles;
    public Material material;

    // Use this for initialization
    void Start()
    {
        particleSys = this.GetComponent<ParticleSystem>();
        particleSystemRenderer = particleSys.GetComponent<ParticleSystemRenderer>();
        cam = Camera.main;
        gameObjects = new Dictionary<GameObject, ParticleSystem.Particle>();
    }
    
    // Update is called once per frame
    void Update()
    {
        if (isPaused)
        {
            foreach (KeyValuePair<GameObject, ParticleSystem.Particle> curPair in gameObjects)
            {
                GameObject curGO = curPair.Key;
                ParticleSystem.Particle curParticle = curPair.Value;

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

                gameObjects.Add(go, particles[i]);
            }
        }
    }

    public void Play()
    {
        if (this.gameObject.activeSelf)
        {
            isPaused = false;
            particleSys.Play(true);

            foreach (KeyValuePair<GameObject, ParticleSystem.Particle> curPair in gameObjects)
            {
                GameObject curGO = curPair.Key;
                Destroy(curGO);
            }

            gameObjects.Clear();
        }
    }
}
