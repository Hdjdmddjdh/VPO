// GPUBoostPlugin.cs — мягко «прогревает» GPU: включает instancing на материалах и грузит меши в видеопамять порциями.
// Без снижения качества — globalTextureMipmapLimit остаётся управляемым в конфиге.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace VPO
{
    [BepInPlugin("com.example.vpo.gpu", "VPO GPU Boost", "0.1.2")]
    public class GPUBoostPlugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> _enableInstancing;
        private ConfigEntry<bool> _onlyMeshRenderers;
        private ConfigEntry<int>  _materialsPerFrame;
        private ConfigEntry<bool> _forceAniso;
        private ConfigEntry<int>  _globalMipmapLimit;

        private void Awake()
        {
            _enableInstancing   = Config.Bind("GPU Instancing", "EnableMaterialInstancing", true, "Включить instancing массово для материалов.");
            _onlyMeshRenderers  = Config.Bind("GPU Instancing", "OnlyMeshRenderers", false, "true = только MeshRenderer; false = также SkinnedMeshRenderer.");
            _materialsPerFrame  = Config.Bind("GPU Instancing", "MaterialsPerFrame", 64, "Сколько материалов обрабатывать за кадр (порционно).");
            _forceAniso         = Config.Bind("GPU Quality", "ForceAnisotropicFiltering", true, "Принудительно включить анизотропную фильтрацию.");
            _globalMipmapLimit  = Config.Bind("GPU Quality", "GlobalTextureMipmapLimit", 0, "0 = максимум качества; 1..3 — более грубые мипы (экономия VRAM).");

            ApplyOneShotGraphics();
            StartCoroutine(EnableInstancingBatched());

            Logger.LogInfo("[GPU] Инициализация завершена.");
        }

        private void ApplyOneShotGraphics()
        {
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(_globalMipmapLimit.Value, 0, 3);
            QualitySettings.anisotropicFiltering = _forceAniso.Value
                ? AnisotropicFiltering.ForceEnable
                : AnisotropicFiltering.Enable;
        }

        private IEnumerable<Renderer> EnumerateRenderers()
        {
            // В Unity 6000 используем новое API
            var all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (_onlyMeshRenderers.Value)
                return all.Where(r => r is MeshRenderer);
            return all;
        }

        private IEnumerator EnableInstancingBatched()
        {
            if (!_enableInstancing.Value) yield break;

            // Подождать, пока сцена поднимется
            yield return new WaitForSeconds(2f);

            int processed = 0, perFrame = Mathf.Max(16, _materialsPerFrame.Value);
            int inFrame = 0;

            foreach (var r in EnumerateRenderers())
            {
                if (!r) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;

                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (!m) continue;
                    if (!m.enableInstancing) m.enableInstancing = true;
                    processed++;
                    inFrame++;
                    if (inFrame >= perFrame)
                    {
                        inFrame = 0;
                        yield return null; // мягкая порционность
                    }
                }

                // Дополнительно: подталкиваем меш в видеопамять без флага readOnly
                var mf = r.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh)
                {
                    try { mf.sharedMesh.UploadMeshData(false); } catch { }
                }
            }

            Logger.LogInfo($"[GPU] Instancing включён (материалов обработано: {processed}).");
        }
    }
}