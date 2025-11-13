using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace VPO
{
    // Патчим без жёсткой сигнатуры: берём последнюю перегрузку CreateObject
    internal static class CompatPatches
    {
        private static MethodInfo _target;

        internal static void Patch_CreateObject(Harmony h, ManualLogSource log)
        {
            var t = AccessTools.TypeByName("ZNetScene");
            if (t == null) { log.LogWarning("ZNetScene не найден — пропускаю патч."); return; }

            _target = AccessTools.GetDeclaredMethods(t)
                                 .Where(m => m.Name == "CreateObject")
                                 .OrderByDescending(m => m.GetParameters().Length)
                                 .FirstOrDefault();

            if (_target == null)
            {
                log.LogWarning("CreateObject: подходящих перегрузок не найдено — патч пропущен.");
                return;
            }

            var pre = new HarmonyMethod(typeof(CompatPatches).GetMethod(nameof(CreateObject_Prefix),
                                                                         BindingFlags.NonPublic | BindingFlags.Static));
            h.Patch(_target, prefix: pre);
            log.LogInfo($"[VPO Hook] Hooked: {t.FullName}.{_target.Name}({string.Join(", ", _target.GetParameters().Select(p => p.ParameterType.Name))})");
        }

        // Сейчас пропускаем через оригинал, но точка расширения есть:
        private static bool CreateObject_Prefix()
        {
            // здесь можно вставить пуллинг/батч и вернуть false, если нужно перехватить создание
            return true; // true => выполнить оригинальный метод
        }
    }
}
