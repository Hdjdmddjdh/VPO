using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace VPO
{
    // Универсальный хук на ZNetScene.CreateObject(...) — без жёсткой сигнатуры.
    [HarmonyPatch]
    static class ZNetScene_CreateObject_Patch
    {
        static MethodBase TargetMethod()
        {
            var t = typeof(ZNetScene);
            var m = AccessTools.GetDeclaredMethods(t)
                .Where(mi => mi.Name == "CreateObject")
                .OrderByDescending(mi => mi.GetParameters().Length)
                .FirstOrDefault();
            return m;
        }

        static void Prefix(object __instance)
        {
            // CoreMod.Log?.LogDebug("ZNetScene.CreateObject()");
        }
    }

    // Мягкий троттлинг для ИИ: ищем любой из методов, которые есть в этой версии игры
    [HarmonyPatch]
    static class BaseAI_Update_Patch
    {
        static MethodBase TargetMethod()
        {
            var t = typeof(BaseAI);
            var m = AccessTools.Method(t, "FixedUpdate")
                ?? AccessTools.Method(t, "FixedUpdateAI")
                ?? AccessTools.Method(t, "UpdateAI")
                ?? AccessTools.Method(t, "Update");
            if (m != null)
                CoreMod.Log?.LogInfo($"BaseAI: hooked => {m.Name}");
            else
                CoreMod.Log?.LogWarning("BaseAI: нет подходящего метода для хука (патч пропущен).");
            return m; // Harmony сам пропустит класс, если вернём null
        }

        static bool Prefix(BaseAI __instance)
        {
            return UpdateThrottlerUtil.ShouldRun(CoreMod.UpdateStep);
        }
    }
}