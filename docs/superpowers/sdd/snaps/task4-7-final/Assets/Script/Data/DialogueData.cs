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

        [TextArea]
        public string[] lines;

        [Tooltip("在最后一句之后显示；留空 = 说完即结束")]
        public DialogueOption[] options;
    }
}
