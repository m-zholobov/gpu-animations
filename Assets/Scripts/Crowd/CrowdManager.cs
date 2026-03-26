using System;
using System.Collections.Generic;
using Pool;
using Vfx;
using UnityEngine;
using Unity.Profiling;

namespace Crowd
{
    public class CrowdManager : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateMarker = new("[AFewDC]CrowdManager.Update");
        
        [SerializeField] private AgentRenderMode _renderMode = AgentRenderMode.GPUInstancing;
        [SerializeField] private VfxSpawnMode _vfxSpawnMode = VfxSpawnMode.Shared;
        [SerializeField] private SpawnController _spawnController;
        [SerializeField] private VfxDispatcher _vfxDispatcher;
        [SerializeField] private GameObject _deathEffectPrefab;
        [SerializeField] private GameObject _stepEffectPrefab;

        [SerializeField] private int _vfxPrewarmCount;

        [SerializeField] private int _maxAgents = 50;
        [SerializeField] private float _spawnInterval = 2f;

        [SerializeField] private float _moveSpeed = 3.5f;
        [SerializeField] private float _angularSpeed = 120f;
        [SerializeField] private float _acceleration = 8f;
        [SerializeField] private float _stoppingDistance = 0.5f;
        [SerializeField] private float _destinationSpread = 3f;

        [SerializeField] private string _runClipName;
        [SerializeField] private string[] _arrivalVariationNames;
        [SerializeField] private string[] _dieClipNames;
        [SerializeField] private float _crossfadeDuration = 0.25f;

        private readonly List<CrowdAgentController> _agents = new();
        private readonly List<CrowdAgentController> _agentsToReturn = new();
        private Transform _agentRoot;
        private ObjectPool<CrowdAgentController> _pool;
        private int _totalSpawned;

        public int ActiveAgentCount => _agents.Count;
        public bool CanSpawn => _agents.Count < _maxAgents;
        public float SpawnInterval => _spawnInterval;
        public AgentRenderMode RenderMode => _renderMode;
        public VfxSpawnMode VfxSpawnMode => _vfxSpawnMode;

        public AgentConfig AgentConfig => new()
        {
            moveSpeed = _moveSpeed,
            angularSpeed = _angularSpeed,
            acceleration = _acceleration,
            stoppingDistance = _stoppingDistance,
            destinationSpread = _destinationSpread,
            runClipName = _runClipName,
            arrivalVariationNames = _arrivalVariationNames,
            dieClipNames = _dieClipNames,
            crossfadeDuration = _crossfadeDuration
        };

        private void Awake()
        {
            _agentRoot = new GameObject("_Agents").transform;
            _agentRoot.SetParent(transform);

            var poolRoot = new GameObject("_Pool").transform;
            poolRoot.SetParent(transform);
            _pool = new ObjectPool<CrowdAgentController>(poolRoot);
        }

        public void PrewarmPrefab(GameObject prefab, int count)
        {
            _pool.Prewarm(prefab, count);
        }

        private void Start()
        {
            _spawnController.Initialize(this);
            PrewarmVfx();
        }

        private void PrewarmVfx()
        {
            if (_vfxDispatcher == null)
                return;

            _vfxDispatcher.Prewarm(_deathEffectPrefab, _vfxSpawnMode, _vfxPrewarmCount);
            _vfxDispatcher.Prewarm(_stepEffectPrefab, _vfxSpawnMode, _vfxPrewarmCount);
        }

        private void Update()
        {
            using (UpdateMarker.Auto())
            {
                var dt = Time.deltaTime;
                _agentsToReturn.Clear();

                foreach (var agent in _agents)
                {
                    agent.Tick(dt);
                    if (agent.IsFinished)
                        _agentsToReturn.Add(agent);
                }

                if (_agentsToReturn.Count > 0)
                {
                    foreach (var agent in _agentsToReturn)
                        ReturnToPool(agent);

                    if (CanSpawn)
                        _spawnController.TrySpawn();
                }
            }
        }

        public void SpawnAgent(GameObject prefab, Vector3 position, Transform destination)
        {
            if (destination == null)
                return;
            
            if (!CanSpawn)
                return;

            var agent = _pool.Get(prefab);
            if (agent == null)
            {
                var container = _pool.GetOrCreateContainer(prefab);
                var go = Instantiate(prefab, container);
                agent = go.GetComponent<CrowdAgentController>();
                if (agent == null)
                    agent = go.AddComponent<CrowdAgentController>();
                agent.SourcePrefab = prefab;
            }

            agent.transform.SetParent(_agentRoot);
            agent.transform.position = position;
            agent.gameObject.SetActive(true);
            agent.gameObject.name = $"{prefab.name}_{_totalSpawned}";
            agent.Init(destination.position, AgentConfig, _vfxDispatcher, _vfxSpawnMode, _deathEffectPrefab, _stepEffectPrefab);
            _agents.Add(agent);
            _totalSpawned++;
        }

        public void KillAgentsInRadius(Vector3 center, float radius)
        {
            var radiusSq = radius * radius;
            foreach (var agent in _agents)
            {
                if (agent.IsFinished)
                    continue;
                
                var distSq = (center - agent.transform.position).sqrMagnitude;
                if (distSq <= radiusSq)
                    agent.ForceKill();
            }
        }

        private void ReturnToPool(CrowdAgentController agent)
        {
            if (!_agents.Remove(agent))
                return;
            
            agent.gameObject.SetActive(false);
            _pool.Return(agent);
        }

        private void ClearAll()
        {
            for (var i = _agents.Count - 1; i >= 0; i--)
            {
                if (_agents[i] != null)
                    Destroy(_agents[i].gameObject);
            }
            _agents.Clear();
            _pool.Clear();
            for (var i = _pool.PoolRoot.childCount - 1; i >= 0; i--)
                Destroy(_pool.PoolRoot.GetChild(i).gameObject);
        }

        private void OnDestroy()
        {
            ClearAll();
        }
    }
}
