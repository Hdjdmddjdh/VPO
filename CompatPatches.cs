// CompatPatches.cs — гибкие Harmony-патчи под Unity 6000: ловим CreateObject и FixedUpdate/UpdateAI без жёстких сигнатур.
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace VPO
{
    // Универсальный хук ZNetScene.CreateObject(...).
    // Включается флагом Core.HookCreateObject (по умолчанию false, чтобы не шуметь).
    [HarmonyPatch]
    internal static class ZNetScene_CreateObject_Patch
    {
        static bool Prepare() => CoreMod.EnableCreateHook;

        static MethodBase TargetMethod()
        {
            var t = typeof(ZNetScene);
            var m = AccessTools.GetDeclaredMethods(t)
                                .Where(mi => mi.Name == "CreateObject")
                                .OrderByDescending(mi => mi.GetParameters().Length)
                                .FirstOrDefault();
            if (m == null)
                CoreMod.Log?.LogWarning("CreateObject: не найден — патч пропущен.");
            else
                CoreMod.Log?.LogInfo($"CreateObject: hooked => {m}");
            return m;
        }

        // Сейчас просто пропускаем через оригинал. Когда захочешь — добавим пул/батч прямо здесь.
        static bool Prefix() => true;
    }

    // Хук для BaseAI.* — находим FixedUpdate/UpdateAI/Update, где что есть в данной версии.
    [HarmonyPatch]
    internal static class BaseAI_Update_Patch
    {
        static MethodBase TargetMethod()
        {
            var t = typeof(BaseAI);
            var m = AccessTools.Method(t, "FixedUpdate")
                 ?? AccessTools.Method(t, "FixedUpdateAI")
                 ?? AccessTools.Method(t, "UpdateAI")
                 ?? AccessTools.Method(t, "Update");
            if (m == null)
                CoreMod.Log?.LogWarning("BaseAI: метод обновления не найден.");
            else
                CoreMod.Log?.LogInfo($"BaseAI: hooked => {m.Name}");
            return m;
        }

        static bool Prefix()
        {
            return UpdateThrottler.ShouldRun(CoreMod.UpdateStep);
        }
    }
}