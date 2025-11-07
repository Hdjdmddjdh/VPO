using BepInEx.Configuration;
using UnityEngine;

namespace VPO.Modules
{
    public class DynamicLODManager : MonoBehaviour
    {
        private static ConfigEntry<bool> _enable;
        private static ConfigEntry<float> _baseDistance;
        private static ConfigEntry<float> _scaleWithFPS;
        private static float _targetFPS = 60f;

        public static void Init(BepInEx.Configuration.ConfigFile cfg)
        {
            _enable       = cfg.Bind("DynamicLOD", "Enable", true, "Включить динамическую регулировку дистанций/LOD.");
            _baseDistance = cfg.Bind("DynamicLOD", "BaseDistance", 80f, "Базовая дистанция отрисовки декора/мелочей.");
            _scaleWithFPS = cfg.Bind("DynamicLOD", "ScaleWithFPS", 1.0f, "Насколько агрессивно масштабировать под FPS (0..2).");
        }

        void Update()
        {
            if (!_enable.Value) return;

            float fps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            float factor = Mathf.Clamp01(fps / _targetFPS) * _scaleWithFPS.Value + (1f - _scaleWithFPS.Value);
            float dist = Mathf.Clamp(_baseDistance.Value * factor, 30f, 200f);

            QualitySettings.lodBias = Mathf.Clamp(dist / 80f, 0.5f, 2.5f);
        }
    }
}
