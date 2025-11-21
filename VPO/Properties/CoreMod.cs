using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace VPO
{
    [BepInPlugin("com.example.vpo.core", "VPO Core", "0.2.3")]
    public class CoreMod : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Harmony H;

        // Включать ли хук на ZNetScene.CreateObject(...)
        internal static bool EnableCreateHook = true;

        // Шаг троттлинга для AI: 1 = каждый кадр, 2 = каждый второй и т.д.
        internal static int UpdateStep = 1;

        private void Awake()
        {
            Log = Logger;
            H = new Harmony("com.example.vpo.core");

            EnableCreateHook = Config.Bind(
                "Core",
                "HookCreateObject",
                true,
                "Включать хук ZNetScene.CreateObject для очереди/пула."
            ).Value;

            UpdateStep = Config.Bind(
                "Core",
                "UpdateThrottlerStep",
                1,
                "Шаг троттлинга AI/логики (1..4). 1 = без троттлинга."
            ).Value;

            H.PatchAll(typeof(CoreMod).Assembly);
            Logger.LogInfo($"[VPO Core] Патчи применены. UpdateStep={UpdateStep}, HookCreate={EnableCreateHook}");
        }
    }
}
