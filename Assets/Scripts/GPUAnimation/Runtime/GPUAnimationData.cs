using UnityEngine;

namespace GPUAnimation.Runtime
{
    [System.Serializable]
    public struct GPUAnimClipInfo
    {
        public string clipName;
        public int startFrame;
        public int frameCount;
        public float frameRate;
        public bool loop;
    }

    [CreateAssetMenu(fileName = "GPUAnimationData", menuName = "GPU Animation/Animation Data")]
    public class GPUAnimationData : ScriptableObject
    {
        public GPUAnimClipInfo[] clips;

        public int FindClipIndex(string clipName)
        {
            if (clips == null)
                return -1;
            
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].clipName == clipName)
                    return i;
            }
            
            return -1;
        }
    }
}
