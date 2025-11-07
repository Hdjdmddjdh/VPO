using BepInEx.Configuration;
using UnityEngine;

namespace VPO.Modules
{
    public class EffectOptimizer : MonoBehaviour
    {
        private static ConfigEntry<bool> _enable;
        private static ConfigEntry<float> _farDistance;
        private static ConfigEntry<float> _nearDistance;

        public static void Init(BepInEx.Configuration.ConfigFile cfg)
        {
            _enable       = cfg.Bind("Effects", "Enable", true, "Снижать нагрузку от частиц на дальних дистанциях.");
            _farDistance  = cfg.Bind("Effects", "FarDistance", 60f, "Дистанция, дальше которой эффекты упрощаются.");
            _nearDistance = cfg.Bind("Effects", "NearDistance", 15f, "Дистанция, ближе которой эффекты возвращаются к норме.");
        }

        void Update()
        {
            if (!_enable.Value) return;

            var cam = Camera.main;
            if (cam == null) return;

            var particles = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            foreach (var ps in particles)
            {
                if (ps == null) continue;
                float dist = Vector3.Distance(cam.transform.position, ps.transform.position);
                var main = ps.main;
                if (dist > _farDistance.Value)
                {
                    main.maxParticles = Mathf.Min(main.maxParticles, 64);
                }
                else if (dist < _nearDistance.Value)
                {
                    main.maxParticles = Mathf.Max(main.maxParticles, 256);
                }
            }
        }
    }
}
