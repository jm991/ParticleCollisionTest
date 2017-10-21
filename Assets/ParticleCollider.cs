using UnityEngine;

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