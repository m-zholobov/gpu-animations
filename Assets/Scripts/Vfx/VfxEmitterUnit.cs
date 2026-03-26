using UnityEngine;

namespace Vfx
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class VfxEmitterUnit : MonoBehaviour
    {
        public new ParticleSystem particleSystem;
        public int burstCount = 1;
        public float lifetimeOverride;

        private void Reset()
        {
            particleSystem = GetComponent<ParticleSystem>();
        }
    }
}
