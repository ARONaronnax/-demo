using UnityEngine;
using UnityEngine.Events;

namespace Demo.UI
{
    /// <summary>右上菜单路由。已有系统直接接入，未实现系统保留 UnityEvent。</summary>
    public class TopRightMenu : MonoBehaviour
    {
        [SerializeField] private InventoryUI inventory;
        [SerializeField] private QuestSummaryPanel quests;
        [SerializeField] private UnityEvent onMapRequested;
        [SerializeField] private UnityEvent onSettingsRequested;

        public void Bind(InventoryUI inventoryView, QuestSummaryPanel questView)
        {
            inventory = inventoryView;
            quests = questView;
        }

        public void OpenInventory() { if (inventory != null) inventory.Open(); }
        public void ToggleQuest() { if (quests != null) quests.Toggle(); }
        public void RequestMap() { if (onMapRequested != null) onMapRequested.Invoke(); }
        public void RequestSettings() { if (onSettingsRequested != null) onSettingsRequested.Invoke(); }
    }
}
