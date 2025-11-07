using BepInEx.Configuration;
using UnityEngine;

namespace VPO.Modules
{
    public class ZonePrefetch : MonoBehaviour
    {
        private static ConfigEntry<bool> _enable;
        private static ConfigEntry<float> _interval;
        private static float _next;

        public static void Init(BepInEx.Configuration.ConfigFile cfg, float intervalSec = 2.5f)
        {
            _enable   = cfg.Bind("ZonePrefetch", "Enable", true, "Включить мягкое предзапрос зон вокруг игрока.");
            _interval = cfg.Bind("ZonePrefetch", "IntervalSec", intervalSec, "Интервал запросов (сек).");
        }

        void Update()
        {
            if (!_enable.Value) return;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Mathf.Max(0.5f, _interval.Value);

            Resources.UnloadUnusedAssets();
        }
    }
}
