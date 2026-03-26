using UnityEngine;

namespace AgentAnimation
{
    public class SkinnedMeshAnimationPlayer : AnimationPlayerBase
    {
        [SerializeField] private Animator _animator;
        
        private float _currentClipDuration;
        private bool _loopCompleted;
        private float _prevNormalizedTime;
        private bool _initialized;
        private bool _playing;

        public override float CurrentClipDuration => _currentClipDuration;
        public override bool LoopCompleted => _loopCompleted;

        public override void Init()
        {
            if (_initialized)
                return;
            
            _initialized = true;
        }

        public override void Tick(float deltaTime)
        {
            _loopCompleted = false;
            
            if (!_playing)
                return;

            var info = _animator.GetCurrentAnimatorStateInfo(0);
            _currentClipDuration = info.length;

            if (info.loop)
            {
                var norm = info.normalizedTime;
                if (_prevNormalizedTime > 0f && Mathf.FloorToInt(norm) > Mathf.FloorToInt(_prevNormalizedTime))
                    _loopCompleted = true;

                _prevNormalizedTime = norm;
            }
        }

        public override void Play(string clipName)
        {
            _animator.Play(clipName, 0, 0f);
            _prevNormalizedTime = 0f;
            _playing = true;
            CacheClipDuration(clipName);
        }

        public override void Crossfade(string clipName, float blendDuration)
        {
            if (blendDuration <= 0f)
            {
                Play(clipName);
                return;
            }

            _animator.CrossFadeInFixedTime(clipName, blendDuration, 0);
            _prevNormalizedTime = 0f;
            _playing = true;
            CacheClipDuration(clipName);
        }

        public override void PlayWithRandomOffset(string clipName)
        {
            float offset = Random.Range(0f, 1f);
            _animator.Play(clipName, 0, offset);
            _prevNormalizedTime = offset;
            _playing = true;
            CacheClipDuration(clipName);
        }

        private void CacheClipDuration(string clipName)
        {
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == clipName)
                {
                    _currentClipDuration = clip.length;
                    return;
                }
            }
        }
    }
}
