// PoolingManager.cs — простой кросс-сценовый пул GameObject.
using System.Collections.Generic;
using UnityEngine;

namespace VPO
{
    public static class PoolingManager
    {
        private static readonly Dictionary<string, Queue<GameObject>> _pools = new();
        private static Transform _root;

        private static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("[VPO_Pool]");
                    Object.DontDestroyOnLoad(go);
                    _root = go.transform;
                }
                return _root;
            }
        }

        public static GameObject GetFromPool(GameObject prefab)
        {
            if (!prefab) return null;
            var key = prefab.name;
            if (_pools.TryGetValue(key, out var q) && q.Count > 0)
            {
                var obj = q.Dequeue();
                if (obj) { obj.SetActive(true); return obj; }
            }
            var inst = Object.Instantiate(prefab);
            inst.name = key;
            inst.SetActive(true);
            return inst;
        }

        public static void ReturnToPool(GameObject obj)
        {
            if (!obj) return;
            var key = obj.name;
            if (!_pools.TryGetValue(key, out var q))
            {
                q = new Queue<GameObject>();
                _pools[key] = q;
            }
            obj.SetActive(false);
            obj.transform.SetParent(Root, false);
            q.Enqueue(obj);
        }
    }
}