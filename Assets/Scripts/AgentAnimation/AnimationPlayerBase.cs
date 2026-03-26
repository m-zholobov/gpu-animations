using UnityEngine;

namespace AgentAnimation
{
    public abstract class AnimationPlayerBase : MonoBehaviour
    {
        public abstract float CurrentClipDuration { get; }
        public abstract bool LoopCompleted { get; }

        public abstract void Init();
        public abstract void Tick(float deltaTime);
        public abstract void Play(string clipName);
        public abstract void Crossfade(string clipName, float blendDuration);
        public abstract void PlayWithRandomOffset(string clipName);
    }
}
