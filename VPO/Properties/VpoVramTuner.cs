// VpoVramTuner.cs (fixed)
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace VPO.Modules
{
    /// Настройки под Unity 6: агрессивный стриминг текстур + SRP-batching.
    [HarmonyPatch]
    internal static class VpoVramTuner
    {
        internal static void Apply()
        {
            // --- Стриминг мипов в VRAM ---
            QualitySettings.streamingMipmapsActive = true;
            QualitySettings.streamingMipmapsMemoryBudget = 2048f; // 2 ГБ под мипы (можешь 3072f на 6 ГБ VRAM)
            QualitySettings.streamingMipmapsMaxLevelReduction = 0; // не урезать качество
            QualitySettings.globalTextureMipmapLimit = 0;          // взамен masterTextureLimit (deprecated)

            // --- Поведение загрузчика текстур ---
            // Эти свойства есть в Unity 2019+; если что-то отсутствует в сборке — ставим через рефлексию безопасно.
            try { Texture.streamingTextureDiscardUnusedMips = false; } catch {}
            try { Texture.streamingTextureForceLoadAll = true; } catch {}

            // Некоторые билды Unity не содержат streamingTextureAddAllCameras — поставим через рефлексию, если свойство существует.
            var prop = typeof(Texture).GetProperty("streamingTextureAddAllCameras", BindingFlags.Public | BindingFlags.Static);
            if (prop != null && prop.CanWrite)
            {
                try { prop.SetValue(null, true); } catch {}
            }

            // --- Графический батчинг SRP (вдобавок к нашему инстансингу) ---
            try { GraphicsSettings.useScriptableRenderPipelineBatching = true; } catch {}
        }

        // Применяем в меню (FejdStartup)…
        [HarmonyPostfix]
        [HarmonyPatch(typeof(FejdStartup), "Awake")]
        private static void OnMenuAwake() => Apply();

        // …и повторно при подъёме сцены объектов.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static void OnWorldAwake() => Apply();
    }
}