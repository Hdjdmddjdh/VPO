// CompatPatches.cs — гибкие Harmony-патчи под Unity 6000: ловим CreateObject и обновление AI.
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using VPO.Modules;

namespace VPO
{
    // Универсальный хук ZNetScene.CreateObject(...).
    // Включается флагом CoreMod.EnableCreateHook.
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

        // Сейчас ничего не меняем — просто даём оригиналу выполняться.
        static bool Prefix() => true;
    }

    // Хук для BaseAI.* — ищем FixedUpdate/UpdateAI/Update, что есть в этой версии.
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

        // Троттлим обновление AI через утилиту UpdateThrottler.
        static bool Prefix()
        {
            return UpdateThrottler.ShouldRun(CoreMod.UpdateStep);
        }
    }
}
