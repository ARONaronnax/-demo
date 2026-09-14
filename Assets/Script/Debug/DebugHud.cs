using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Demo.Core;

namespace Demo.Debugging
{
    /// <summary>
    /// 临时验收用 IMGUI HUD。做正式 UI 时整个 Debug/ 目录删除。
    /// 所有引用均为可选，未接入的系统显示为 "—"。
    ///
    /// 尺寸随屏幕高度自适应：以 1080p 为基准，再乘 uiScale。
    /// 高 DPI 屏上默认字号会小到无法阅读，因此不要改成固定像素。
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        private const int MaxLogLines = 12;
        private const float ReferenceHeight = 1080f;
        private const float PanelWidth = 520f;
        private const float PanelHeight = 500f;
        private const int FontSize = 16;

        [SerializeField] private Combat.HealthComponent playerHealth;
        [SerializeField] private Quest.QuestComponent questComponent;
        [SerializeField] private Inventory.InventoryComponent inventory;

        [Tooltip("在自适应缩放之上再乘的倍数，屏幕太大或太小时调这个")]
        [SerializeField] private float uiScale = 1.4f;

        private readonly List<string> _log = new List<string>();
        private string _inventorySummary = "—";

        private GUIStyle _labelStyle;
        private GUIStyle _promptStyle;

        private bool _promptVisible;
        private string _promptText = string.Empty;

        private void OnEnable()
        {
            EventBus.Subscribe<InteractionPromptChangedEvent>(OnPromptChanged);
            EventBus.Subscribe<InteractedEvent>(OnInteracted);
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<DialogueLineChangedEvent>(OnDialogueLineChanged);
            EventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
            EventBus.Subscribe<QuestAcceptedEvent>(OnQuestAccepted);
            EventBus.Subscribe<QuestProgressChangedEvent>(OnQuestProgressChanged);
            EventBus.Subscribe<QuestCompletedEvent>(OnQuestCompleted);
            EventBus.Subscribe<EntityDamagedEvent>(OnEntityDamaged);
            EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
            EventBus.Subscribe<ItemPickedUpEvent>(OnItemPickedUp);
            EventBus.Subscribe<InventoryChangedEvent>(OnInventoryChanged);

            RebuildInventorySummary();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InteractionPromptChangedEvent>(OnPromptChanged);
            EventBus.Unsubscribe<InteractedEvent>(OnInteracted);
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueLineChangedEvent>(OnDialogueLineChanged);
            EventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
            EventBus.Unsubscribe<QuestAcceptedEvent>(OnQuestAccepted);
            EventBus.Unsubscribe<QuestProgressChangedEvent>(OnQuestProgressChanged);
            EventBus.Unsubscribe<QuestCompletedEvent>(OnQuestCompleted);
            EventBus.Unsubscribe<EntityDamagedEvent>(OnEntityDamaged);
            EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
            EventBus.Unsubscribe<ItemPickedUpEvent>(OnItemPickedUp);
            EventBus.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        private void Log(string line)
        {
            _log.Add(line);

            while (_log.Count > MaxLogLines)
            {
                _log.RemoveAt(0);
            }
        }

        private void OnPromptChanged(InteractionPromptChangedEvent e)
        {
            _promptVisible = e.Visible;
            _promptText = e.PromptText;
        }

        private void OnInteracted(InteractedEvent e)
        {
            Log("交互: " + e.TargetName);
        }

        private void OnDialogueStarted(DialogueStartedEvent e)
        {
            Log("对话开始: " + (e.Data != null ? e.Data.speakerName : "?"));
        }

        private void OnDialogueLineChanged(DialogueLineChangedEvent e)
        {
            Log((e.Total > 1 ? "台词 " + (e.Index + 1) + "/" + e.Total + ": " : "") + e.Line);
        }

        private void OnDialogueEnded(DialogueEndedEvent e)
        {
            Log("对话结束");
        }

        private void OnQuestAccepted(QuestAcceptedEvent e)
        {
            Log(">>> 接受任务: " + (e.Quest != null ? e.Quest.title : "?"));
        }

        private void OnQuestProgressChanged(QuestProgressChangedEvent e)
        {
            Log("任务进度: " + e.Current + "/" + e.Required);
        }

        private void OnQuestCompleted(QuestCompletedEvent e)
        {
            Log(">>> 任务完成: " + (e.Quest != null ? e.Quest.title : "?"));
        }

        private void OnEntityDamaged(EntityDamagedEvent e)
        {
            Log("伤害 " + e.TargetName + " -" + e.Amount.ToString("0.#") +
                " (剩余 " + e.RemainingHp.ToString("0.#") + ")");
        }

        private void OnEnemyDied(EnemyDiedEvent e)
        {
            Log("死亡: " + e.EnemyTypeId);
        }

        private void OnItemPickedUp(ItemPickedUpEvent e)
        {
            Log("拾取: " + (e.Item != null ? e.Item.displayName : "?") + " x" + e.Amount);
        }

        private void OnInventoryChanged(InventoryChangedEvent e)
        {
            RebuildInventorySummary();
        }

        /// <summary>
        /// 背包内容只在变化时重建一次，不在 OnGUI 里每帧拼字符串。
        /// </summary>
        private void RebuildInventorySummary()
        {
            if (inventory == null)
            {
                _inventorySummary = "—";
                return;
            }

            IReadOnlyList<Inventory.InventoryItem> items = inventory.GetItems();

            if (items.Count == 0)
            {
                _inventorySummary = "（空）";
                return;
            }

            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append("   ");
                }

                builder.Append(items[i].Data != null ? items[i].Data.displayName : "?");

                if (items[i].Amount > 1)
                {
                    builder.Append(" x").Append(items[i].Amount);
                }
            }

