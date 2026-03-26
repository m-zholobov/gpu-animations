using UnityEngine;
using Unity.Profiling;

namespace Crowd
{
    public class SpawnController : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateMarker = new("[AFewDC]SpawnController.Update");
        
        [SerializeField] private GameObject[] _gpuPrefabs;
        [SerializeField] private GameObject[] _skinnedPrefabs;
        [SerializeField] private int _prewarmPerPrefab;
        [SerializeField] private bool _spawnOnStart = true;

        private CrowdManager _manager;
        private SpawnPoint[] _spawnPoints;
        private DestinationPoint[] _destinations;
        private float _timer;
        private bool _initialized;

        private GameObject[] ActivePrefabs => _manager.RenderMode == AgentRenderMode.SkinnedMesh ? _skinnedPrefabs : _gpuPrefabs;

        public void Initialize(CrowdManager manager)
        {
            _manager = manager;
            _spawnPoints = FindObjectsByType<SpawnPoint>();
            _destinations = FindObjectsByType<DestinationPoint>();

            var prefabs = ActivePrefabs;
            if (_prewarmPerPrefab > 0)
            {
                foreach (var prefab in prefabs)
                    _manager.PrewarmPrefab(prefab, _prewarmPerPrefab);
            }

            _timer = _spawnOnStart ? _manager.SpawnInterval : 0f;
            _initialized = true;
        }

        private void Update()
        {
            using (UpdateMarker.Auto())
            {
                if (!_initialized)
                    return;

                _timer += Time.deltaTime;
                var interval = _manager.SpawnInterval;
                if (_timer >= interval)
                {
                    _timer -= interval;
                    TrySpawn();
                }
            }
        }

        public void TrySpawn()
        {
            if (!_manager.CanSpawn)
                return;

            var prefabs = ActivePrefabs;
            if (prefabs == null || prefabs.Length == 0)
                return;

            var spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
            var destination = _destinations[Random.Range(0, _destinations.Length)];
            var prefab = prefabs[Random.Range(0, prefabs.Length)];

            _manager.SpawnAgent(prefab, spawnPoint.transform.position, destination.transform);
        }
    }
}
