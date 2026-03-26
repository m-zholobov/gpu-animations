using UnityEngine;

namespace Vfx
{
    public sealed class VfxEmitterGroup : MonoBehaviour
    {
        [field: SerializeField] public ParticleSystemCustomData DataStream { get; private set; } = ParticleSystemCustomData.Custom2;

        [field: SerializeField] public TagChannel TagSlot { get; private set; } = TagChannel.W;

        public int ParticleCap { get; private set; }
        public VfxEmitterUnit[] Emitters { get; private set; }

        private void Awake()
        {
            Emitters = GetComponentsInChildren<VfxEmitterUnit>();
            foreach (var emitter in Emitters)
                ParticleCap = Mathf.Max(ParticleCap, emitter.particleSystem.main.maxParticles);
        }
    }
}
