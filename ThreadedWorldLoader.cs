using System.Collections;
using UnityEngine;

namespace VPO.Modules
{
    public class ThreadedWorldLoader : MonoBehaviour
    {
        public static void StartWarmup(MonoBehaviour runner, float delaySec = 2f)
        {
            runner.StartCoroutine(DoWarmup(delaySec));
        }

        private static IEnumerator DoWarmup(float delaySec)
        {
            if (delaySec > 0f) yield return new WaitForSecondsRealtime(delaySec);

            float end = Time.unscaledTime + 3f;
            while (Time.unscaledTime < end)
            {
                Resources.UnloadUnusedAssets();
                yield return null;
            }
        }
    }
}
