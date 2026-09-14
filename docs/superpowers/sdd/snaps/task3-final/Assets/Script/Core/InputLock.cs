using System.Collections.Generic;
using UnityEngine;

namespace Demo.Core
{
    /// <summary>
    /// 按持有者计数的输入锁。对话等模态状态下屏蔽攻击/交互输入。
    /// </summary>
    public static class InputLock
    {
        private static readonly HashSet<object> Owners = new HashSet<object>();

        public static bool IsLocked
        {
            get { return Owners.Count > 0; }
        }

        public static void Acquire(object owner)
        {
            if (owner == null)
            {
                return;
            }

            Owners.Add(owner);
        }

        public static void Release(object owner)
        {
            if (owner == null)
            {
                return;
            }

            Owners.Remove(owner);
        }

        public static void Clear()
        {
            Owners.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Clear();
        }
    }
}
