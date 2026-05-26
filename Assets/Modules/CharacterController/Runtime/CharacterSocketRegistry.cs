using System.Collections.Generic;
using UnityEngine;

namespace CharacterController.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Character/Character Socket Registry")]
    public sealed class CharacterSocketRegistry : MonoBehaviour
    {
        private readonly Dictionary<CharacterSocketId, Transform> _typedSockets =
            new Dictionary<CharacterSocketId, Transform>();
        private readonly Dictionary<string, Transform> _namedSockets =
            new Dictionary<string, Transform>(System.StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            Refresh();
        }

        private void OnValidate()
        {
            Refresh();
        }

        public void Refresh()
        {
            _typedSockets.Clear();
            _namedSockets.Clear();
            _typedSockets[CharacterSocketId.Root] = transform;

            var sockets = GetComponentsInChildren<CharacterSocket>(true);
            foreach (var socket in sockets)
            {
                if (socket == null)
                    continue;

                if (!_typedSockets.ContainsKey(socket.Id))
                    _typedSockets.Add(socket.Id, socket.transform);
                else
                    Debug.LogWarning($"[CharacterSocketRegistry] Duplicate socket '{socket.Id}' on '{socket.name}'. Keeping the first one.", socket);

                if (!string.IsNullOrWhiteSpace(socket.CustomId) && !_namedSockets.ContainsKey(socket.CustomId))
                    _namedSockets.Add(socket.CustomId, socket.transform);
            }
        }

        public bool TryGet(CharacterSocketId id, out Transform socket)
        {
            if (_typedSockets.Count == 0)
                Refresh();

            return _typedSockets.TryGetValue(id, out socket);
        }

        public bool TryGet(string id, out Transform socket)
        {
            if (_typedSockets.Count == 0 && _namedSockets.Count == 0)
                Refresh();

            if (_namedSockets.TryGetValue(id, out socket))
                return true;

            foreach (var pair in _typedSockets)
            {
                if (string.Equals(pair.Key.ToString(), id, System.StringComparison.OrdinalIgnoreCase))
                {
                    socket = pair.Value;
                    return true;
                }
            }

            socket = null;
            return false;
        }
    }
}
