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

        [Header("诊断")]
        [Tooltip("在 Console 打印任务状态变化，验收期间保持开启")]
        [SerializeField] private bool verboseLog = true;

        private readonly QuestSystem _system = new QuestSystem();

        public Data.QuestData PrimaryQuest { get { return primaryQuest; } }

        private void OnEnable()
        {
            EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);

            _system.Accepted += OnAccepted;
            _system.ProgressChanged += OnProgressChanged;
            _system.Completed += OnCompleted;
            _system.TurnedIn += OnTurnedIn;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);

            _system.Accepted -= OnAccepted;
            _system.ProgressChanged -= OnProgressChanged;
            _system.Completed -= OnCompleted;
            _system.TurnedIn -= OnTurnedIn;
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
            bool ok = _system.Accept(quest);

            if (verboseLog)
            {
                Debug.Log("[Quest] Accept(" + (quest != null ? quest.name : "null") +
                    ") 返回 " + ok, this);
            }

            return ok;
        }

        public bool TurnIn(Data.QuestData quest)
        {
            return _system.TurnIn(quest);
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
                return "探索村庄，和烦恼的村民对话";
            }

            if (status == QuestStatus.Completed)
            {
                return "任务完成，找村民汇报";
            }

            if (status == QuestStatus.TurnedIn)
            {
                return "委托完成：村庄恢复了平静";
            }

            return primaryQuest.objectiveText + "（" +
                _system.GetProgress(primaryQuest) + "/" + primaryQuest.requiredAmount + "）";
        }

        private void OnEnemyDied(EnemyDiedEvent e)
        {
            _system.ReportKill(e.EnemyTypeId);
        }

        private void OnAccepted(Data.QuestData quest)
        {
            if (verboseLog)
            {
                Debug.Log("[Quest] 发布 QuestAcceptedEvent: " +
                    (quest != null ? quest.title : "null"), this);
            }

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

        private void OnTurnedIn(Data.QuestData quest)
        {
            EventBus.Publish(new QuestTurnedInEvent(quest));

            if (quest != null && quest.rewardItem != null && quest.rewardAmount > 0)
            {
                EventBus.Publish(new QuestRewardGrantedEvent(quest, quest.rewardItem, quest.rewardAmount));
            }
        }
    }
}
