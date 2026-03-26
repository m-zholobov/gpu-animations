using System;

namespace Crowd
{
    [Serializable]
    public struct AgentConfig
    {
        public float moveSpeed;
        public float angularSpeed;
        public float acceleration;
        public float stoppingDistance;
        public float destinationSpread;
        public string runClipName;
        public string[] arrivalVariationNames;
        public string[] dieClipNames;
        public float crossfadeDuration;
    }
}
