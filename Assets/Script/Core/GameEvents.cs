using UnityEngine;
using Demo.Data;

namespace Demo.Core
{
    /// <summary>交互提示变化。PromptText 不含按键前缀，UI 自行拼 "[E] "。</summary>
    public readonly struct InteractionPromptChangedEvent
    {
        public readonly string PromptText;
        public readonly bool Visible;

        public InteractionPromptChangedEvent(string promptText, bool visible)
        {
            PromptText = promptText;
            Visible = visible;
        }
    }

    /// <summary>玩家完成了一次交互。</summary>
    public readonly struct InteractedEvent
    {
        public readonly string TargetName;

        public InteractedEvent(string targetName)
        {
            TargetName = targetName;
        }
    }

    public readonly struct DialogueStartedEvent
    {
        public readonly DialogueData Data;

        public DialogueStartedEvent(DialogueData data)
        {
            Data = data;
        }
    }

    public readonly struct DialogueLineChangedEvent
    {
        public readonly string Speaker;
        public readonly string Line;
        public readonly int Index;
        public readonly int Total;

        public DialogueLineChangedEvent(string speaker, string line, int index, int total)
        {
            Speaker = speaker;
            Line = line;
            Index = index;
            Total = total;
        }
    }

    public readonly struct DialogueEndedEvent
    {
        public readonly DialogueData Data;

        public DialogueEndedEvent(DialogueData data)
        {
            Data = data;
        }
    }

    /// <summary>对话选项出现。UI 只展示并把索引交还给 DialogueRunner。</summary>
    public readonly struct DialogueOptionsPresentedEvent
    {
        public readonly System.Collections.Generic.IReadOnlyList<DialogueOption> Options;

        public DialogueOptionsPresentedEvent(System.Collections.Generic.IReadOnlyList<DialogueOption> options)
        {
            Options = options;
        }
    }

    public readonly struct QuestAcceptedEvent
    {
        public readonly QuestData Quest;

        public QuestAcceptedEvent(QuestData quest)
        {
            Quest = quest;
        }
    }

    public readonly struct QuestProgressChangedEvent
    {
        public readonly QuestData Quest;
        public readonly int Current;
        public readonly int Required;

        public QuestProgressChangedEvent(QuestData quest, int current, int required)
        {
            Quest = quest;
            Current = current;
            Required = required;
        }
    }

    public readonly struct QuestCompletedEvent
    {
        public readonly QuestData Quest;

        public QuestCompletedEvent(QuestData quest)
        {
            Quest = quest;
        }
    }

    public readonly struct QuestTurnedInEvent
    {
        public readonly QuestData Quest;

        public QuestTurnedInEvent(QuestData quest)
        {
            Quest = quest;
        }
    }

    public readonly struct QuestRewardGrantedEvent
    {
        public readonly QuestData Quest;
        public readonly ItemData Item;
        public readonly int Amount;

        public QuestRewardGrantedEvent(QuestData quest, ItemData item, int amount)
        {
            Quest = quest;
            Item = item;
            Amount = amount;
        }
    }

    public readonly struct EntityDamagedEvent
    {
        public readonly string TargetName;
        public readonly float Amount;
        public readonly float RemainingHp;
        public readonly bool IsPlayer;

        public EntityDamagedEvent(string targetName, float amount, float remainingHp, bool isPlayer)
        {
            TargetName = targetName;
            Amount = amount;
            RemainingHp = remainingHp;
            IsPlayer = isPlayer;
        }
    }

    /// <summary>怪物死亡。Enemy 只发布此事件，不引用 QuestSystem 或 DropSpawner。</summary>
    public readonly struct EnemyDiedEvent
    {
        public readonly string EnemyTypeId;
        public readonly Vector3 Position;
        public readonly WeaponData Drop;

        public EnemyDiedEvent(string enemyTypeId, Vector3 position, WeaponData drop)
        {
            EnemyTypeId = enemyTypeId;
            Position = position;
            Drop = drop;
        }
    }

    /// <summary>
    /// 玩家死亡。怪物订阅它来脱战——玩家不再是可以追的目标，
    /// 于是退回 Idle 改为在出生点附近游走。
    /// </summary>
    public readonly struct PlayerDiedEvent
    {
        public readonly Vector3 Position;

        public PlayerDiedEvent(Vector3 position)
        {
            Position = position;
        }
    }

    public readonly struct ItemPickedUpEvent
    {
        public readonly ItemData Item;
        public readonly int Amount;

        public ItemPickedUpEvent(ItemData item, int amount)
        {
            Item = item;
            Amount = amount;
        }
    }

    /// <summary>
    /// 背包内容变化。只是"变了"的信号，不携带数据——
    /// 订阅方（UI）自己去问 InventoryComponent 要当前内容，避免事件里塞快照。
    /// </summary>
    public readonly struct InventoryChangedEvent
    {
    }

    /// <summary>
    /// 玩家在详情区点了"装备"。这是**请求**不是结果——
    /// UI 只管喊一声，装不装、怎么装由 EquipmentComponent 决定。
    /// 和 DroppedItem 发 ItemPickedUpEvent 是同一个路子。
    /// </summary>
    public readonly struct EquipRequestedEvent
    {
        public readonly WeaponData Weapon;

        public EquipRequestedEvent(WeaponData weapon)
        {
            Weapon = weapon;
        }
    }

    public readonly struct UseConsumableRequestedEvent
    {
        public readonly ConsumableData Consumable;

        public UseConsumableRequestedEvent(ConsumableData consumable)
        {
            Consumable = consumable;
        }
    }

    public readonly struct EntityHealedEvent
    {
        public readonly float Amount;
        public readonly float CurrentHp;
        public readonly float MaxHp;
        public readonly bool IsPlayer;

        public EntityHealedEvent(float amount, float currentHp, float maxHp, bool isPlayer)
        {
            Amount = amount;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            IsPlayer = isPlayer;
        }
    }

    /// <summary>
    /// 装备变了（装上、换掉、卸下都会发）。Weapon 为 null 表示现在空手。
    /// Phase 07 的武器挂点订阅它来换模型。
    /// </summary>
    public readonly struct WeaponEquippedEvent
    {
        public readonly WeaponData Weapon;

        public WeaponEquippedEvent(WeaponData weapon)
        {
            Weapon = weapon;
        }
    }
}
