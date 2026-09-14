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
}
