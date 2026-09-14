using UnityEngine;

namespace Demo.Data
{
    [CreateAssetMenu(fileName = "QuestData", menuName = "Demo/Quest Data")]
    public class QuestData : ScriptableObject
    {
        [Header("标识")]
        [Tooltip("如 KillMonsters")]
        public string id;

        public string title;

        [TextArea]
        public string description;

        [Header("目标")]
        [TextArea]
        public string objectiveText;

        public int requiredAmount = 3;

        [Tooltip("留空 = 任意敌人都计数")]
        public string targetEnemyTypeId;

        [Header("奖励（本切片不发放）")]
        public ItemData rewardItem;
        public int rewardAmount;
    }
}
