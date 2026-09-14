using UnityEngine;
using Demo.Core;

namespace Demo.Interaction
{
    /// <summary>
    /// NPC 交互。持有三段对话（按任务状态选用），并把对话播放委托给 DialogueRunner。
    /// runner 走 Inspector 显式引用，不做运行时查找。
    /// </summary>
    public class NpcInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string displayName = "村民";

        [Header("引用")]
        [Tooltip("场景中的 DialogueRunner，Inspector 里拖入")]
        [SerializeField] private Dialogue.DialogueRunner runner;

        public string PromptText
        {
            get { return "对话"; }
        }

        public Transform Transform
        {
            get { return transform; }
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
