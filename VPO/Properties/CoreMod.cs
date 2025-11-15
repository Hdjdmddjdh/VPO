using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using VPO.Modules;

namespace VPO
{
    [BepInPlugin("com.example.vpo.core", "VPO Core", "0.2.3")]
    public class CoreMod : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Harmony H;
        internal static int UpdateStep = 2;

        private void Awake()
        {
            Log = Logger;
            H = new Harmony("com.example.vpo.core");

            UpdateStep = Config.Bind(
                "Core",
                "UpdateThrottlerStep",
                2,
                "Шаг троттлинга логики (1..4). 1 = без троттлинга"
            ).Value;

            try
            {
                H.PatchAll(typeof(CoreMod).Assembly);
                Logger.LogInfo($"[VPO Core] Патчи применены. UpdateStep={UpdateStep}");

                SetupManagers();
            }
            catch (Exception e)
            {
                Logger.LogError($"[VPO Core] Ошибка при PatchAll: {e.GetType().Name}: {e.Message}");
            }
        }

        private void SetupManagers()
        {
            var go = new GameObject("[VPO_Manager]");
            UnityEngine.Object.DontDestroyOnLoad(go);

            // GC сглаживание
            var gc = go.AddComponent<GCManager>();
            GCManager.Init(Config, gc);

            // Динамический LOD
            var lod = go.AddComponent<DynamicLODManager>();
            DynamicLODManager.Init(Config);

            // Оптимизация эффектов
            var fx = go.AddComponent<EffectOptimizer>();
            EffectOptimizer.Init(Config);

            // Мягкие ресурсы / “префетч”
            var z = go.AddComponent<ZonePrefetch>();
            ZonePrefetch.Init(Config, 3.0f);

            // Тёплый старт мира
            var warm = go.AddComponent<ThreadedWorldLoader>();
            ThreadedWorldLoader.StartWarmup(warm, 2.0f);

            Logger.LogInfo("[VPO Core] VPO_Manager инициализирован.");
        }
    }
}
