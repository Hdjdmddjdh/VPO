using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;

namespace VPO
{
    [BepInPlugin("com.example.vpo.core", "VPO Core", "0.2.3")]
    public class CoreMod : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Harmony H;
        internal static int UpdateStep = 2; // 1 = без троттлинга

        private void Awake()
        {
            Log = Logger;
            H = new Harmony("com.example.vpo.core");

            UpdateStep = Config.Bind("Core", "UpdateThrottlerStep", 2,
                "Шаг троттлинга логики (1..4). 1 = без троттлинга").Value;

            try
            {
                H.PatchAll(typeof(CoreMod).Assembly);
                Logger.LogInfo($"[VPO Core] Патчи применены. UpdateStep={UpdateStep}");
            }
            catch (Exception e)
            {
                Logger.LogError($"[VPO Core] Ошибка при PatchAll: {e.GetType().Name}: {e.Message}");
            }
        }
    }

    internal static class UpdateThrottlerUtil
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