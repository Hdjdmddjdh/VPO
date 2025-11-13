using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace VPO
{
    [BepInPlugin("com.example.vpo.core", "VPO Core", "0.2.3")]
    public sealed class CoreMod : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Harmony Harmony;

        private void Awake()
        {
            Log = Logger;
            Harmony = new Harmony("com.example.vpo.core");
            CompatPatches.Patch_CreateObject(Harmony, Log);
            Log.LogInfo("VPO Core: патчи применены.");
        }

        private void OnDestroy()
        {
            try { Harmony?.UnpatchSelf(); } catch { }
        }
    }
}
