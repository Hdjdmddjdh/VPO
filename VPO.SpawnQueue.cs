using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace VPO
{
    [BepInPlugin("com.example.vpo.spawnqueue", "VPO Spawn Queue", "0.2.0")]
    public class VpoSpawnQueue : BaseUnityPlugin
    {
        internal static VpoSpawnQueue Instance;
        internal static ManualLogSource Log;

        // Очередь отложенной активации
        private readonly Queue<GameObject> _activationQueue = new Queue<GameObject>();

        // Сколько объектов создали в этом кадре
        private int _createdThisFrame;

        // Конфиги
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _deferExtra;
        private ConfigEntry<int> _maxNewPerFrame;
        private ConfigEntry<int> _maxActivationsPerFrame;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // === Конфиг ===
            _enabled = Config.Bind(
                "General",
                "Enabled",
                true,
                "Включить очередь спавна объектов."
            );

            _deferExtra = Config.Bind(
                "General",
                "DeferExtra",
                true,
                "Если включено, объекты сверх лимита за кадр будут временно выключены и активированы позже."
            );

            _maxNewPerFrame = Config.Bind(
                "General",
                "MaxNewPerFrame",
                120,
                "Максимальное количество новых объектов, создаваемых за один кадр до начала откладывания в очередь."
            );

            _maxActivationsPerFrame = Config.Bind(
                "General",
                "MaxActivationsPerFrame",
                30,
                "Максимальное количество отложенных объектов, активируемых за один кадр."
            );

            // === Harmony-патчи ===
            _harmony = new Harmony("com.example.vpo.spawnqueue");

            try
            {
                _harmony.PatchAll(typeof(VpoSpawnQueue).Assembly);
                Log.LogInfo("[VPO SpawnQueue] Патчи применены.");
            }
            catch (Exception e)
            {
                Log.LogError($"[VPO SpawnQueue] Ошибка при PatchAll: {e.GetType().Name}: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                try
                {
                    _harmony.UnpatchSelf();
                    Log.LogInfo("[VPO SpawnQueue] Патчи сняты.");
                }
                catch (Exception e)
                {
                    Log.LogError($"[VPO SpawnQueue] Ошибка при UnpatchSelf: {e.GetType().Name}: {e.Message}");
                }
            }
        }

        private void LateUpdate()
        {
            // Каждый кадр обнуляем счётчик созданных объектов
            _createdThisFrame = 0;

            if (!_enabled.Value)
                return;

            if (_activationQueue.Count == 0)
                return;

            int remaining = Mathf.Max(0, _maxActivationsPerFrame.Value);

            while (remaining-- > 0 && _activationQueue.Count > 0)
            {
                var go = _activationQueue.Dequeue();

                if (!go)
                    continue;

                if (!go.activeSelf)
                {
                    try
                    {
                        go.SetActive(true);
                    }
                    catch (Exception e)
                    {
                        Log.LogWarning($"[VPO SpawnQueue] Ошибка при активации объекта '{go.name}': {e.GetType().Name}: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Внутренний вызов из патча: зарегистрировать только что созданный объект.
        /// </summary>
        internal void OnObjectCreated(GameObject go)
        {
            if (!_enabled.Value || go == null)
                return;

            _createdThisFrame++;

            // Если откладывание выключено – даём игре работать как обычно
            if (!_deferExtra.Value)
                return;

            // Пока не превысили лимит – тоже ничего не трогаем
            if (_createdThisFrame <= _maxNewPerFrame.Value)
                return;

            // Всё сверх лимита – в очередь
            try
            {
                if (go.activeSelf)
                    go.SetActive(false);

                _activationQueue.Enqueue(go);
            }
            catch (Exception e)
            {
                Log.LogWarning($"[VPO SpawnQueue] Ошибка при добавлении объекта '{go.name}' в очередь: {e.GetType().Name}: {e.Message}");
            }
        }

        // ============================
        // Harmony-патч на ZNetScene.CreateObject(ZDO)
        // ============================

        [HarmonyPatch(typeof(ZNetScene), "CreateObject")]
        [HarmonyPatch(new Type[] { typeof(ZDO) })]
        private static class Patch_ZNetScene_CreateObject
        {
            // Postfix, чтобы поймать уже созданный объект
            private static void Postfix(GameObject __result)
            {
                var inst = VpoSpawnQueue.Instance;
                if (inst == null || __result == null)
                    return;

                inst.OnObjectCreated(__result);
            }
        }
    }
}
