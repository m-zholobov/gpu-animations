using Pool;
using UnityEngine;
using Unity.Profiling;

namespace Bombing
{
    public class BombController : MonoBehaviour, IPoolable
    {
        private static readonly ProfilerMarker TickMarker = new("[AFewDC]BombController.Tick");
        
        private float _targetY;
        private float _fallSpeed;
        private Vector3 _targetPoint;

        private enum State { Idle, Falling, Landed }
        private State _state = State.Idle;

        public GameObject SourcePrefab { get; set; }
        public bool IsLanded => _state == State.Landed;
        public Vector3 TargetPoint => _targetPoint;

        public void Init(Vector3 targetPoint, BombConfig config)
        {
            _targetPoint = targetPoint;
            _targetY = targetPoint.y;
            _fallSpeed = config.fallSpeed;
            _state = State.Falling;

            transform.position = targetPoint + Vector3.up * config.spawnHeight;
            transform.rotation = Random.rotation;
        }

        public void Tick(float deltaTime)
        {
            using (TickMarker.Auto())
            {
                if (_state != State.Falling)
                    return;

                Vector3 pos = transform.position;
                pos.y -= _fallSpeed * deltaTime;

                if (pos.y <= _targetY)
                {
                    pos.y = _targetY;
                    _state = State.Landed;
                }

                transform.position = pos;
            }
        }
    }
}
