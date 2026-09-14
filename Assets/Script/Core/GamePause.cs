using System.Collections.Generic;
using UnityEngine;

namespace Demo.Core
{
    /// <summary>
    /// 按持有者计数的全局暂停。任意持有者 Acquire 之后 Time.timeScale = 0，
    /// 全部 Release 后恢复。
    ///
    /// 与 InputLock 的分工：
    ///   InputLock  只屏蔽输入，世界照常运转（对话用）
    ///   GamePause  冻结整个游戏时间，怪物 / 动画 / 计时器全停（背包用）
    /// 需要"让玩家安心操作"的场合两个一起用。
    ///
    /// 记的是进入暂停前的 timeScale 而不是硬编码 1，以后加子弹时间不会被冲掉。
    /// </summary>
    public static class GamePause
    {
        private static readonly HashSet<object> Owners = new HashSet<object>();

        private static float _timeScaleBeforePause = 1f;

        public static bool IsPaused
        {
            get { return Owners.Count > 0; }
        }

        public static void Acquire(object owner)
        {
            if (owner == null)
            {
                return;
            }

            if (Owners.Count == 0)
            {
                _timeScaleBeforePause = Time.timeScale;
            }

            if (Owners.Add(owner))
            {
                Apply();
            }
        }

        public static void Release(object owner)
        {
            if (owner == null)
            {
                return;
            }

            if (!Owners.Remove(owner))
            {
                return;
            }

            Apply();
        }

        public static void Clear()
        {
            Owners.Clear();
            _timeScaleBeforePause = 1f;
            Time.timeScale = 1f;
        }

        private static void Apply()
        {
            Time.timeScale = Owners.Count > 0 ? 0f : _timeScaleBeforePause;
        }

        // 关闭 Domain Reload 时静态状态会跨 Play 残留，必须清空。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Clear();
        }
    }
}
