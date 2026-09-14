using UnityEngine;
using Demo.Combat;
using Demo.Core;

namespace Demo.Player
{
    /// <summary>
    /// 把 PlayerController 的攻击窗口事件接到 Hitbox 上。
    /// 不修改 PlayerController 的战斗职责，只订阅它广播的窗口开合。
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private Hitbox hitbox;

        private void Reset()
        {
            controller = GetComponent<PlayerController>();
        }

        private void OnEnable()
        {
            if (controller == null)
            {
                controller = GetComponent<PlayerController>();
            }

            if (controller != null)
            {
                controller.AttackWindowOpened += OnWindowOpened;
                controller.AttackWindowClosed += OnWindowClosed;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.AttackWindowOpened -= OnWindowOpened;
                controller.AttackWindowClosed -= OnWindowClosed;
            }
        }

        /// <summary>multiplier 来自 PlayerController 的连招链，最后一段为翻倍值。</summary>
        private void OnWindowOpened(float multiplier)
        {
            if (InputLock.IsLocked || hitbox == null)
            {
                return;
            }

            hitbox.OpenWindow(multiplier);
        }

        private void OnWindowClosed()
        {
            if (hitbox != null)
            {
                hitbox.CloseWindow();
            }
        }
    }
}