            _inventorySummary = builder.ToString();
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null)
            {
                return;
            }

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = FontSize;
            _labelStyle.wordWrap = false;

            _promptStyle = new GUIStyle(GUI.skin.label);
            _promptStyle.fontSize = FontSize + 8;
            _promptStyle.fontStyle = FontStyle.Bold;
            _promptStyle.alignment = TextAnchor.MiddleCenter;
        }

        private void OnGUI()
        {
            EnsureStyles();

            // 以 1080p 高度为基准做整体缩放，保证高 DPI 屏上字号可读。
            float scale = Mathf.Max(1f, Screen.height / ReferenceHeight) * uiScale;
            float virtualWidth = Screen.width / scale;
            float virtualHeight = Screen.height / scale;

            Matrix4x4 savedMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(scale, scale, 1f));

            GUILayout.BeginArea(new Rect(10f, 10f, PanelWidth, PanelHeight), GUI.skin.box);

            GUILayout.Label("=== 玩家 ===", _labelStyle);

            if (playerHealth != null)
            {
                GUILayout.Label("HP: " + playerHealth.CurrentHp.ToString("0.#") +
                    " / " + playerHealth.MaxHp.ToString("0.#"), _labelStyle);
            }
            else
            {
                GUILayout.Label("HP: —", _labelStyle);
            }

            GUILayout.Label(string.Empty, _labelStyle);
            GUILayout.Label("=== 任务 ===", _labelStyle);
            GUILayout.Label(
                questComponent != null ? questComponent.DescribeForHud() : "—",
                _labelStyle);

            GUILayout.Label(string.Empty, _labelStyle);
            GUILayout.Label("=== 背包 ===", _labelStyle);
            GUILayout.Label(_inventorySummary, _labelStyle);

            GUILayout.Label(string.Empty, _labelStyle);
            GUILayout.Label("=== 事件（最新在下）===", _labelStyle);

            for (int i = 0; i < _log.Count; i++)
            {
                GUILayout.Label(_log[i], _labelStyle);
            }

            GUILayout.EndArea();

            if (_promptVisible && !InputLock.IsLocked)
            {
                float w = 420f;
                float h = 44f;

                GUI.Label(
                    new Rect((virtualWidth - w) * 0.5f, virtualHeight - 120f, w, h),
                    "[E] " + _promptText,
                    _promptStyle);
            }

            GUI.matrix = savedMatrix;
        }
    }
}
