using BepInEx.Configuration;
using UnityEngine;
using System.Collections;

namespace VPO.Modules
{
    public class GCManager : MonoBehaviour
    {
        private static ConfigEntry<bool> _enable;
        private static ConfigEntry<float> _interval;

        public static void Init(BepInEx.Configuration.ConfigFile cfg, MonoBehaviour runner)
        {
            _enable   = cfg.Bind("GC", "Enable", true, "Включить сглаживание GC.");
            _interval = cfg.Bind("GC", "IntervalSec", 20f, "Интервал мягких сборок мусора (сек).");
            runner.StartCoroutine(Co());
        }

        private static IEnumerator Co()
        {
            while (true)
            {
                if (_enable.Value)
                {
                    System.GC.Collect(System.GC.MaxGeneration, System.GCCollectionMode.Optimized, false, false);
                }
                yield return new WaitForSecondsRealtime(Mathf.Max(5f, _interval.Value));
            }
        }
    }
}
