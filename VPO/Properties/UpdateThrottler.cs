namespace VPO
{
    internal static class UpdateThrottler
    {
        private static int _frameCounter;

        /// <summary>
        /// Возвращает true, если в этом кадре логику можно выполнять.
        /// step <= 1 — всегда true.
        /// </summary>
        internal static bool ShouldRun(int step)
        {
            if (step <= 1)
                return true;

            if (step < 0)
                step = 1;

            _frameCounter++;
            return (_frameCounter % step) == 0;
        }
    }
}
