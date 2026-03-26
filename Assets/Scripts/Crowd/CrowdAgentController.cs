using AgentAnimation;
using Pool;
using Vfx;
using UnityEngine;
using UnityEngine.AI;

namespace Crowd
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class CrowdAgentController : MonoBehaviour, IPoolable
    {
        private NavMeshAgent _navAgent;
        private Transform _transform;
        private AnimationPlayerBase _animPlayer;
        private VfxDispatcher _vfxDispatcher;
        private VfxSpawnMode _vfxSpawnMode;
        private GameObject _deathEffectPrefab;
        private GameObject _stepEffectPrefab;

        private string[] _variationClips;
        private string[] _dieClips;
        private float _crossfadeDuration;
        private float _reachRadius;
        private float _reachRadiusSq;
        private Vector3 _destinationPosition;
        private bool _arrived;
        private float _sequenceTimeLeft;

        private enum ArrivalState { None, PlayingVariation, PlayingDie, Finished }
        private ArrivalState _arrivalState;

        public GameObject SourcePrefab { get; set; }
        public bool IsFinished => _arrivalState == ArrivalState.Finished;

        public void Init(Vector3 destination, AgentConfig config,
            VfxDispatcher vfxDispatcher = null, VfxSpawnMode vfxSpawnMode = VfxSpawnMode.Shared, GameObject deathEffectPrefab = null,
            GameObject stepEffectPrefab = null)
        {
            _vfxDispatcher = vfxDispatcher;
            _vfxSpawnMode = vfxSpawnMode;
            _deathEffectPrefab = deathEffectPrefab;
            _stepEffectPrefab = stepEffectPrefab;

            _navAgent = GetComponent<NavMeshAgent>();
            _animPlayer = GetComponent<AnimationPlayerBase>();
            _transform = transform;

            _navAgent.enabled = true;
            _navAgent.ResetPath();
            _navAgent.isStopped = false;

            _variationClips = config.arrivalVariationNames != null && config.arrivalVariationNames.Length > 0
                ? config.arrivalVariationNames
                : System.Array.Empty<string>();
            _dieClips = config.dieClipNames != null && config.dieClipNames.Length > 0
                ? config.dieClipNames
                : new[] { "Die01" };
            _crossfadeDuration = config.crossfadeDuration;
            var spread = Mathf.Max(0f, config.destinationSpread);
            var desired = destination;
            if (spread > 0f)
            {
                var disk = Random.insideUnitCircle * spread;
                desired = destination + new Vector3(disk.x, 0f, disk.y);
            }

            var sampleDist = spread + 5f;
            var fallbackSearch = Mathf.Max(spread + 5f, 6f);
            if (!NavMesh.SamplePosition(desired, out var slotHit, sampleDist, NavMesh.AllAreas))
            {
                if (!NavMesh.SamplePosition(destination, out slotHit, fallbackSearch, NavMesh.AllAreas))
                    slotHit.position = destination;
            }

            _destinationPosition = slotHit.position;
            _reachRadius = Mathf.Max(config.stoppingDistance * 2f, config.stoppingDistance + 0.65f);
            _reachRadiusSq = _reachRadius * _reachRadius;

            _navAgent.speed = config.moveSpeed;
            _navAgent.angularSpeed = config.angularSpeed;
            _navAgent.acceleration = config.acceleration;
            _navAgent.stoppingDistance = config.stoppingDistance;
            _navAgent.Warp(_transform.position);

            _animPlayer.Init();
            _animPlayer.PlayWithRandomOffset(config.runClipName);

            _navAgent.SetDestination(_destinationPosition);

            _arrived = false;
            _arrivalState = ArrivalState.None;
        }

        public void Tick(float deltaTime)
        {
            _animPlayer.Tick(deltaTime);

            if (_arrived)
            {
                TickArrivalSequence(deltaTime);
                return;
            }

            if (_animPlayer.LoopCompleted)
                SpawnStepEffect();

            var pos = _transform.position;
            var dx = pos.x - _destinationPosition.x;
            var dy = pos.y - _destinationPosition.y;
            var dz = pos.z - _destinationPosition.z;
            if (dx * dx + dy * dy + dz * dz <= _reachRadiusSq)
                OnArrived();
        }

        public void ForceKill()
        {
            if (_arrivalState == ArrivalState.Finished)
                return;

            if (_navAgent.enabled && _navAgent.isOnNavMesh)
                _navAgent.isStopped = true;
            
            _navAgent.enabled = false;
            _arrived = true;

            _animPlayer.Crossfade(GetRandomDieClip(), _crossfadeDuration);
            SetPlayingDie();
            _sequenceTimeLeft = Mathf.Max(0.5f, _animPlayer.CurrentClipDuration);
        }

        private string GetRandomDieClip() => _dieClips[Random.Range(0, _dieClips.Length)];

        private void SpawnStepEffect()
        {
            _vfxDispatcher.Spawn(_stepEffectPrefab, transform.position + transform.forward * 0.3f, transform.rotation, _vfxSpawnMode);
        }

        private void OnArrived()
        {
            _arrived = true;
            _navAgent.isStopped = true;
            _navAgent.enabled = false;

            if (_variationClips.Length > 0)
            {
                var clip = _variationClips[Random.Range(0, _variationClips.Length)];
                _animPlayer.Crossfade(clip, _crossfadeDuration);
                _arrivalState = ArrivalState.PlayingVariation;
                _sequenceTimeLeft = Mathf.Max(0.5f, _animPlayer.CurrentClipDuration);
            }
            else
            {
                _animPlayer.Crossfade(GetRandomDieClip(), _crossfadeDuration);
                SetPlayingDie();
                _sequenceTimeLeft = Mathf.Max(0.5f, _animPlayer.CurrentClipDuration);
            }
        }

        private void SetPlayingDie()
        {
            _vfxDispatcher.Spawn(_deathEffectPrefab, _transform.position + Vector3.up * 1.5f, _transform.rotation, _vfxSpawnMode);
            _arrivalState = ArrivalState.PlayingDie;
        }

        private void TickArrivalSequence(float deltaTime)
        {
            if (_arrivalState == ArrivalState.Finished)
                return;

            _sequenceTimeLeft -= deltaTime;
            if (_sequenceTimeLeft > 0f)
                return;

            if (_arrivalState == ArrivalState.PlayingVariation)
            {
                _animPlayer.Crossfade(GetRandomDieClip(), _crossfadeDuration);
                SetPlayingDie();
                _sequenceTimeLeft = Mathf.Max(0.5f, _animPlayer.CurrentClipDuration);
                return;
            }

            _arrivalState = ArrivalState.Finished;
        }
    }
}
