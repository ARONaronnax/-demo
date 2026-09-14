using System.Collections.Generic;
using UnityEngine;
using Demo.Core;
using Demo.Data;

namespace Demo.Dialogue
{
    /// <summary>
    /// 对话桥接：把 DialogueSystem 的纯逻辑状态转成 EventBus 事件，
    /// 并在对话期间持有输入锁。
    ///
    /// 本切片用键盘推进（E / Space 下一句，1/2/3 选选项）；
    /// 做正式 UI 时改为鼠标点击，DialogueSystem 不需要改动。
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        [Header("按键")]
        [SerializeField] private KeyCode advanceKey = KeyCode.E;
        [SerializeField] private KeyCode alternateAdvanceKey = KeyCode.Space;

        [Header("引用")]
        [Tooltip("任务组件；选中带 questToOffer 的选项时由它接受任务")]
        [SerializeField] private Quest.QuestComponent questComponent;

        private readonly DialogueSystem _system = new DialogueSystem();
        private QuestData _pendingQuestOffer;

        public bool IsRunning
        {
            get { return _system.IsRunning || _system.HasOptions; }
        }

        private void OnEnable()
        {
            _system.Started += OnStarted;
            _system.LineChanged += OnLineChanged;
            _system.OptionsPresented += OnOptionsPresented;
            _system.OptionSelected += OnOptionSelected;
            _system.Ended += OnEnded;
        }

        private void OnDisable()
        {
            _system.Started -= OnStarted;
            _system.LineChanged -= OnLineChanged;
            _system.OptionsPresented -= OnOptionsPresented;
            _system.OptionSelected -= OnOptionSelected;
            _system.Ended -= OnEnded;

            // 对话中途被禁用时不能把输入锁漏在场上。
            if (IsRunning)
            {
                InputLock.Release(this);
            }
        }

        /// <summary>由 NpcInteractable 调用，播放它按任务状态选定的那段对话。</summary>
        public void BeginFor(Interaction.NpcInteractable npc, GameObject interactor)
        {
            if (npc == null)
            {
                return;
            }

            Begin(npc.SelectDialogue());
        }

        public void Begin(DialogueData data)
        {
            if (data == null)
            {
                return;
            }

            _pendingQuestOffer = null;
            InputLock.Acquire(this);
            _system.Start(data, Time.frameCount);
        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            if (_system.HasOptions)
            {
                HandleOptionInput();
                return;
            }

            if (Input.GetKeyDown(advanceKey) ||
                Input.GetKeyDown(alternateAdvanceKey))
            {
                _system.TryAdvance(Time.frameCount);
            }
        }

        private void HandleOptionInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                _system.TrySelectOption(0, Time.frameCount);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                _system.TrySelectOption(1, Time.frameCount);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                _system.TrySelectOption(2, Time.frameCount);
            }
        }

        private void OnStarted(DialogueData data)
        {
            EventBus.Publish(new DialogueStartedEvent(data));
        }

        private void OnLineChanged(string line, int index, int total)
        {
            EventBus.Publish(new DialogueLineChangedEvent(_system.SpeakerName, line, index, total));
        }

        private void OnOptionsPresented(IReadOnlyList<DialogueOption> options)
        {
            // 临时 HUD 没有独立选项 UI，这里把选项也走台词通道发出去。
            // 做正式 UI 时改为发布专门的选项事件。
            for (int i = 0; i < options.Count; i++)
            {
                EventBus.Publish(new DialogueLineChangedEvent(
                    _system.SpeakerName,
                    "[" + (i + 1) + "] " + options[i].text,
                    i,
                    options.Count));
            }
        }

        private void OnOptionSelected(DialogueOption option)
        {
            if (option != null && option.questToOffer != null)
            {
                _pendingQuestOffer = option.questToOffer;
            }
        }

        private void OnEnded(DialogueData data)
        {
            InputLock.Release(this);

            if (_pendingQuestOffer != null)
            {
                if (questComponent != null)
                {
                    questComponent.Accept(_pendingQuestOffer);
                }

                _pendingQuestOffer = null;
            }

            EventBus.Publish(new DialogueEndedEvent(data));
        }
    }
}
