using System.Collections.Generic;
using UnityEngine;

namespace Pool
{
    public class ObjectPool<T> where T : Component, IPoolable
    {
        private readonly Dictionary<GameObject, Queue<T>> _byPrefab = new();
        private readonly Transform _poolRoot;

        public ObjectPool(Transform poolRoot)
        {
            _poolRoot = poolRoot;
        }

        public Transform PoolRoot => _poolRoot;

        public T Get(GameObject prefab)
        {
            if (prefab == null)
                return null;
            
            if (!_byPrefab.TryGetValue(prefab, out var queue) || queue.Count == 0)
                return null;
            
            return queue.Dequeue();
        }

        public void Return(T item)
        {
            if (item == null)
                return;
            
            var prefab = item.SourcePrefab;
            if (prefab == null)
                return;

            item.transform.SetParent(GetOrCreateContainer(prefab));

            if (!_byPrefab.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<T>();
                _byPrefab[prefab] = queue;
            }
            queue.Enqueue(item);
        }

        public Transform GetOrCreateContainer(GameObject prefab)
        {
            if (prefab == null)
                return _poolRoot;
            
            var name = prefab.name;
            for (var i = 0; i < _poolRoot.childCount; i++)
            {
                var child = _poolRoot.GetChild(i);
                if (child.name == name)
                    return child;
            }
            
            var go = new GameObject(name);
            go.transform.SetParent(_poolRoot);
            
            return go.transform;
        }

        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0)
                return;

            var container = GetOrCreateContainer(prefab);

            if (!_byPrefab.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<T>();
                _byPrefab[prefab] = queue;
            }

            for (var i = 0; i < count; i++)
            {
                var go = Object.Instantiate(prefab, container);
                var item = go.GetComponent<T>();
                if (item == null)
                    item = go.AddComponent<T>();
                item.SourcePrefab = prefab;
                go.SetActive(false);
                queue.Enqueue(item);
            }
        }

        public void Clear()
        {
            _byPrefab.Clear();
        }
    }
}
