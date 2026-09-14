using Demo.Data;

namespace Demo.Inventory
{
    /// <summary>
    /// 背包里的一格。Data 指向静态的 ItemData 资产（不持有它的副本），
    /// Amount 是这一格的堆叠数量。
    ///
    /// Amount 的 setter 是 internal：只有同程序集的 InventorySystem 能改，
    /// UI 等其他程序集只能读，避免绕过 AddItem / RemoveItem 直接篡改数据。
    /// </summary>
    public class InventoryItem
    {
        public ItemData Data { get; internal set; }
        public int Amount { get; internal set; }

        public InventoryItem(ItemData data, int amount)
        {
            Data = data;
            Amount = amount;
        }
    }
}
