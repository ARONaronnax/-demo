using Demo.Core;
using TMPro;
using UnityEngine;

namespace Demo.UI
{
    /// <summary>现有交互检测事件的纯显示层，不参与目标检测或输入处理。</summary>
    public class InteractionPromptPanel : MonoBehaviour
    {
        [SerializeField] private GameObject visual;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private string keyLabel = "E";

        public void Bind(GameObject visualObject, TMP_Text label)
        {
            visual = visualObject;
            promptText = label;
            SetVisible(false, string.Empty);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<InteractionPromptChangedEvent>(OnPromptChanged);
            SetVisible(false, string.Empty);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InteractionPromptChangedEvent>(OnPromptChanged);
        }

        private void OnPromptChanged(InteractionPromptChangedEvent e)
        {
            SetVisible(e.Visible, e.PromptText);
        }

        private void SetVisible(bool visible, string text)
        {
            if (promptText != null)
            {
                promptText.text = "<b>[ " + keyLabel + " ]</b>  " + text;
            }

            if (visual != null)
            {
                visual.SetActive(visible);
            }
        }
    }
}
