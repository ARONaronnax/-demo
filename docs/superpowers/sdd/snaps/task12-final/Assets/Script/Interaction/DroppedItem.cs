using UnityEngine;
using Demo.Core;
using Demo.Data;

namespace Demo.Interaction
{
    /// <summary>
    /// 地面掉落物。只发布 ItemPickedUpEvent，不引用 InventorySystem。
    /// 由 DropSpawner 在运行时动态挂载，无需手工制作 prefab。
    /// </summary>
    public class DroppedItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData item;
        [SerializeField] private int amount = 1;

        public string PromptText
        {
            get { return "拾取"; }
        }

        public Transform Transform
        {
            get { return transform; }
        }

        public ItemData Item { get { return item; } }
        public int Amount { get { return amount; } }

        public void Setup(ItemData data, int count)
        {
            item = data;
            amount = count;
        }

        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled && item != null;
        }

        public void Interact(GameObject interactor)
        {
            if (item == null)
            {
                return;
            }

            EventBus.Publish(new ItemPickedUpEvent(item, amount));

            Destroy(gameObject);
        }
    }
}
