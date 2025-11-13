using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// Patches all ZNetScene.CreateObject(...) overloads without hardcoding signatures.
// No usage of string.Contains(x, StringComparison) so it compiles on .NET Framework 4.7.2.
namespace VPO.Patches
{
    [HarmonyPatch]
    internal static class ZNetScene_CreateObject_Patch
    {
        // Harmony will call this and patch every returned method.
        static IEnumerable<MethodBase> TargetMethods()
        {
            var znsType = AccessTools.TypeByName("ZNetScene");
            if (znsType == null)
            {
                Debug.Log("[VPO] ZNetScene type not found — patch skipped.");
                yield break;
            }

            foreach (var m in AccessTools.GetDeclaredMethods(znsType))
            {
                // We only care about methods literally named CreateObject (any overload).
                if (m.Name == "CreateObject" && m.GetParameters().Length >= 1)
                    yield return m;
            }
        }

        // Lightweight Prefix just to confirm the hook works (can be extended later).
        static void Prefix(MethodBase __originalMethod)
        {
            try
            {
                Debug.Log("[VPO Hook] ZNetScene." + __originalMethod.Name + "(" +
                          string.Join(", ", __originalMethod.GetParameters().Select(p => p.ParameterType.Name)) + ")");
            }
            catch (Exception e)
            {
                Debug.Log("[VPO Hook] Prefix log failed: " + e);
            }
        }
    }
}
