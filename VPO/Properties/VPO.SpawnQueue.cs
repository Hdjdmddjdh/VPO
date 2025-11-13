
// VPO.SpawnQueue.cs
// BepInEx plugin: VPO Spawn Queue (defer GameObject activation to avoid spikes on base enter)
// Author: Aksel (for Vlad). Unity 6 / .NET 4.x, BepInEx 5.x
// GUID: com.vpo.spawnqueue

using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace VPO
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class VpoSpawnQueue : BaseUnityPlugin
    {
        public const string GUID = "com.vpo.spawnqueue";
        public const string NAME = "VPO Spawn Queue";
        public const string VERSION = "0.2.0";

        private static VpoSpawnQueue _inst;

        private ConfigEntry<bool> _deferExtra;
        private ConfigEntry<int> _maxNewPerFrame;
        private ConfigEntry<int> _resumePerFrame;

        private int _createdThisFrame;
        private readonly Queue<GameObject> _activationQueue = new Queue<GameObject>(2048);

        private void Awake()
        {
            _inst = this;
            _deferExtra     = Config.Bind("Queue", "DeferExtra", true, "Если за кадр создано слишком много объектов — временно деактивировать лишние и активировать позже.");
            _maxNewPerFrame = Config.Bind("Queue", "MaxNewPerFrame", 50, "Сколько новых объектов можно активировать сразу в кадре (остальные в очередь).");
            _resumePerFrame = Config.Bind("Queue", "ResumePerFrame", 80, "Сколько «отложенных» объектов активировать каждый кадр.");

            Harmony.CreateAndPatchAll(typeof(VpoSpawnQueue));
            Logger.LogInfo($"{NAME} {VERSION} initialized.");
        }

        private void Update()
        {
            // Сбрасываем счётчик на каждый кадр
            _createdThisFrame = 0;

            // Активируем отложенные объекты порциями
            int toResume = _resumePerFrame.Value;
            while (toResume-- > 0 && _activationQueue.Count > 0)
            {
                var go = _activationQueue.Dequeue();
                if (go) go.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.CreateObject), typeof(ZDO))]
        private static class Patch_CreateObject
        {
            private static void Postfix(GameObject __result)
            {
                if (__result == null || _inst == null) return;

                _inst._createdThisFrame++;

                if (_inst._deferExtra.Value && _inst._createdThisFrame > _inst._maxNewPerFrame.Value)
                {
                    // Временно «замораживаем» активацию тяжёлых объектов
                    if (__result.activeSelf)
                        __result.SetActive(false);
                    _inst._activationQueue.Enqueue(__result);
                }
            }
        }
    }
}
