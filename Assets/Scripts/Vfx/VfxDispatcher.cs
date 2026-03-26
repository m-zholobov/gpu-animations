using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace Vfx
{
    public class VfxDispatcher : MonoBehaviour
    {
        private static readonly ProfilerMarker UpdateMarker = new("[AFewDC]VfxDispatcher.Update");
        
        private const float FallbackLifetime = 10f;

        private readonly Dictionary<GameObject, VfxEmitterGroup> _sharedGroups = new();
        private readonly List<LiveSharedEmission> _liveSharedEmissions = new();
        private readonly List<Vector4> _tagDataBuffer = new();

        private readonly Dictionary<GameObject, Queue<PooledInstance>> _prefabPools = new();
        private readonly List<LivePooledEmission> _livePooledEmissions = new();

        private Transform _root;
        private Transform _poolRoot;
        private ParticleSystem.Particle[] _particleBuffer;
        private uint _nextId = 1;

        private struct LiveSharedEmission
        {
            public uint id;
            public VfxEmitterGroup group;
            public float expirationTime;
        }

        private struct PooledInstance
        {
            public GameObject gameObject;
            public ParticleSystem[] particleSystems;
            public GameObject sourcePrefab;
        }

        private struct LivePooledEmission
        {
            public uint id;
            public PooledInstance instance;
            public float expirationTime;
        }

        private void Awake()
        {
            _root = new GameObject("_SharedVfx").transform;
            _root.SetParent(transform);

            _poolRoot = new GameObject("_PooledVfx").transform;
            _poolRoot.SetParent(transform);
        }

        public void Prewarm(GameObject prefab, VfxSpawnMode mode, int count = 0)
        {
            switch (mode)
            {
                case VfxSpawnMode.Shared:
                    AcquireGroup(prefab);
                    break;

                case VfxSpawnMode.Pooled:
                    if (count <= 0)
                        return;
                    PrewarmPooled(prefab, count);
                    break;
            }
        }

        private void PrewarmPooled(GameObject prefab, int count)
        {
            if (!_prefabPools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<PooledInstance>();
                _prefabPools[prefab] = queue;
            }

            for (var i = 0; i < count; i++)
            {
                var go = Instantiate(prefab, _poolRoot);
                go.name = prefab.name;
                go.SetActive(false);

                queue.Enqueue(new PooledInstance
                {
                    gameObject = go,
                    particleSystems = go.GetComponentsInChildren<ParticleSystem>(true),
                    sourcePrefab = prefab
                });
            }
        }

        public VfxEmissionToken Spawn(
            GameObject vfxPrefab,
            Vector3 position,
            Quaternion rotation,
            VfxSpawnMode mode = VfxSpawnMode.Shared)
        {
            if (vfxPrefab == null)
                return default;

            return mode switch
            {
                VfxSpawnMode.Shared => SpawnShared(vfxPrefab, position, rotation),
                VfxSpawnMode.Pooled => SpawnPooled(vfxPrefab, position, rotation),
                _ => default
            };
        }

        public void Stop(VfxEmissionToken token)
        {
            if (!token.IsValid)
                return;

            for (var i = _liveSharedEmissions.Count - 1; i >= 0; i--)
            {
                if (_liveSharedEmissions[i].id != token.Id)
                    continue;
                DiscardTaggedParticles(_liveSharedEmissions[i].group, token.Id);
                _liveSharedEmissions.RemoveAt(i);
                return;
            }

            for (var i = _livePooledEmissions.Count - 1; i >= 0; i--)
            {
                if (_livePooledEmissions[i].id != token.Id)
                    continue;
                ReturnPooledInstance(_livePooledEmissions[i].instance);
                _livePooledEmissions.RemoveAt(i);
                return;
            }
        }

        private void Update()
        {
            using (UpdateMarker.Auto())
            {
                var time = Time.time;

                for (var i = _liveSharedEmissions.Count - 1; i >= 0; i--)
                {
                    if (time < _liveSharedEmissions[i].expirationTime)
                        continue;

                    DiscardTaggedParticles(_liveSharedEmissions[i].group, _liveSharedEmissions[i].id);
                    _liveSharedEmissions.RemoveAt(i);
                }

                for (var i = _livePooledEmissions.Count - 1; i >= 0; i--)
                {
                    if (time < _livePooledEmissions[i].expirationTime)
                        continue;

                    ReturnPooledInstance(_livePooledEmissions[i].instance);
                    _livePooledEmissions.RemoveAt(i);
                }
            }
        }


        private VfxEmissionToken SpawnShared(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var group = AcquireGroup(prefab);
            var id = _nextId++;

            var maxLifetime = 0f;

            foreach (var emitter in group.Emitters)
            {
                InjectParticles(emitter, group, id, position, rotation);
                maxLifetime = Mathf.Max(maxLifetime, emitter.lifetimeOverride);
            }

            _liveSharedEmissions.Add(new LiveSharedEmission
            {
                id = id,
                group = group,
                expirationTime = Time.time + (maxLifetime > 0 ? maxLifetime : FallbackLifetime)
            });

            return new VfxEmissionToken(id);
        }

        private VfxEmitterGroup AcquireGroup(GameObject prefab)
        {
            if (_sharedGroups.TryGetValue(prefab, out var existing))
                return existing;

            var instance = Instantiate(prefab, _root);
            instance.name = prefab.name;

            var group = instance.GetComponent<VfxEmitterGroup>();

            foreach (var emitter in group.Emitters)
            {
                var ps = emitter.particleSystem;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

                var emission = ps.emission;
                emission.rateOverTime = 0;
                emission.rateOverDistance = 0;
                emission.burstCount = 0;

                ps.Play(false);
            }

            _sharedGroups[prefab] = group;
            GrowBuffer(group.ParticleCap);
            
            return group;
        }

        private void InjectParticles(
            VfxEmitterUnit emitter,
            VfxEmitterGroup group,
            uint id,
            Vector3 position,
            Quaternion rotation)
        {
            var ps = emitter.particleSystem;
            var countBefore = ps.particleCount;

            var emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                rotation3D = rotation.eulerAngles,
                applyShapeToPosition = true
            };
            ps.Emit(emitParams, emitter.burstCount);

            var countAfter = ps.particleCount;
            if (countAfter <= countBefore)
                return;

            ps.GetCustomParticleData(_tagDataBuffer, group.DataStream);
            var tagVector = group.TagSlot.WriteTagVector(id);
            for (int i = countBefore; i < countAfter; i++)
                _tagDataBuffer[i] = tagVector;
            ps.SetCustomParticleData(_tagDataBuffer, group.DataStream);
        }

        private void DiscardTaggedParticles(VfxEmitterGroup group, uint id)
        {
            foreach (var emitter in group.Emitters)
            {
                var ps = emitter.particleSystem;
                var count = ps.GetParticles(GrowBuffer(ps.particleCount));
                if (count == 0)
                    continue;

                ps.GetCustomParticleData(_tagDataBuffer, group.DataStream);

                var changed = false;
                for (var i = 0; i < count; i++)
                {
                    if (group.TagSlot.ReadTag(_tagDataBuffer[i]) != id)
                        continue;
                    _particleBuffer[i].remainingLifetime = -1f;
                    changed = true;
                }

                if (changed)
                    ps.SetParticles(_particleBuffer, count);
            }
        }

        private ParticleSystem.Particle[] GrowBuffer(int capacity)
        {
            if (_particleBuffer == null || _particleBuffer.Length < capacity)
                _particleBuffer = new ParticleSystem.Particle[Mathf.Max(capacity, 256)];
            return _particleBuffer;
        }


        private VfxEmissionToken SpawnPooled(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var instance = AcquirePooledInstance(prefab);
            var id = _nextId++;

            var t = instance.gameObject.transform;
            t.SetParent(_root);
            t.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);

            var maxLifetime = 0f;
            foreach (var ps in instance.particleSystems)
            {
                ps.Clear(true);
                ps.Play(true);

                var main = ps.main;
                var lt = main.duration + main.startLifetime.constantMax;
                maxLifetime = Mathf.Max(maxLifetime, lt);
            }

            if (maxLifetime <= 0f)
                maxLifetime = FallbackLifetime;

            _livePooledEmissions.Add(new LivePooledEmission
            {
                id = id,
                instance = instance,
                expirationTime = Time.time + maxLifetime
            });

            return new VfxEmissionToken(id);
        }

        private PooledInstance AcquirePooledInstance(GameObject prefab)
        {
            if (_prefabPools.TryGetValue(prefab, out var queue) && queue.Count > 0)
                return queue.Dequeue();

            var go = Instantiate(prefab, _poolRoot);
            go.name = prefab.name;
            go.SetActive(false);

            return new PooledInstance
            {
                gameObject = go,
                particleSystems = go.GetComponentsInChildren<ParticleSystem>(true),
                sourcePrefab = prefab
            };
        }

        private void ReturnPooledInstance(PooledInstance instance)
        {
            foreach (var ps in instance.particleSystems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                ps.Clear(true);
            }

            instance.gameObject.SetActive(false);
            instance.gameObject.transform.SetParent(_poolRoot);

            if (!_prefabPools.TryGetValue(instance.sourcePrefab, out var queue))
            {
                queue = new Queue<PooledInstance>();
                _prefabPools[instance.sourcePrefab] = queue;
            }

            queue.Enqueue(instance);
        }

        public void ClearAll()
        {
            foreach (var kvp in _sharedGroups)
            {
                if (kvp.Value == null)
                    continue;
                foreach (var ps in kvp.Value.GetComponentsInChildren<ParticleSystem>())
                    ps.Clear();
            }

            _liveSharedEmissions.Clear();

            for (var i = _livePooledEmissions.Count - 1; i >= 0; i--)
                ReturnPooledInstance(_livePooledEmissions[i].instance);
            
            _livePooledEmissions.Clear();
        }

        private void OnDestroy()
        {
            foreach (var kvp in _sharedGroups)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value.gameObject);
            }

            _sharedGroups.Clear();
            _liveSharedEmissions.Clear();

            for (int i = _livePooledEmissions.Count - 1; i >= 0; i--)
            {
                if (_livePooledEmissions[i].instance.gameObject != null)
                    Destroy(_livePooledEmissions[i].instance.gameObject);
            }

            _livePooledEmissions.Clear();

            foreach (var kvp in _prefabPools)
            {
                while (kvp.Value.Count > 0)
                {
                    var inst = kvp.Value.Dequeue();
                    if (inst.gameObject != null)
                        Destroy(inst.gameObject);
                }
            }

            _prefabPools.Clear();
        }
    }
}
