// CoreMod.cs — ядро VPO: конфиги, логгер, и запуск Harmony-патчей.
// Совместимо с Valheim 0.221.x (Unity 6000) + BepInEx 5 + HarmonyX.
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace VPO
{
    [BepInPlugin("com.example.vpo.core", "VPO Core", "0.2.2")]
    public class CoreMod : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Harmony H;

        // Публичные настройки (читаются из конфига)
        public static int UpdateStep = 2; // 1 = без троттлинга
        public static bool EnableCreateHook = false; // можно включить позже, когда понадобится пул на спавне

        private void Awake()
        {
            Log = Logger;
            H = new Harmony("com.example.vpo.core");

            // Конфиг
            UpdateStep = Config.Bind("Core", "UpdateThrottlerStep", 2,
                "Шаг троттлинга логики (1..4). 1 = без троттлинга").Value;
            EnableCreateHook = Config.Bind("Core", "HookCreateObject", false,
                "Включить хук ZNetScene.CreateObject (экспериментально)").Value;

            // Патчим все классы из этой сборки
            H.PatchAll(typeof(CoreMod).Assembly);

            Log.LogInfo($"VPO Core: патчи применены. UpdateStep={UpdateStep}, HookCreateObject={EnableCreateHook}");
        }
    }

    /// <summary>Мини-троттлер, чтобы пропускать тики Update/FixedUpdate.</summary>
    internal static class UpdateThrottler
    {
        private static int s_counter;
        public static bool ShouldRun(int step)
        {
            if (step <= 1) return true;
            s_counter++;
            if (s_counter >= step) { s_counter = 0; return true; }
            return false;
        }
    }
}