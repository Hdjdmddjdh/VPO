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
            if (delaySec > 0f)
                yield return new WaitForSecondsRealtime(delaySec);

            // ќдин т€жЄлый проход
            Resources.UnloadUnusedAssets();

            // ≈щЄ один Ц через секунду, чтобы добить мусор, но без лупа по 3 секунды
            yield return new WaitForSecondsRealtime(1f);
            Resources.UnloadUnusedAssets();
        }
    }
}
