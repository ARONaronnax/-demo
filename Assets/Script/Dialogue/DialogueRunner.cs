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
        [Tooltip("主键盘数字区上方那一排的 1/2/3，不是小键盘")]
        [SerializeField] private KeyCode advanceKey = KeyCode.E;
        [SerializeField] private KeyCode alternateAdvanceKey = KeyCode.Space;

        [Header("引用")]
        [Tooltip("任务组件；选中带 questToOffer 的选项时由它接受任务")]
        [SerializeField] private Quest.QuestComponent questComponent;

        [Header("诊断")]
        [Tooltip("在 Console 打印接任务链路的每一步，验收期间保持开启")]
        [SerializeField] private bool verboseLog = true;

        private readonly DialogueSystem _system = new DialogueSystem();
        private QuestData _pendingQuestOffer;

        public bool IsRunning
        {
            get { return _system.IsRunning || _system.HasOptions; }
        }

        /// <summary>正式 UI 的继续按钮入口；键盘流程仍保留。</summary>
        public bool TryAdvance()
        {
            return !_system.HasOptions && _system.TryAdvance(Time.frameCount);
        }

        /// <summary>正式 UI 的选项按钮入口；业务判断仍由 DialogueSystem 完成。</summary>
        public bool TrySelectOption(int index)
        {
            return _system.TrySelectOption(index, Time.frameCount);
        }

        private void Log(string message)
        {
            if (verboseLog)
            {
                Debug.Log("[Dialogue] " + message, this);
            }
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
                Log("BeginFor: npc 为空，忽略");
                return;
            }

            DialogueData data = npc.SelectDialogue();
            Log("BeginFor: SelectDialogue 返回 " + (data != null ? data.name : "null"));

            Begin(data);
        }

        public void Begin(DialogueData data)
        {
            if (data == null)
            {
                Log("Begin: data 为空，无法开始对话");
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
            int index = -1;

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                index = 0;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                index = 1;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                index = 2;
            }

            if (index < 0)
            {
                return;
            }

            bool accepted = _system.TrySelectOption(index, Time.frameCount);

            Log("按下数字键 " + (index + 1) + " → TrySelectOption 返回 " + accepted);

            if (!accepted)
            {
                Log("选项未被接受。若你按的是小键盘，请改用主键盘数字排");
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
            Log("选项已弹出，共 " + options.Count + " 项");

            EventBus.Publish(new DialogueOptionsPresentedEvent(options));

            for (int i = 0; i < options.Count; i++)
            {
                DialogueOption opt = options[i];
                Log("  选项 " + (i + 1) + ": " + opt.text +
                    " / nextDialogue=" + (opt.nextDialogue != null ? opt.nextDialogue.name : "null") +
                    " / questToOffer=" + (opt.questToOffer != null ? opt.questToOffer.name : "null"));

            }
        }

        private void OnOptionSelected(DialogueOption option)
        {
            if (option == null)
            {
                Log("OnOptionSelected: option 为空");
                return;
            }

            if (option.questToOffer == null)
            {
                Log("OnOptionSelected: 该选项没有 questToOffer，不会接任务");
                return;
            }

            _pendingQuestOffer = option.questToOffer;
            Log("OnOptionSelected: 记下待接任务 " + option.questToOffer.name);
        }

        private void OnEnded(DialogueData data)
        {
            InputLock.Release(this);

            if (_pendingQuestOffer == null)
            {
                Log("OnEnded: 没有待接任务，直接结束");
            }
            else if (questComponent == null)
            {
                Log("OnEnded: 有待接任务 " + _pendingQuestOffer.name +
                    "，但 DialogueRunner.questComponent 未接线，任务未接");
            }
            else
            {
                bool ok = questComponent.Accept(_pendingQuestOffer);
                Log("OnEnded: 调用 Accept(" + _pendingQuestOffer.name + ") 返回 " + ok);
            }

            _pendingQuestOffer = null;

            EventBus.Publish(new DialogueEndedEvent(data));
        }
    }
}
