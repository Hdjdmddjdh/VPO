
// VPO.Culling.cs
// BepInEx plugin: VPO Distance Culling + Light/FX throttling (jobified distance checks)
// Author: Aksel (for Vlad). Unity 6 / .NET 4.x, BepInEx 5.x
// Drop this file into your existing VPO project (same folder as other .cs files) and build.
// GUID: com.example.vpo.culling

using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace VPO
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class VpoCullingPlugin : BaseUnityPlugin
    {
        public const string GUID = "com.example.vpo.culling";
        public const string NAME = "VPO Distance Culling";
        public const string VERSION = "0.1.0";

        // --- Config ---
        private ConfigEntry<bool> _enableCulling;
        private ConfigEntry<float> _cullDistanceMeters;
        private ConfigEntry<int> _applyPerFrame;
        private ConfigEntry<float> _refreshCacheSec;
        private ConfigEntry<bool> _throttleLights;
        private ConfigEntry<float> _lightShadowOffMeters;
        private ConfigEntry<float> _lightDisableMeters;
        private ConfigEntry<bool> _throttleParticles;
        private ConfigEntry<float> _particleDisableMeters;

        // --- Data ---
        private Camera _cam;
        private readonly List<Renderer> _renderers = new List<Renderer>(4096);
        private readonly List<Light> _lights = new List<Light>(512);
        private readonly List<ParticleSystem> _fx = new List<ParticleSystem>(1024);

        private TransformAccessArray _taa = default;
        private NativeArray<byte> _visible = default; // 1 = near, 0 = far (culled)
        private float _nextCacheTime;
        private float _nextJobTime;
        private int _lastAppliedIndex;
        private bool _disposed;

        private void Awake()
        {
            _enableCulling = Config.Bind("Culling", "Enable", true, "Включить дистанционное отключение рендереров (Visibility Culling).");
            _cullDistanceMeters = Config.Bind("Culling", "CullDistance", 120f, "Дальность видимости (м). Всё, что дальше — выключается.");
            _applyPerFrame = Config.Bind("Culling", "ChangesPerFrame", 600, "Сколько рендереров вкл/выкл за кадр (батч).");
            _refreshCacheSec = Config.Bind("Culling", "RefreshCacheSec", 5f, "Переиндексация сценовых объектов каждые N секунд.");
            _throttleLights = Config.Bind("Lights", "Throttle", true, "Гасить тени и отключать дальние источники света.");
            _lightShadowOffMeters = Config.Bind("Lights", "ShadowOffDistance", 35f, "Дальше этого — выключаем тени у света.");
            _lightDisableMeters = Config.Bind("Lights", "DisableDistance", 60f, "Дальше этого — выключаем источник света полностью.");
            _throttleParticles = Config.Bind("Particles", "Throttle", true, "Отключать дальние ParticleSystem.");
            _particleDisableMeters = Config.Bind("Particles", "DisableDistance", 55f, "Дальше этого — отключаем эмиссию частиц.");

            Harmony.CreateAndPatchAll(typeof(VpoCullingPlugin));

            Logger.LogInfo($"{NAME} {VERSION} initialized.");
        }

        private void OnDestroy() => DisposeNative();

        private void DisposeNative()
        {
            if (_disposed) return;
            if (_taa.isCreated) _taa.Dispose();
            if (_visible.IsCreated) _visible.Dispose();
            _disposed = true;
        }

        private void Update()
        {
            _cam = _cam ? _cam : Camera.main;
            if (_cam == null) return;

            if (Time.time >= _nextCacheTime)
            {
                RebuildCache();
                _nextCacheTime = Time.time + Mathf.Max(1f, _refreshCacheSec.Value);
            }

            if (_enableCulling.Value)
            {
                // Schedule + apply visibility job ~4x/second (не на каждый кадр, чтобы не жечь CPU)
                if (Time.time >= _nextJobTime)
                {
                    RunVisibilityJob();
                    _nextJobTime = Time.time + 0.25f;
                    _lastAppliedIndex = 0; // применяем результаты батчами в LateUpdate
                }
            }
        }

        private void LateUpdate()
        {
            if (!_enableCulling.Value || !_visible.IsCreated) return;

            int toApply = _applyPerFrame.Value;
            int count = math.min(toApply, _renderers.Count - _lastAppliedIndex);
            if (count <= 0) return;

            var camPos = (float3)_cam.transform.position;
            float cullSqr = _cullDistanceMeters.Value * _cullDistanceMeters.Value;
            for (int i = 0; i < count; i++)
            {
                int idx = _lastAppliedIndex + i;
                if (idx >= _renderers.Count) break;
                var r = _renderers[idx];
                if (!r) continue;

                bool near = _visible[idx] != 0;
                // Вкл/выкл рендер
                if (r.enabled != near) r.enabled = near;

                // Тени — только когда достаточно близко
                if (near && Vector3.SqrMagnitude(r.transform.position - (Vector3)camPos) <= (_lightShadowOffMeters.Value * _lightShadowOffMeters.Value))
                    r.shadowCastingMode = ShadowCastingMode.On;
                else
                    r.shadowCastingMode = ShadowCastingMode.Off;
            }
            _lastAppliedIndex += count;

            if (_throttleLights.Value) ApplyLightThrottle(camPos);
            if (_throttleParticles.Value) ApplyParticleThrottle(camPos);
        }

        private void ApplyLightThrottle(float3 camPos)
        {
            float offSqr = _lightShadowOffMeters.Value * _lightShadowOffMeters.Value;
            float killSqr = _lightDisableMeters.Value * _lightDisableMeters.Value;
            int processed = 0;
            // Обновляем немного за кадр, чтобы не дёргать всё сразу
            foreach (var l in _lights)
            {
                if (!l) continue;
                float d2 = math.lengthsq((float3)l.transform.position - camPos);
                if (d2 > killSqr)
                {
                    if (l.enabled) l.enabled = false;
                }
                else
                {
                    if (!l.enabled) l.enabled = true;
                    l.shadows = (d2 <= offSqr) ? LightShadows.Soft : LightShadows.None;
                }
                if (++processed >= 128) break; // ограничимся скромным числом правок в кадр
            }
        }

        private void ApplyParticleThrottle(float3 camPos)
        {
            float killSqr = _particleDisableMeters.Value * _particleDisableMeters.Value;
            int processed = 0;
            foreach (var ps in _fx)
            {
                if (!ps) continue;
                float d2 = math.lengthsq((float3)ps.transform.position - camPos);
                var emission = ps.emission;
                bool want = d2 <= killSqr;
                if (emission.enabled != want) emission.enabled = want;
                if (++processed >= 256) break;
            }
        }

        private void RebuildCache()
        {
            // Собираем актуальные объекты сцены (только активные и видимые слои).
            // Исключаем: скрытые редакторские, океан/воду, Gizmos и т.п.
            var rends = UnityEngine.Object.FindObjectsOfType<Renderer>(includeInactive: false);
            _renderers.Clear();
            _renderers.AddRange(rends.Where(r =>
            {
                if (!r.gameObject.activeInHierarchy) return false;
                if (r is ParticleSystemRenderer) return false; // частицы отдельно
                if (r is SpriteRenderer) return false;
                // Игнорируем эффекты постпроца/скрытые
                return true;
            }));

            var lights = UnityEngine.Object.FindObjectsOfType<Light>(includeInactive: false);
            _lights.Clear();
            _lights.AddRange(lights.Where(l => l.type != LightType.Directional)); // управляем только локальными

            var fx = UnityEngine.Object.FindObjectsOfType<ParticleSystem>(includeInactive: false);
            _fx.Clear();
            _fx.AddRange(fx);

            // Переcобираем TransformAccessArray и NativeArray для результатов
            if (_taa.isCreated) _taa.Dispose();
            if (_visible.IsCreated) _visible.Dispose();

            var transforms = _renderers.Select(r => r.transform).ToArray();
            _taa = new TransformAccessArray(transforms);
            _visible = new NativeArray<byte>(_renderers.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            Logger.LogInfo($"[{NAME}] Cached: {_renderers.Count} renderers, {_lights.Count} lights, {_fx.Count} particles.");
        }

        private void RunVisibilityJob()
        {
            if (!_taa.isCreated || _taa.length == 0) return;
            if (!_cam) return;

            var job = new DistanceCullingJob
            {
                camPos = _cam.transform.position,
                cullSqr = _cullDistanceMeters.Value * _cullDistanceMeters.Value,
                output = _visible
            };

            // Планируем параллельно по всем трансформам
            JobHandle handle = job.Schedule(_taa);
            handle.Complete(); // Завершаем сейчас; применение - батчами в LateUpdate
        }

        // --- Job: считает расстояние до камеры ---
        private struct DistanceCullingJob : IJobParallelForTransform
        {
            public float3 camPos;
            public float cullSqr;
            public NativeArray<byte> output; // 1 = ближний, 0 = дальний

            public void Execute(int index, TransformAccess transform)
            {
                float3 p = transform.position;
                float d2 = math.lengthsq(p - camPos);
                output[index] = (byte)(d2 <= cullSqr ? 1 : 0);
            }
        }
    }
}
