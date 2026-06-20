using UnityEngine;

namespace DecalMini
{
    public static class DecalSystemLog
    {
        public static bool VerboseEnabled
        {
            get
            {
                var settings = DecalSystemSettings.Instance;
                return settings != null && settings.verboseLogging;
            }
        }

        public static void Verbose(string message)
        {
            if (VerboseEnabled)
                Debug.Log(message);
        }

        public static void Warning(string message)
        {
            Debug.LogWarning(message);
        }
    }
}
