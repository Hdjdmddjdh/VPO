using System.Collections;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace VPO
{
    [BepInPlugin("com.example.vpo.gpu", "VPO GPU Boost", "0.2.3")]
    public sealed class GPUBoostPlugin : BaseUnityPlugin
    {
        // === Конфиг ===
        private ConfigEntry<bool> _enableInstancing;
        private ConfigEntry<bool> _onlyMeshRenderers;
        private ConfigEntry<int> _materialsPerFrame;

        private ConfigEntry<int> _streamBudgetMB;
        private ConfigEntry<int> _uploadBufferMB;
        private ConfigEntry<bool> _uploadPersistent;

        private ConfigEntry<bool> _forceAniso;
        private ConfigEntry<int> _globalMipmapLimit;

        private void Awake()
        {
            // GPU Instancing
            _enableInstancing = Config.Bind("GPU Instancing", "EnableInstancing", true, "Включить инстансинг (материалы).");
            _onlyMeshRenderers = Config.Bind("GPU Instancing", "OnlyMeshRenderers", false, "Трогать только материалы у MeshRenderer.");
            _materialsPerFrame = Config.Bind("GPU Instancing", "MaterialsPerFrame", 64, "Материалов обрабатывается за кадр.");

            // Streaming/Upload
            _streamBudgetMB = Config.Bind("GPU Streaming", "StreamingBudgetMB", 4096, "Бюджет стриминга текстур (MB).");
            _uploadBufferMB = Config.Bind("GPU Streaming", "AsyncUploadBufferMB", 125, "Буфер асинхронной загрузки (MB).");
            _uploadPersistent = Config.Bind("GPU Streaming", "AsyncUploadPersistent", true, "Постоянный буфер асинхронной загрузки.");

            // Quality
            _forceAniso = Config.Bind("GPU Quality", "ForceAnisotropic", true, "Принудительно включить AF.");
            _globalMipmapLimit = Config.Bind("GPU Quality", "GlobalTextureMipmapLimit", 0, "Глобальный mipmap-лимит (0 = максимум качества).");

            StartCoroutine(SetupRoutine());
        }

        private IEnumerator SetupRoutine()
        {
            // 1) Применяем «глобальные» настройки сразу
            ApplyStreamingAndQuality();

            // 2) Чуть ждём, чтобы мир и ресурсы успели инициализироваться
            yield return new WaitForSeconds(2f);

            // 3) Аккуратно включаем инстансинг партиями
            if (_enableInstancing.Value)
                yield return EnableInstancingBatched(_materialsPerFrame.Value, _onlyMeshRenderers.Value);
        }

        private void ApplyStreamingAndQuality()
        {
            // Стриминг текстур
            QualitySettings.streamingMipmapsActive = true;
            QualitySettings.streamingMipmapsMemoryBudget = Mathf.Clamp(_streamBudgetMB.Value, 256, 8192); // MB

            // Асинхронные аплоады в GPU
            QualitySettings.asyncUploadBufferSize = Mathf.Clamp(_uploadBufferMB.Value, 16, 512);          // MB
            QualitySettings.asyncUploadPersistentBuffer = _uploadPersistent.Value;

            // Качество текстур/анизотропия
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(_globalMipmapLimit.Value, 0, 3);
            if (_forceAniso.Value)
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

            CoreMod.Log.LogInfo($"[GPU] Streaming={QualitySettings.streamingMipmapsMemoryBudget} MB, Upload={QualitySettings.asyncUploadBufferSize} MB, MipLimit={QualitySettings.globalTextureMipmapLimit}, AF={(QualitySettings.anisotropicFiltering)}");
        }

        private static System.Collections.Generic.IEnumerable<Material> EnumerateMaterials(bool onlyMeshRenderers)
        {
            // Берём только загруженные ресурсы
            var mats = Resources.FindObjectsOfTypeAll<Material>();
            if (!onlyMeshRenderers) return mats;

            // Если просили — оставим материалы, которые реально висят на MeshRenderer
            var used = Resources.FindObjectsOfTypeAll<MeshRenderer>()
                                .SelectMany(r => r.sharedMaterials)
                                .Where(m => m != null)
                                .ToHashSet();
            return mats.Where(m => used.Contains(m));
        }

        private IEnumerator EnableInstancingBatched(int perFrame, bool onlyMeshRenderers)
        {
            var count = 0;
            foreach (var m in EnumerateMaterials(onlyMeshRenderers))
            {
                if (m == null) continue;
                if (!m.enableInstancing)
                    m.enableInstancing = true;

                if (++count % Mathf.Max(1, perFrame) == 0)
                    yield return null; // отдаём кадр
            }
            CoreMod.Log.LogInfo($"[GPU] Instancing enabled. Materials touched: {count}");
        }
    }
}
