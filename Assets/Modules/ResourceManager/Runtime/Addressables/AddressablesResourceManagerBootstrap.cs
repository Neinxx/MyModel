using UnityEngine;

namespace ResourceManagerModule.Runtime
{
    internal static class AddressablesResourceManagerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterProvider()
        {
            _ = AddressablesProvider.RegisterDefault().InitializeAsync();
        }
    }
}
