using UnityEngine;
using Demo.Core;

namespace Demo.Quest
{
    /// <summary>
    /// 任务桥接。订阅 EnemyDiedEvent 并转成任务进度，
    /// 再把 QuestSystem 的 C# 事件转发为 EventBus 事件供 UI 使用。
    /// Enemy 完全不知道本类的存在。
    /// </summary>
    public class QuestComponent : MonoBehaviour
    {
        [Tooltip("本切片的主线任务")]
        [SerializeField] private Data.QuestData primaryQuest;

        private readonly QuestSystem _system = new QuestSystem();

        public Data.QuestData PrimaryQuest { get { return primaryQuest; } }

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);

            _system.Accepted += OnAccepted;
            _system.ProgressChanged += OnProgressChanged;
            _system.Completed += OnCompleted;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);

            _system.Accepted -= OnAccepted;
            _system.ProgressChanged -= OnProgressChanged;
            _system.Completed -= OnCompleted;
        }

        public bool IsActive(Data.QuestData quest)
        {
            return _system.IsActive(quest);
        }

        public bool IsCompleted(Data.QuestData quest)
        {
            return _system.GetStatus(quest) == QuestStatus.Completed;
        }

        public QuestStatus GetStatus(Data.QuestData quest)
        {
            return _system.GetStatus(quest);
        }

        public int GetProgress(Data.QuestData quest)
        {
            return _system.GetProgress(quest);
        }

        public bool Accept(Data.QuestData quest)
        {
            return _system.Accept(quest);
        }

        public string DescribeForHud()
        {
            if (primaryQuest == null)
            {
                return "未配置任务";
            }

            QuestStatus status = _system.GetStatus(primaryQuest);

            if (status == QuestStatus.NotStarted)
            {
                return primaryQuest.title + "：未接受";
            }

            return primaryQuest.title + "：" +
                _system.GetProgress(primaryQuest) + "/" + primaryQuest.requiredAmount +
                (status == QuestStatus.Completed ? "  已完成" : "  （进行中）");
        }

        private void OnEnemyDied(EnemyDiedEvent e)
        {
            _system.ReportKill(e.EnemyTypeId);
        }

        private void OnAccepted(Data.QuestData quest)
        {
            EventBus.Publish(new QuestAcceptedEvent(quest));
        }

        private void OnProgressChanged(Data.QuestData quest, int current, int required)
        {
            EventBus.Publish(new QuestProgressChangedEvent(quest, current, required));
        }

        private void OnCompleted(Data.QuestData quest)
        {
            EventBus.Publish(new QuestCompletedEvent(quest));
        }
    }
}
