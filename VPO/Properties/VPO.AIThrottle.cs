
// VPO.AIThrottle.cs
// BepInEx plugin: VPO AI Throttle (skip updates adaptively by FPS)
// Author: Aksel (for Vlad). Unity 6 / .NET 4.x, BepInEx 5.x
// GUID: com.vpo.aithrottle

using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace VPO
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class VpoAIThrottle : BaseUnityPlugin
    {
        public const string GUID = "com.vpo.aithrottle";
        public const string NAME = "VPO AI Throttle";
        public const string VERSION = "0.2.0";

        private static VpoAIThrottle _inst;

        private ConfigEntry<bool> _enable;
        private ConfigEntry<int> _skipUpdateBase;
        private ConfigEntry<int> _skipFixedBase;
        private ConfigEntry<float> _fpsLow;
        private ConfigEntry<float> _fpsHigh;

        private float _fpsTimer;
        private int _fpsFrames;
        private float _fps = 60f;

        private int _skipUpdate; // текущие адаптивные настройки
        private int _skipFixed;

        // Счётчики на инстанс BaseAI
        private static readonly Dictionary<int, int> _updateCounters = new Dictionary<int, int>(8192);
        private static readonly Dictionary<int, int> _fixedCounters = new Dictionary<int, int>(8192);

        private void Awake()
        {
            _inst = this;
            _enable        = Config.Bind("AI", "Enable", true, "Включить троттлинг AI.");
            _skipUpdateBase= Config.Bind("AI", "SkipUpdateBase", 1, "Пропускать каждые N Update-тиков (1=каждый второй).");
            _skipFixedBase = Config.Bind("AI", "SkipFixedBase", 1, "Пропускать каждые N FixedUpdate-тиков.");
            _fpsLow        = Config.Bind("AI", "FpsLow", 35f, "Ниже — усиливаем троттлинг.");
            _fpsHigh       = Config.Bind("AI", "FpsHigh", 58f, "Выше — ослабляем троттлинг.");

            _skipUpdate = Mathf.Max(0, _skipUpdateBase.Value);
            _skipFixed  = Mathf.Max(0, _skipFixedBase.Value);

            Harmony.CreateAndPatchAll(typeof(VpoAIThrottle));
            Logger.LogInfo($"{NAME} {VERSION} initialized.");
        }

        private void Update()
        {
            // FPS метрика
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps = _fpsFrames / _fpsTimer;
                _fpsFrames = 0;
                _fpsTimer = 0f;

                if (_enable.Value)
                {
                    if (_fps <= _fpsLow.Value)
                    {
                        _skipUpdate = Mathf.Max(1, _skipUpdateBase.Value + 1);
                        _skipFixed  = Mathf.Max(1, _skipFixedBase.Value + 1);
                    }
                    else if (_fps >= _fpsHigh.Value)
                    {
                        _skipUpdate = Mathf.Max(0, _skipUpdateBase.Value - 1);
                        _skipFixed  = Mathf.Max(0, _skipFixedBase.Value - 1);
                    }
                    // иначе — оставляем как есть
                }
            }
        }

        [HarmonyPatch(typeof(BaseAI), "Update")]
        private static class Patch_AI_Update
        {
            private static bool Prefix(BaseAI __instance)
            {
                if (_inst == null || !_inst._enable.Value) return true;
                int id = __instance.GetInstanceID();
                int c;
                if (!_updateCounters.TryGetValue(id, out c)) c = 0;
                c++;
                _updateCounters[id] = c;

                // каждые (_skipUpdate+1) кадра выполняем логику, иначе — пропуск
                if (c % (_inst._skipUpdate + 1) != 0)
                    return false; // skip original
                return true;
            }
        }

        [HarmonyPatch(typeof(BaseAI), "FixedUpdate")]
        private static class Patch_AI_FixedUpdate
        {
            private static bool Prefix(BaseAI __instance)
            {
                if (_inst == null || !_inst._enable.Value) return true;
                int id = __instance.GetInstanceID();
                int c;
                if (!_fixedCounters.TryGetValue(id, out c)) c = 0;
                c++;
                _fixedCounters[id] = c;

                if (c % (_inst._skipFixed + 1) != 0)
                    return false; // skip original
                return true;
            }
        }
    }
}
