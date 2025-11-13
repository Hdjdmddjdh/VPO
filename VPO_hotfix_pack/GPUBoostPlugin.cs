// GPUBoostPlugin.cs (fixed: без _didMenuPass и без предупреждений)
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VPO
{
    [BepInPlugin("com.example.vpo.gpu", "VPO GPU Boost", "0.2.2")]
    public class GPUBoostPlugin : BaseUnityPlugin
    {
        // === Конфиг ===
        private ConfigEntry<bool> _enableInstancing;
        private ConfigEntry<bool> _onlyMeshRenderers;
        private ConfigEntry<int>  _batchSize;
        private ConfigEntry<bool> _forceAniso;
        private ConfigEntry<int>  _globalMips;
        private ConfigEntry<bool> _secondPass;
        private ConfigEntry<int>  _secondDelay;
        private ConfigEntry<int>  _firstDelay;
        private ConfigEntry<bool> _uploadTuner;

        // флаги, которые реально используются
        private bool _didWorldPass1;
        private bool _didWorldPass2;

        private void Awake()
        {
            _enableInstancing    = Config.Bind("GPU Instancing", "EnableMaterialInstancing", true, "Включить instancing (материалы) порциями.");
            _onlyMeshRenderers   = Config.Bind("GPU Instancing", "OnlyMeshRenderers", false, "true = только MeshRenderer; false = включая SkinnedMeshRenderer.");
            _batchSize           = Config.Bind("GPU Instancing", "MaterialsPerFrame", 64, "Сколько рендереров обрабатывать за кадр.");
            _forceAniso          = Config.Bind("GPU Quality",     "ForceAnisotropicFiltering", true, "Принудительно включить AF.");
            _globalMips          = Config.Bind("GPU Quality",     "GlobalTextureMipmapLimit", 0, "0 = макс. качество; 1..3 — грубее мипы.");
            _secondPass          = Config.Bind("GPU Streaming",   "EnableSecondPass", true, "Сделать повторный мягкий проход.");
            _secondDelay         = Config.Bind("GPU Streaming",   "StreamingSecondPassDelaySec", 12, "Задержка перед повторным проходом, сек.");
            _firstDelay          = Config.Bind("GPU Streaming",   "ForceLoadDelaySec", 3, "Задержка перед первым прогревом, сек.");
            _uploadTuner         = Config.Bind("GPU Streaming",   "EnableUploadTuner", true, "Мягкие UploadMeshData-подсказки.");

            ApplyGraphicsOnce();

            // Мини-проход в меню (может обработать 0 — это ок)
            StartCoroutine(InstancingPassDelayed(_firstDelay.Value, context:"menu"));

            // Реагируем на смену сцен
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        private void OnSceneChanged(Scene from, Scene to)
        {
            // Первый проход через пару секунд после входа в мир
            if (!_didWorldPass1)
                StartCoroutine(InstancingPassDelayed(3, context:"world-1", markWorld1:true));

            // Второй мягкий через заданную задержку
            if (_secondPass.Value && !_didWorldPass2)
                StartCoroutine(InstancingPassDelayed(Mathf.Max(8, _secondDelay.Value), context:"world-2", markWorld2:true));
        }

        private void ApplyGraphicsOnce()
        {
            QualitySettings.globalTextureMipmapLimit = Mathf.Max(0, _globalMips.Value);
            QualitySettings.anisotropicFiltering = _forceAniso.Value
                ? AnisotropicFiltering.ForceEnable
                : AnisotropicFiltering.Enable;
        }

        private IEnumerator InstancingPassDelayed(int delaySec, string context, bool markWorld1 = false, bool markWorld2 = false)
        {
            if (!_enableInstancing.Value) yield break;
            if (delaySec > 0) yield return new WaitForSeconds(delaySec);

            int processed = 0;
            if (_onlyMeshRenderers.Value)
            {
                var arr = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                processed += EnableInstancingFor(arr);
            }
            else
            {
                var a = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var b = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                processed += EnableInstancingFor(a);
                processed += EnableInstancingFor(b);
            }

            Logger.LogInfo($"[GPU] Instancing включён ({context}), обработано материалов/рендереров: {processed}.");

            if (markWorld1) _didWorldPass1 = true;
            if (markWorld2) _didWorldPass2 = true;
        }

        private int EnableInstancingFor(Renderer[] renderers)
        {
            int processed = 0;
            int batch = Mathf.Max(8, _batchSize.Value);
            int local = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;

                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (!mat) continue;
                    if (!mat.enableInstancing) mat.enableInstancing = true;
                    processed++;
                }

                if (_uploadTuner.Value)
                {
                    if (r is MeshRenderer mr)
                    {
                        var mf = mr.GetComponent<MeshFilter>();
                        if (mf && mf.sharedMesh) mf.sharedMesh.UploadMeshData(false);
                    }
                    else if (r is SkinnedMeshRenderer smr)
                    {
                        if (smr.sharedMesh) smr.sharedMesh.UploadMeshData(false);
                    }
                }

                local++;
                if ((local % batch) == 0) { local = 0; }
            }
            return processed;
        }
    }
}