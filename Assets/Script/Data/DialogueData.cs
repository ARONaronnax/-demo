using System;
using UnityEngine;

namespace Demo.Data
{
    [Serializable]
    public class DialogueOption
    {
        public string text;

        [Tooltip("留空 = 结束对话")]
        public DialogueData nextDialogue;

        [Tooltip("非空 = 选中此项即接受该任务")]
        public QuestData questToOffer;
    }

    [CreateAssetMenu(fileName = "DialogueData", menuName = "Demo/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        public string speakerName;

        [Tooltip("仅供对话 UI 显示；留空时使用界面上的默认 NPC 头像")]
        public Sprite portrait;

        [TextArea]
        public string[] lines;

        [Tooltip("在最后一句之后显示；留空 = 说完即结束")]
        public DialogueOption[] options;
    }
}
