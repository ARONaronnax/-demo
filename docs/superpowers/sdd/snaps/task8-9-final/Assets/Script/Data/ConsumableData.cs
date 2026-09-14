using UnityEngine;

namespace Demo.Data
{
    [CreateAssetMenu(fileName = "ConsumableData", menuName = "Demo/Consumable Data")]
    public class ConsumableData : ItemData
    {
        public int healAmount;

        private void OnValidate()
        {
            itemType = ItemType.Consumable;
        }
    }
}
