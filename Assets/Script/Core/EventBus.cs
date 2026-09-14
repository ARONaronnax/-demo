using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo.Core
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Handlers =
            new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                return;
            }

            Type key = typeof(T);

            if (Handlers.TryGetValue(key, out Delegate existing))
            {
                Handlers[key] = Delegate.Combine(existing, handler);
            }
            else
            {
                Handlers[key] = handler;
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
            {
                return;
            }

            Type key = typeof(T);

            if (!Handlers.TryGetValue(key, out Delegate existing))
            {
                return;
            }

            Delegate remaining = Delegate.Remove(existing, handler);

            if (remaining == null)
            {
                Handlers.Remove(key);
            }
            else
            {
                Handlers[key] = remaining;
            }
        }

        public static void Publish<T>(T evt) where T : struct
        {
            if (!Handlers.TryGetValue(typeof(T), out Delegate existing))
            {
                return;
            }

            // Delegates are immutable, so this local value is already a safe dispatch snapshot.
            ((Action<T>)existing).Invoke(evt);
        }

        public static void Clear()
        {
            Handlers.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Clear();
        }
    }
}
