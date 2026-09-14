using TMPro;
using UnityEngine;
using Demo.Core;
using Demo.Quest;

namespace Demo.UI
{
    /// <summary>已有 QuestComponent 的轻量只读投影。</summary>
    public class QuestSummaryPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text summary;
        [SerializeField] private QuestComponent quests;

        public void Bind(GameObject panelObject, TMP_Text label, QuestComponent source)
        {
            panel = panelObject;
            summary = label;
            quests = source;
            Refresh();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<QuestAcceptedEvent>(OnQuestChanged);
            EventBus.Subscribe<QuestProgressChangedEvent>(OnQuestChanged);
            EventBus.Subscribe<QuestCompletedEvent>(OnQuestChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<QuestAcceptedEvent>(OnQuestChanged);
            EventBus.Unsubscribe<QuestProgressChangedEvent>(OnQuestChanged);
            EventBus.Unsubscribe<QuestCompletedEvent>(OnQuestChanged);
        }

        public void Toggle()
        {
            if (panel == null) return;
            panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf) Refresh();
        }

        private void OnQuestChanged(QuestAcceptedEvent e) { Refresh(); }
        private void OnQuestChanged(QuestProgressChangedEvent e) { Refresh(); }
        private void OnQuestChanged(QuestCompletedEvent e) { Refresh(); }

        private void Refresh()
        {
            if (summary != null) summary.text = quests != null ? quests.DescribeForHud() : "暂无任务数据";
        }
    }
}
