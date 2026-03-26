using AgentAnimation;
using UnityEngine;

namespace GPUAnimation.Runtime
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GPUAnimationPlayer : AnimationPlayerBase
    {
        [Header("Data")]
        public GPUAnimationData animationData;

        [Header("Playback")]
        public float speed = 1f;

        private bool _isPlaying;
        private bool _initialized;

        private int _clipStartFrame;
        private int _clipFrameCount;
        private float _clipFrameRate;
        private float _clipDuration;
        private bool _clipLoop;
        private float _time;

        private bool _isBlending;
        private int _prevClipStartFrame;
        private int _prevClipFrameCount;
        private float _prevClipFrameRate;
        private float _prevClipDuration;
        private bool _prevClipLoop;
        private float _prevTime;

        private float _blendWeight;
        private float _blendDuration;
        private float _blendElapsed;

        private bool _loopCompleted;

        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _mpb;

        private static readonly int AnimFrameProp = Shader.PropertyToID("_AnimFrame");
        private static readonly int AnimFrameNextProp = Shader.PropertyToID("_AnimFrameNext");
        private static readonly int AnimLerpProp = Shader.PropertyToID("_AnimLerp");
        private static readonly int PrevAnimFrameProp = Shader.PropertyToID("_PrevAnimFrame");
        private static readonly int PrevAnimFrameNextProp = Shader.PropertyToID("_PrevAnimFrameNext");
        private static readonly int PrevAnimLerpProp = Shader.PropertyToID("_PrevAnimLerp");
        private static readonly int BlendWeightProp = Shader.PropertyToID("_BlendWeight");

        public override float CurrentClipDuration => _clipDuration;
        public override bool LoopCompleted => _loopCompleted;

        public override void Init()
        {
            if (_initialized)
                return;

            _meshRenderer = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();

            if (animationData == null)
            {
                Debug.LogError($"[GPUAnimationPlayer] animationData is null on '{name}'!");
                return;
            }

            _blendWeight = 1f;
            _initialized = true;
        }

        public override void Tick(float deltaTime)
        {
            if (!_isPlaying)
                return;

            var dt = deltaTime * speed;

            _time += dt;
            _loopCompleted = WrapTime(ref _time, _clipDuration, _clipFrameRate, _clipLoop);

            if (_isBlending)
            {
                _prevTime += dt;
                WrapTime(ref _prevTime, _prevClipDuration, _prevClipFrameRate, _prevClipLoop);

                _blendElapsed += deltaTime;
                _blendWeight = Mathf.Clamp01(_blendElapsed / _blendDuration);

                if (_blendWeight >= 1f)
                    _isBlending = false;
            }

            ComputeFrames(_time, _clipFrameRate, _clipFrameCount, _clipStartFrame, _clipLoop,
                out int frame, out int frameNext, out float lerp);

            _mpb.SetFloat(AnimFrameProp, frame);
            _mpb.SetFloat(AnimFrameNextProp, frameNext);
            _mpb.SetFloat(AnimLerpProp, lerp);

            if (_isBlending)
            {
                ComputeFrames(_prevTime, _prevClipFrameRate, _prevClipFrameCount, _prevClipStartFrame, _prevClipLoop,
                    out int prevFrame, out int prevFrameNext, out float prevLerp);

                _mpb.SetFloat(PrevAnimFrameProp, prevFrame);
                _mpb.SetFloat(PrevAnimFrameNextProp, prevFrameNext);
                _mpb.SetFloat(PrevAnimLerpProp, prevLerp);
                _mpb.SetFloat(BlendWeightProp, _blendWeight);
            }
            else
            {
                _mpb.SetFloat(BlendWeightProp, 1f);
            }

            _meshRenderer.SetPropertyBlock(_mpb);
        }

        private static bool WrapTime(ref float time, float duration, float frameRate, bool loop)
        {
            if (loop)
            {
                if (time >= duration)
                {
                    time %= duration;
                    return true;
                }
            }
            else
            {
                if (time >= duration)
                    time = duration - (1f / frameRate);
            }

            return false;
        }

        private const float LERP_SNAP_THRESHOLD = 0.01f;

        private static void ComputeFrames(float time, float frameRate, int frameCount, int startFrame, bool loop,
            out int absoluteFrame, out int absoluteFrameNext, out float lerp)
        {
            var frameFloat = time * frameRate;
            var frame = (int)frameFloat;
            lerp = frameFloat - frame;

            int frameNext;
            if (loop)
            {
                frame %= frameCount;
                frameNext = (frame + 1) % frameCount;
            }
            else
            {
                if (frame >= frameCount)
                    frame = frameCount - 1;
                
                frameNext = frame + 1;
                
                if (frameNext >= frameCount)
                    frameNext = frameCount - 1;
            }

            if (lerp < LERP_SNAP_THRESHOLD)
            {
                lerp = 0f;
                frameNext = frame;
            }
            else if (lerp > 1f - LERP_SNAP_THRESHOLD)
            {
                frame = frameNext;
                lerp = 0f;
            }

            absoluteFrame = startFrame + frame;
            absoluteFrameNext = startFrame + frameNext;
        }

        private void Play(int clipIndex)
        {
            if (clipIndex < 0 || clipIndex >= animationData.clips.Length)
                return;

            SetCurrentClip(clipIndex);
            _time = 0f;
            _isPlaying = true;
            _isBlending = false;
            _blendWeight = 1f;
        }

        public override void Play(string clipName)
        {
            var index = animationData.FindClipIndex(clipName);
            if (index >= 0)
                Play(index);
        }

        private void Crossfade(int clipIndex, float blendDuration)
        {
            if (clipIndex < 0 || clipIndex >= animationData.clips.Length)
                return;

            if (blendDuration <= 0f || !_isPlaying)
            {
                Play(clipIndex);
                return;
            }

            _prevClipStartFrame = _clipStartFrame;
            _prevClipFrameCount = _clipFrameCount;
            _prevClipFrameRate = _clipFrameRate;
            _prevClipDuration = _clipDuration;
            _prevClipLoop = _clipLoop;
            _prevTime = _time;

            SetCurrentClip(clipIndex);
            _time = 0f;
            _isPlaying = true;

            _isBlending = true;
            _blendDuration = blendDuration;
            _blendElapsed = 0f;
            _blendWeight = 0f;
        }

        public override void Crossfade(string clipName, float blendDuration)
        {
            var index = animationData.FindClipIndex(clipName);
            if (index >= 0)
                Crossfade(index, blendDuration);
        }

        private void PlayWithRandomOffset(int clipIndex)
        {
            Play(clipIndex);
            if (_isPlaying)
                _time = Random.Range(0f, _clipDuration);
        }

        public override void PlayWithRandomOffset(string clipName)
        {
            var index = animationData.FindClipIndex(clipName);
            if (index >= 0)
                PlayWithRandomOffset(index);
        }

        private void SetCurrentClip(int clipIndex)
        {
            var clip = animationData.clips[clipIndex];
            _clipStartFrame = clip.startFrame;
            _clipFrameCount = clip.frameCount;
            _clipFrameRate = clip.frameRate;
            _clipDuration = clip.frameCount / clip.frameRate;
            _clipLoop = clip.loop;
        }
    }
}
