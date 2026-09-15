using UnityEngine;
using Demo.Core;
using Demo.Data;

namespace Demo.Interaction
{
    /// <summary>
    /// NPC 交互。持有三段对话，按任务状态选用其一，并把播放委托给 DialogueRunner。
    /// 所有引用走 Inspector 显式赋值，不做运行时查找。
    /// </summary>
    public class NpcInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string displayName = "村民";

        [Header("三段对话（按任务状态选用）")]
        [SerializeField] private DialogueData dialogueBeforeQuest;
        [SerializeField] private DialogueData dialogueQuestActive;
        [SerializeField] private DialogueData dialogueQuestCompleted;
        [SerializeField] private DialogueData dialogueQuestTurnedIn;

        [Header("引用")]
        [Tooltip("场景中的 DialogueRunner")]
        [SerializeField] private Dialogue.DialogueRunner runner;

        [Tooltip("任务组件，用于判断该说哪一段")]
        [SerializeField] private Quest.QuestComponent questComponent;

        public string PromptText
        {
            get { return "对话"; }
        }

        public Transform Transform
        {
            get { return transform; }
        }

        /// <summary>按任务状态选一段对话。状态都不匹配时退回第一个非空的。</summary>
        public DialogueData SelectDialogue()
        {
            if (questComponent != null && questComponent.PrimaryQuest != null)
            {
                if (questComponent.IsCompleted(questComponent.PrimaryQuest) &&
                    dialogueQuestCompleted != null)
                {
                    return dialogueQuestCompleted;
                }

                if (questComponent.GetStatus(questComponent.PrimaryQuest) == Quest.QuestStatus.TurnedIn &&
                    dialogueQuestTurnedIn != null)
                {
                    return dialogueQuestTurnedIn;
                }

                if (questComponent.IsActive(questComponent.PrimaryQuest) &&
                    dialogueQuestActive != null)
                {
                    return dialogueQuestActive;
                }
            }

            if (dialogueBeforeQuest != null)
            {
                return dialogueBeforeQuest;
            }

            if (dialogueQuestActive != null)
            {
                return dialogueQuestActive;
            }

            return dialogueQuestCompleted;
        }

        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled;
        }

        public void Interact(GameObject interactor)
        {
            EventBus.Publish(new InteractedEvent(displayName));

            // runner 未接线时仅停留在事件广播，不报错。
            if (runner != null)
            {
                runner.BeginFor(this, interactor);
            }
        }
    }
}
