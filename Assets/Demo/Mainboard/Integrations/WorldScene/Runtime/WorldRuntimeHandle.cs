using System;
using UnityEngine;
using WorldSceneModule.Runtime;
using Object = UnityEngine.Object;

namespace Mainboard.Runtime
{
    internal sealed class WorldRuntimeHandle : IDisposable
    {
        private readonly WorldSceneDriver _ownedDriver;
        private bool _disposed;

        public WorldRuntimeHandle(WorldSceneDriver driver, WorldSceneDriver ownedDriver)
        {
            Driver = driver;
            _ownedDriver = ownedDriver;
        }

        public WorldSceneDriver Driver { get; }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_ownedDriver != null)
                Object.Destroy(_ownedDriver.gameObject);
        }
    }
}
