using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// Dumps exact ZNetScene *Create* method signatures to the log at runtime.
// Uses IndexOf(..., StringComparison) to work on .NET Framework 4.7.2.
namespace VPO.Patches
{
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class ZNetScene_SignatureDump
    {
        static void Postfix()
        {
            try
            {
                var t = typeof(ZNetScene);
                var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(m => m.Name.IndexOf("Create", StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(m => m.Name)
                    .ThenBy(m => m.GetParameters().Length);

                Debug.Log("--- ZNetScene signatures ---");
                foreach (var m in methods)
                {
                    var pars = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.FullName));
                    Debug.Log($"[VPO Sig] {m.ReturnType.FullName} ZNetScene.{m.Name}({pars})");
                }
                Debug.Log("--- /ZNetScene signatures ---");
            }
            catch (Exception e)
            {
                Debug.Log("[VPO Sig] Dump failed: " + e);
            }
        }
    }
}
