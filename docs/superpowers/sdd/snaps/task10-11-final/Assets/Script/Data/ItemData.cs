using UnityEngine;

namespace Demo.Data
{
    public enum ItemType
    {
        Weapon,
        Consumable,
        Quest
    }

    public abstract class ItemData : ScriptableObject
    {
        public string id;
        public string displayName;

        [TextArea]
        public string description;

        public Sprite icon;
        public int maxStack = 1;
        public ItemType itemType;
    }
}
