using System;
using System.Collections.Generic;
using Demo.Data;

namespace Demo.Dialogue
{
    /// <summary>
    /// 纯逻辑对话推进。不依赖任何场景对象。
    /// 帧号由调用方传入，使同帧输入守卫可在 EditMode 中直接测试。
    /// </summary>
    public class DialogueSystem
    {
        private static readonly DialogueOption[] EmptyOptions = new DialogueOption[0];

        private DialogueData _data;
        private int _index;
        private DialogueOption[] _presentedOptions = EmptyOptions;
        private bool _awaitingOption;

        public bool IsRunning { get; private set; }
        public int StartFrame { get; private set; }
        public int CurrentIndex { get { return _index; } }

        public string SpeakerName
        {
            get { return _data != null ? _data.speakerName : string.Empty; }
        }

        public string CurrentLine
        {
            get
            {
                if (_data == null || _data.lines == null)
                {
                    return string.Empty;
                }

                if (_index < 0 || _index >= _data.lines.Length)
                {
                    return string.Empty;
                }

                return _data.lines[_index];
            }
        }

        public bool HasOptions
        {
            get { return _presentedOptions.Length > 0; }
        }

        public IReadOnlyList<DialogueOption> CurrentOptions
        {
            get { return _presentedOptions; }
        }

        public event Action<DialogueData> Started;
        public event Action<string, int, int> LineChanged;
        public event Action<IReadOnlyList<DialogueOption>> OptionsPresented;
        public event Action<DialogueOption> OptionSelected;
        public event Action<DialogueData> Ended;

        public void Start(DialogueData data, int currentFrame)
        {
            _data = data;
            _index = 0;
            _presentedOptions = EmptyOptions;
            _awaitingOption = false;
            IsRunning = true;
            StartFrame = currentFrame;

            if (Started != null)
            {
                Started(_data);
            }

            RaiseLineChanged();
        }

        public bool TryAdvance(int currentFrame)
        {
            // 同帧守卫：触发交互的那个按键不能在同一帧又推进对话。
            if (!IsRunning || currentFrame == StartFrame || _awaitingOption)
            {
                return false;
            }

            int lineCount = _data != null && _data.lines != null ? _data.lines.Length : 0;

            if (_index + 1 < lineCount)
            {
                _index++;
                RaiseLineChanged();
                return true;
            }

            if (_data != null && _data.options != null && _data.options.Length > 0)
            {
                _presentedOptions = _data.options;
                _awaitingOption = true;
                IsRunning = false;

                if (OptionsPresented != null)
                {
                    OptionsPresented(_presentedOptions);
                }

                return true;
            }

            Finish();
            return true;
        }

        public bool TrySelectOption(int index, int currentFrame)
        {
            // 防御性守卫：正常流程下走不到这里——选项只可能由 TryAdvance 弹出，
            // 而 TryAdvance 已把同帧情形挡掉。保留是为了防止将来重构 Start 时
            // 重新引入"触发交互的同一个按键又把选项选掉"的问题。
            if (currentFrame == StartFrame)
            {
                return false;
            }

            // 真正的保护：没有选项可选的时侯不能被选中。
            if (!_awaitingOption)
            {
                return false;
            }

            if (index < 0 || index >= _presentedOptions.Length)
            {
                return false;
            }

            DialogueOption chosen = _presentedOptions[index];
            _awaitingOption = false;
            _presentedOptions = EmptyOptions;

            if (OptionSelected != null)
            {
                OptionSelected(chosen);
            }

            if (chosen.nextDialogue != null)
            {
                Start(chosen.nextDialogue, currentFrame);
                return true;
            }

            Finish();
            return true;
        }

        private void RaiseLineChanged()
        {
            if (LineChanged == null)
            {
                return;
            }

            int total = _data != null && _data.lines != null ? _data.lines.Length : 0;
            LineChanged(CurrentLine, _index, total);
        }

        private void Finish()
        {
            // 先留存再清空：订阅方（DialogueRunner）需要知道刚结束的是哪段对话。
            DialogueData finished = _data;

            IsRunning = false;
            _data = null;
            _presentedOptions = EmptyOptions;
            _awaitingOption = false;

            if (Ended != null)
            {
                Ended(finished);
            }
        }
    }
}
