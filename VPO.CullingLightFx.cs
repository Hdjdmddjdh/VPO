
// VPO.CullingLightFx.cs
// BepInEx plugin: VPO Culling + Lights/Particles throttling + basic quality knobs (Lite, no Jobs/Burst)
// Author: Aksel (for Vlad). Unity 6 / .NET 4.x, BepInEx 5.x
// GUID: com.vpo.cullinglite

using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace VPO
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class VpoCullingLightFx : BaseUnityPlugin
    {
        public const string GUID = "com.vpo.cullinglite";
        public const string NAME = "VPO Culling & LightFX (Lite)";
        public const string VERSION = "0.3.0";

        // --- Config ---
        private ConfigEntry<bool> _enableCulling;
        private ConfigEntry<float> _cullDistance;
        private ConfigEntry<int> _applyPerFrame;
        private ConfigEntry<float> _refreshCacheSec;

        private ConfigEntry<bool> _lightsThrottle;
        private ConfigEntry<float> _lightShadowOffDist;
        private ConfigEntry<float> _lightDisableDist;
        private ConfigEntry<int> _pixelLightCount;

        private ConfigEntry<bool> _particlesThrottle;
        private ConfigEntry<float> _particleDisableDist;

        private ConfigEntry<bool> _adaptiveShadows;
        private ConfigEntry<float> _shadowDistanceNear;
        private ConfigEntry<float> _shadowDistanceFar;
        private ConfigEntry<float> _fpsLow;
        private ConfigEntry<float> _fpsHigh;

        // --- Data ---
        private readonly List<Renderer> _renderers = new List<Renderer>(4096);
        private readonly List<Light> _lights = new List<Light>(512);
        private readonly List<ParticleSystem> _fx = new List<ParticleSystem>(1024);

        private Camera _cam;
        private int _applyIndex;
        private float _nextCache;
        private float _fpsTimer;
        private int _fpsFrames;
        private float _currentFps = 60f;

        private void Awake()
        {
            // Culling
            _enableCulling     = Config.Bind("Culling",   "Enable", true,  "Включить дистанционное отключение рендереров.");
            _cullDistance      = Config.Bind("Culling",   "CullDistance", 120f, "Дальность видимости (м). Дальше — выключаем Renderer.");
            _applyPerFrame     = Config.Bind("Culling",   "ApplyPerFrame", 800, "Сколько рендереров вкл/выкл за кадр.");
            _refreshCacheSec   = Config.Bind("Culling",   "RefreshCacheSec", 5f, "Переиндексация сцены каждые N секунд.");

            // Lights
            _lightsThrottle    = Config.Bind("Lights",    "Throttle", true, "Ограничивать огни и тени по дистанции.");
            _lightShadowOffDist= Config.Bind("Lights",    "ShadowOffDistance", 35f, "Дальше этого расстояния тени у источников отключаются.");
            _lightDisableDist  = Config.Bind("Lights",    "DisableDistance", 60f, "Дальше этого расстояния источник выключается.");
            _pixelLightCount   = Config.Bind("Lights",    "PixelLightCount", 2, "QualitySettings.pixelLightCount.");

            // Particles
            _particlesThrottle = Config.Bind("Particles", "Throttle", true, "Отключать дальние ParticleSystem.");
            _particleDisableDist = Config.Bind("Particles", "DisableDistance", 55f, "Дальше этого расстояния частицы выключены.");

            // Adaptive shadows
            _adaptiveShadows   = Config.Bind("Shadows",   "Adaptive", true, "Адаптивная дальность теней по FPS.");
            _shadowDistanceNear= Config.Bind("Shadows",   "DistanceNear", 70f, "shadowDistance при низком FPS.");
            _shadowDistanceFar = Config.Bind("Shadows",   "DistanceFar", 120f, "shadowDistance при высоком FPS.");
            _fpsLow            = Config.Bind("Shadows",   "FpsLow", 35f, "Ниже этого FPS — Near профиль.");
            _fpsHigh           = Config.Bind("Shadows",   "FpsHigh", 58f, "Выше этого FPS — Far профиль.");

            QualitySettings.pixelLightCount = Mathf.Max(0, _pixelLightCount.Value);

            Harmony.CreateAndPatchAll(typeof(VpoCullingLightFx));
            Logger.LogInfo($"{NAME} {VERSION} initialized.");
        }

        private void Update()
        {
            // FPS meter (скользящее усреднение ~0.5с)
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _currentFps = _fpsFrames / _fpsTimer;
                _fpsFrames = 0;
                _fpsTimer = 0f;

                if (_adaptiveShadows.Value)
                {
                    if (_currentFps <= _fpsLow.Value)
                        QualitySettings.shadowDistance = _shadowDistanceNear.Value;
                    else if (_currentFps >= _fpsHigh.Value)
                        QualitySettings.shadowDistance = _shadowDistanceFar.Value;
                }
            }

            _cam = _cam ? _cam : Camera.main;
            if (!_cam) return;

            if (Time.time >= _nextCache)
            {
                RebuildCache();
                _nextCache = Time.time + Mathf.Max(1f, _refreshCacheSec.Value);
                _applyIndex = 0;
            }

            if (_enableCulling.Value)
            {
                ApplyCullingBatched(_applyPerFrame.Value);
            }

            if (_lightsThrottle.Value)
            {
                ApplyLightsBatched(128);
            }
            if (_particlesThrottle.Value)
            {
                ApplyParticlesBatched(256);
            }
        }

        private void RebuildCache()
        {
            _renderers.Clear();
            _lights.Clear();
            _fx.Clear();

#if UNITY_2023_1_OR_NEWER
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var fx = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
#else
            var rends = UnityEngine.Object.FindObjectsOfType<Renderer>();
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            var fx = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
#endif

            foreach (var r in rends)
            {
                if (!r || !r.gameObject.activeInHierarchy) continue;
                if (r is ParticleSystemRenderer) continue;
                if (r is SpriteRenderer) continue;
                _renderers.Add(r);
            }

            foreach (var l in lights)
            {
                if (!l || !l.gameObject.activeInHierarchy) continue;
                if (l.type == LightType.Directional) continue; // глобальный свет не трогаем
                _lights.Add(l);
            }

            foreach (var p in fx)
            {
                if (!p || !p.gameObject.activeInHierarchy) continue;
                _fx.Add(p);
            }

            Logger.LogInfo($"[{NAME}] Cached: {_renderers.Count} renderers, {_lights.Count} lights, {_fx.Count} particles.");
        }

        private void ApplyCullingBatched(int perFrame)
        {
            if (_renderers.Count == 0) return;
            var camPos = _cam.transform.position;
            float cullSqr = _cullDistance.Value * _cullDistance.Value;

            int count = Mathf.Min(perFrame, _renderers.Count - _applyIndex);
            for (int i = 0; i < count; i++)
            {
                int idx = _applyIndex + i;
                if (idx >= _renderers.Count) break;
                var r = _renderers[idx];
                if (!r) continue;

                float d2 = (r.transform.position - camPos).sqrMagnitude;
                bool near = d2 <= cullSqr;

                if (r.enabled != near) r.enabled = near;
                r.shadowCastingMode = near ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            _applyIndex += count;
            if (_applyIndex >= _renderers.Count) _applyIndex = 0; // круг
        }

        private int _lightCursor;
        private void ApplyLightsBatched(int perFrame)
        {
            if (_lights.Count == 0) return;
            var camPos = _cam.transform.position;
            float offSqr = _lightShadowOffDist.Value * _lightShadowOffDist.Value;
            float killSqr = _lightDisableDist.Value * _lightDisableDist.Value;

            int n = Mathf.Min(perFrame, _lights.Count);
            for (int i = 0; i < n; i++)
            {
                var l = _lights[_lightCursor];
                if (l)
                {
                    float d2 = (l.transform.position - camPos).sqrMagnitude;
                    if (d2 > killSqr)
                    {
                        if (l.enabled) l.enabled = false;
                    }
                    else
                    {
                        if (!l.enabled) l.enabled = true;
                        l.shadows = (d2 <= offSqr) ? LightShadows.Soft : LightShadows.None;
                    }
                }
                _lightCursor++;
                if (_lightCursor >= _lights.Count) _lightCursor = 0;
            }
        }

        private int _fxCursor;
        private void ApplyParticlesBatched(int perFrame)
        {
            if (_fx.Count == 0) return;
            var camPos = _cam.transform.position;
            float killSqr = _particleDisableDist.Value * _particleDisableDist.Value;

            int n = Mathf.Min(perFrame, _fx.Count);
            for (int i = 0; i < n; i++)
            {
                var ps = _fx[_fxCursor];
                if (ps)
                {
                    float d2 = (ps.transform.position - camPos).sqrMagnitude;
                    var emission = ps.emission;
                    bool want = d2 <= killSqr;
                    if (emission.enabled != want) emission.enabled = want;
                }
                _fxCursor++;
                if (_fxCursor >= _fx.Count) _fxCursor = 0;
            }
        }
    }
}
