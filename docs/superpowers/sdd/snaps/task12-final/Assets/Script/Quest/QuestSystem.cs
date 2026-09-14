using System;
using System.Collections.Generic;
using Demo.Data;

namespace Demo.Quest
{
    /// <summary>
    /// 纯逻辑任务系统。不认识 Enemy、不认识场景。
    /// 击杀来源由 QuestComponent 通过 EnemyDiedEvent 转发进来。
    /// </summary>
    public class QuestSystem
    {
        private class Entry
        {
            public QuestStatus Status;
            public int Progress;
        }

        private readonly Dictionary<QuestData, Entry> _entries =
            new Dictionary<QuestData, Entry>();

        public event Action<QuestData> Accepted;
        public event Action<QuestData, int, int> ProgressChanged;
        public event Action<QuestData> Completed;

        public QuestStatus GetStatus(QuestData quest)
        {
            Entry entry = Find(quest);
            return entry != null ? entry.Status : QuestStatus.NotStarted;
        }

        public int GetProgress(QuestData quest)
        {
            Entry entry = Find(quest);
            return entry != null ? entry.Progress : 0;
        }

        public bool IsActive(QuestData quest)
        {
            return GetStatus(quest) == QuestStatus.InProgress;
        }

        public bool Accept(QuestData quest)
        {
            if (quest == null)
            {
                return false;
            }

            if (Find(quest) != null)
            {
                return false;
            }

            Entry entry = new Entry { Status = QuestStatus.InProgress, Progress = 0 };
            _entries[quest] = entry;

            if (Accepted != null)
            {
                Accepted(quest);
            }

            if (ProgressChanged != null)
            {
                ProgressChanged(quest, 0, quest.requiredAmount);
            }

            return true;
        }

        public bool ReportKill(string enemyTypeId)
        {
            foreach (KeyValuePair<QuestData, Entry> pair in _entries)
            {
                QuestData quest = pair.Key;
                Entry entry = pair.Value;

                if (entry.Status != QuestStatus.InProgress)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(quest.targetEnemyTypeId) &&
                    quest.targetEnemyTypeId != enemyTypeId)
                {
                    continue;
                }

                entry.Progress++;

                if (entry.Progress >= quest.requiredAmount)
                {
                    entry.Status = QuestStatus.Completed;
                    entry.Progress = quest.requiredAmount;

                    if (ProgressChanged != null)
                    {
                        ProgressChanged(quest, entry.Progress, quest.requiredAmount);
                    }

                    if (Completed != null)
                    {
                        Completed(quest);
                    }
                }
                else if (ProgressChanged != null)
                {
                    ProgressChanged(quest, entry.Progress, quest.requiredAmount);
                }

                return true;
            }

            return false;
        }

        private Entry Find(QuestData quest)
        {
            if (quest == null)
            {
                return null;
            }

            Entry entry;
            return _entries.TryGetValue(quest, out entry) ? entry : null;
        }
    }
}
