using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo.Core
{
    /// <summary>
    /// 静态类型安全的发布订阅总线。事件一律为值类型结构体。
    /// 订阅方必须在 OnDisable 中成对退订。
    /// </summary>
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

            // 取快照后调用：处理器在回调中订阅/退订时不会破坏本次派发。
            Delegate[] snapshot = existing.GetInvocationList();

            for (int i = 0; i < snapshot.Length; i++)
            {
                ((Action<T>)snapshot[i]).Invoke(evt);
            }
        }

        public static void Clear()
        {
            Handlers.Clear();
        }

        // 关闭 Domain Reload 时静态状态会跨 Play 残留，必须清空。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Clear();
        }
    }
}
