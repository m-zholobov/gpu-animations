using System.Collections.Generic;
using Crowd;
using Pool;
using Vfx;
using UnityEngine;
using Unity.Profiling;

namespace Bombing
{
    public class BombingManager : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateMarker = new("[AFewDC]BombingManager.Update");
        
        [SerializeField] private VfxDispatcher _vfxDispatcher;
        [SerializeField] private CrowdManager _crowdManager;
        [SerializeField] private GameObject _bombPrefab;
        [SerializeField] private GameObject _explosionEffectPrefab;
        [SerializeField] private BombConfig _config;
        [SerializeField] private int _maxActiveBombs = 3;
        [SerializeField] private int _prewarmCount;

        private readonly List<BombController> _activeBombs = new();
        private readonly List<BombController> _landedBombsBuffer = new();
        private ObjectPool<BombController> _pool;
        private Transform _bombRoot;

        private void Awake()
        {
            _bombRoot = new GameObject("_Bombs").transform;
            _bombRoot.SetParent(transform);

            Transform poolRoot = new GameObject("_BombPool").transform;
            poolRoot.SetParent(transform);
            _pool = new ObjectPool<BombController>(poolRoot);

            if (_prewarmCount > 0)
                _pool.Prewarm(_bombPrefab, _prewarmCount);
        }

        private void Start()
        {
            _vfxDispatcher.Prewarm(_explosionEffectPrefab, _crowdManager.VfxSpawnMode, _prewarmCount);
        }

        public void RequestBomb(Vector3 hitPoint)
        {
            if (_activeBombs.Count >= _maxActiveBombs)
                return;

            BombController bomb = _pool.Get(_bombPrefab);
            if (bomb == null)
            {
                Transform container = _pool.GetOrCreateContainer(_bombPrefab);
                GameObject go = Instantiate(_bombPrefab, container);
                bomb = go.GetComponent<BombController>();
                if (bomb == null)
                    bomb = go.AddComponent<BombController>();
                bomb.SourcePrefab = _bombPrefab;
            }

            bomb.transform.SetParent(_bombRoot);
            bomb.gameObject.SetActive(true);
            bomb.Init(hitPoint + Vector3.up, _config);
            _activeBombs.Add(bomb);
        }

        private void Update()
        {
            using (UpdateMarker.Auto())
            {
                var dt = Time.deltaTime;
                _landedBombsBuffer.Clear();

                foreach (var bomb in _activeBombs)
                {
                    bomb.Tick(dt);
                    if (bomb.IsLanded)
                        _landedBombsBuffer.Add(bomb);
                }

                if (_landedBombsBuffer.Count == 0)
                    return;

                foreach (var bombController in _landedBombsBuffer)
                    OnBombLanded(bombController);
            }
        }

        private void OnBombLanded(BombController bomb)
        {
            _activeBombs.Remove(bomb);

            _vfxDispatcher.Spawn(_explosionEffectPrefab, bomb.TargetPoint + Vector3.up * 1.5f, Quaternion.identity);
            _crowdManager.KillAgentsInRadius(bomb.TargetPoint, _config.explosionRadius);

            bomb.gameObject.SetActive(false);
            _pool.Return(bomb);
        }

        private void OnDestroy()
        {
            for (int i = _activeBombs.Count - 1; i >= 0; i--)
            {
                if (_activeBombs[i] != null)
                    Destroy(_activeBombs[i].gameObject);
            }
            _activeBombs.Clear();
            _pool.Clear();

            if (_pool.PoolRoot != null)
            {
                for (int i = _pool.PoolRoot.childCount - 1; i >= 0; i--)
                    Destroy(_pool.PoolRoot.GetChild(i).gameObject);
            }
        }
    }
}
