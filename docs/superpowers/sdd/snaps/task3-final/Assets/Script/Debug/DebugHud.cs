using System.Collections.Generic;
using UnityEngine;
using Demo.Core;
using Demo.Data;

namespace Demo.Debugging
{
    /// <summary>
    /// 临时验收用 IMGUI HUD。做正式 UI 时整个 Debug/ 目录删除。
    /// 所有引用均为可选，未接入的系统显示为 "—"。
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        private const int MaxLogLines = 8;

        [SerializeField] private Combat.HealthComponent playerHealth;
        [SerializeField] private Quest.QuestComponent questComponent;

        private readonly List<string> _log = new List<string>();

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
            Log("台词 " + (e.Index + 1) + "/" + e.Total + ": " + e.Line);
        }

        private void OnDialogueEnded(DialogueEndedEvent e)
        {
            Log("对话结束");
        }

        private void OnQuestAccepted(QuestAcceptedEvent e)
        {
            Log("接受任务: " + (e.Quest != null ? e.Quest.title : "?"));
        }

        private void OnQuestProgressChanged(QuestProgressChangedEvent e)
        {
            Log("任务进度: " + e.Current + "/" + e.Required);
        }

        private void OnQuestCompleted(QuestCompletedEvent e)
        {
            Log("任务完成: " + (e.Quest != null ? e.Quest.title : "?"));
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

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10f, 10f, 460f, 340f), GUI.skin.box);

            GUILayout.Label("=== 玩家 ===");

            if (playerHealth != null)
            {
                GUILayout.Label("HP: " + playerHealth.CurrentHp.ToString("0.#") +
                    " / " + playerHealth.MaxHp.ToString("0.#"));
            }
            else
            {
                GUILayout.Label("HP: —");
            }

            GUILayout.Label("");
            GUILayout.Label("=== 任务 ===");

            if (questComponent != null)
            {
                GUILayout.Label(questComponent.DescribeForHud());
            }
            else
            {
                GUILayout.Label("—");
            }

            GUILayout.Label("");
            GUILayout.Label("=== 事件 ===");

            for (int i = 0; i < _log.Count; i++)
            {
                GUILayout.Label(_log[i]);
            }

            GUILayout.EndArea();

            if (_promptVisible && !InputLock.IsLocked)
            {
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 60f, Screen.height - 90f, 200f, 30f),
                    "[E] " + _promptText);
            }
        }
    }
}
