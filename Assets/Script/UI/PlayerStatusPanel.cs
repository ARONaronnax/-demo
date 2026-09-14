using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Demo.Combat;
using Demo.Core;

namespace Demo.UI
{
    /// <summary>玩家状态的纯 View：首次读取一次 HealthComponent，之后只响应玩家受伤事件。</summary>
    public class PlayerStatusPanel : MonoBehaviour
    {
        [SerializeField] private HealthComponent health;
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Image portrait;

        public void Bind(HealthComponent source, Image fill, TMP_Text value, Image portraitImage)
        {
            health = source;
            hpFill = fill;
            hpText = value;
            portrait = portraitImage;
            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EntityDamagedEvent>(OnEntityDamaged);
            Refresh();
        }

        private void Start()
        {
            // 不同 GameObject 之间的 Awake/OnEnable 顺序不保证固定。
            // 到 Start 时所有场景对象的 Awake 已完成，可安全取得玩家初始满血值。
            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EntityDamagedEvent>(OnEntityDamaged);
        }

        public void SetPortrait(Sprite sprite)
        {
            if (portrait != null && sprite != null) portrait.sprite = sprite;
        }

        private void OnEntityDamaged(EntityDamagedEvent e)
        {
            if (e.IsPlayer) SetValue(e.RemainingHp, health != null ? health.MaxHp : e.RemainingHp);
        }

        private void Refresh()
        {
            if (health != null) SetValue(health.CurrentHp, health.MaxHp);
        }

        private void SetValue(float current, float maximum)
        {
            float safeMax = Mathf.Max(1f, maximum);
            if (hpFill != null) hpFill.fillAmount = Mathf.Clamp01(current / safeMax);
            if (hpText != null) hpText.text = Mathf.CeilToInt(current) + " / " + Mathf.CeilToInt(maximum);
        }
    }
}
