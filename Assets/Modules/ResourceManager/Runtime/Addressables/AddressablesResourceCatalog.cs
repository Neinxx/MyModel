using System.Collections.Generic;
using UnityEngine;

namespace ResourceManagerModule.Runtime
{
    [CreateAssetMenu(menuName = "Modules/Resource Manager/Addressables Catalog", fileName = "AddressablesResourceCatalog")]
    public sealed class AddressablesResourceCatalog : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] private List<string> _keys = new List<string>();

        private HashSet<string> _keySet;

        public IReadOnlyList<string> Keys => _keys;

        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            EnsureIndex();
            return _keySet.Contains(key);
        }

        public void SetKeys(IEnumerable<string> keys)
        {
            _keys.Clear();

            if (keys != null)
            {
                foreach (var key in keys)
                {
                    if (!string.IsNullOrEmpty(key) && !_keys.Contains(key))
                    {
                        _keys.Add(key);
                    }
                }
            }

            _keySet = null;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            _keySet = null;
        }

        private void EnsureIndex()
        {
            if (_keySet != null)
            {
                return;
            }

            _keySet = new HashSet<string>();
            foreach (var key in _keys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    _keySet.Add(key);
                }
            }
        }
    }
}
