using System;
using System.Collections.Generic;
using UnityEngine;

namespace VPO.Modules
{
    /// <summary>
    /// Безопасный батчинг статических мешей. Не трогаем Piece/дым/воду.
    /// Использует StaticBatchingUtility.Combine для стабильности.
    /// </summary>
    public static class MeshCombiner
    {
        // Грубые фильтры компонентов, которые НЕ нужно комбинировать
        private static readonly string[] BlockedTypes =
        {
            "Piece", "WearNTear", "Smoke", "ZNetView", "WaterVolume", "Fermenter", "BeeHive"
        };

        /// <summary>
        /// Комбинирует детей у указанного корня, соблюдая лимиты.
        /// </summary>
        public static int CombineUnder(Transform root, int maxTrisPerBatch = 40000)
        {
            if (root == null) return 0;

            var toCombine = new List<GameObject>();
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var f in filters)
            {
                if (f == null || f.sharedMesh == null) continue;
                var go = f.gameObject;
                if (!go.activeInHierarchy) continue;
                if (HasBlocked(go)) continue;
                if (f.sharedMesh.triangles.Length / 3 > maxTrisPerBatch) continue;

                toCombine.Add(go);
            }

            if (toCombine.Count == 0) return 0;

            try
            {
                StaticBatchingUtility.Combine(toCombine.ToArray(), root.gameObject);
            }
            catch { /* если что-то пошло не так — просто пропускаем */ }

            return toCombine.Count;
        }

        private static bool HasBlocked(GameObject go)
        {
            foreach (var t in BlockedTypes)
            {
                var comp = go.GetComponent(t);
                if (comp != null) return true;
            }
            return false;
        }
    }
}