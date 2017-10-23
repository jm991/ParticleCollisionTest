using UnityEngine;

public class ParticleCollider : MonoBehaviour
{
    #region Variables (private)

    [SerializeField]
    [ReadOnly]
    private ParticleSystem.Particle particle;
    [SerializeField]
    [ReadOnly]
    private ParticleSystem particleSys;

    #endregion


    #region Properties (public)

    public ParticleSystem.Particle Particle { get { return particle; } }
    public ParticleSystem ParticleSys { get { return particleSys; } }

    #endregion


    #region Methods (public)

    public void Init(ParticleSystem.Particle particle, ParticleSystem particleSys)
    {
        this.particle = particle;
        this.particleSys = particleSys;
    }

    #endregion
}