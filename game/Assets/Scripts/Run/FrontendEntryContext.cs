namespace TrickalFanGame.Run
{
    public static class FrontendEntryContext
    {
        private static bool homeRequested;

        public static bool HasHomeRequest => homeRequested;

        public static bool TryRequestHome()
        {
            if (homeRequested) return false;
            homeRequested = true;
            return true;
        }

        public static bool TryConsumeHome()
        {
            if (!homeRequested) return false;
            homeRequested = false;
            return true;
        }

        public static void Clear()
        {
            homeRequested = false;
        }
    }
}
