using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mainboard.Runtime
{
    [CreateAssetMenu(fileName = "MainboardProfile", menuName = "Mainboard/Profile")]
    public sealed class MainboardProfile : ScriptableObject
    {
        public bool autoBoot = true;

        [SerializeField] private List<MainboardInstaller> installers =
            new List<MainboardInstaller>();

        public IReadOnlyList<MainboardInstaller> Installers => installers;

        public IEnumerable<MainboardInstaller> GetInstallers()
        {
            return installers
                .Where(installer => installer != null)
                .OrderBy(installer => installer.Priority);
        }
    }
}
