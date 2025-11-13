// BaseAI_UpdateAI_Patch.cs
using HarmonyLib;
using System.Reflection;

namespace VPO.Modules
{
    /// Патчим только BaseAI.UpdateAI (Unity 6). Никаких FixedUpdate/FixedUpdateAI.
    [HarmonyPatch]
    internal static class BaseAI_UpdateAI_Patch
    {
        private static MethodBase _target;

        // Готовим: ищем метод. Если не нашли — патч отключается.
        static bool Prepare()
        {
            var t = AccessTools.TypeByName("BaseAI");
            _target = t != null ? AccessTools.DeclaredMethod(t, "UpdateAI") : null;
            return _target != null;
        }

        // Указываем Хармони конкретную цель.
        [HarmonyTargetMethod]
        static MethodBase TargetMethod() => _target;

        // Тут можешь в будущем что-то делать с AI, пока пусто — важно корректное подключение.
        static void Postfix() { }
    }
}