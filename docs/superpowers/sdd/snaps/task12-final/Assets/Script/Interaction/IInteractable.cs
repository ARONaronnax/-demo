using UnityEngine;

namespace Demo.Interaction
{
    /// <summary>
    /// 可交互对象。PromptText 只返回动词（"对话"/"拾取"），
    /// 按键前缀 "[E] " 由 UI 拼接。
    /// </summary>
    public interface IInteractable
    {
        string PromptText { get; }
        Transform Transform { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
