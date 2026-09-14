using UnityEngine;

namespace Demo.Data
{
    [CreateAssetMenu(fileName = "QuestItemData", menuName = "Demo/Quest Item Data")]
    public class QuestItemData : ItemData
    {
        private void OnValidate()
        {
            itemType = ItemType.Quest;
        }
    }
}
