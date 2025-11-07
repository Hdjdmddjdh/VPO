using System;
using HarmonyLib;
using UnityEngine;

namespace VPO.Modules
{
    /// <summary>
    /// Распределяет апдейты ИИ/физики по кадрам.
    /// Простая логика: выполняем только раз в N кадров (N динамический).
    /// </summary>
    public static class UpdateThrottler
    {
        private static int _aiInterval = 2;
        private static int _physicsInterval = 2;

        public static void Configure(int aiInterval, int physicsInterval)
        {
            _aiInterval = Mathf.Max(1, aiInterval);
            _physicsInterval = Mathf.Max(1, physicsInterval);
        }

        private static bool ShouldRun(int interval)
        {
            if (interval <= 1) return true;
            return (Time.frameCount % interval) == 0;
        }

        // ---- Патч BaseAI.UpdateAI ----
        [HarmonyPatch]
        internal static class Patch_BaseAI_UpdateAI
        {
            static System.Reflection.MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("BaseAI");
                return t != null ? AccessTools.Method(t, "UpdateAI") : null;
            }

            static bool Prefix()
            {
                return ShouldRun(_aiInterval);
            }
        }

        // ---- Патч BaseAI.FixedUpdate ----
        [HarmonyPatch]
        internal static class Patch_BaseAI_FixedUpdate
        {
            static System.Reflection.MethodBase TargetMethod()
            {
                var t = AccessTools.TypeByName("BaseAI");
                return t != null ? AccessTools.Method(t, "FixedUpdate") : null;
            }

            static bool Prefix()
            {
                return ShouldRun(_physicsInterval);
            }
        }
    }
}