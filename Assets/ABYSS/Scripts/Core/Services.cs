using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Core
{
    // dead simple service registry. managers register themselves in Awake,
    // everyone else asks here instead of calling FindObjectOfType all over the place
    public static class Services
    {
        static readonly Dictionary<Type, object> _map = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) return;
            if (_map.TryGetValue(typeof(T), out var existing) && existing != null && !ReferenceEquals(existing, service))
                AbyssLog.Warn($"replacing service {typeof(T).Name}");
            _map[typeof(T)] = service;
        }

        public static void Unregister<T>(T service) where T : class
        {
            if (_map.TryGetValue(typeof(T), out var existing) && ReferenceEquals(existing, service))
                _map.Remove(typeof(T));
        }

        public static T Get<T>() where T : class
        {
            if (_map.TryGetValue(typeof(T), out var s))
            {
                // destroyed unity objects compare equal to null via the unity override
                if (s is UnityEngine.Object uo && uo == null) { _map.Remove(typeof(T)); return null; }
                return (T)s;
            }
            return null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            service = Get<T>();
            return service != null;
        }

        // domain reload can be off in the editor, so wipe statics on play
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _map.Clear();
    }
}
