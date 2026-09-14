using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Demo.Core;
using Demo.Data;
using Demo.Dialogue;

namespace Demo.Tests
{
    public class DialogueSystemTests
    {
        private DialogueData _dialogue;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            InputLock.Clear();

            _dialogue = ScriptableObject.CreateInstance<DialogueData>();
            _dialogue.speakerName = "村民";
            _dialogue.lines = new[] { "你好。", "我这里有个委托。" };
        }

        [TearDown]
        public void TearDown()
        {
            if (_dialogue != null)
            {
                Object.DestroyImmediate(_dialogue);
            }
        }

        /// <summary>
        /// 把对话推进到"选项已弹出"的状态。
        /// _dialogue 有 2 句台词，因此需要推进两次；推进次数依赖 lines.Length，
        /// 所以这里断言前置条件，避免测试因为"选项压根没弹"而假通过。
        /// </summary>
        private static void AdvanceToOptions(DialogueSystem system)
        {
            system.TryAdvance(101);
            system.TryAdvance(102);

            Assert.IsTrue(system.HasOptions, "前置条件失败：选项未弹出，后续断言无意义");
            Assert.IsFalse(system.IsRunning, "前置条件失败：弹出选项后对话不应仍处于逐句状态");
        }

        [Test]
        public void Start_ShowsFirstLine()
        {
            var system = new DialogueSystem();
            system.Start(_dialogue, 100);

            Assert.IsTrue(system.IsRunning);
            Assert.AreEqual("村民", system.SpeakerName);
            Assert.AreEqual("你好。", system.CurrentLine);
            Assert.AreEqual(0, system.CurrentIndex);
            Assert.AreEqual(100, system.StartFrame);
        }

        [Test]
        public void Start_RaisesStartedEvent()
        {
            var system = new DialogueSystem();
            DialogueData received = null;
            system.Started += d => received = d;

            system.Start(_dialogue, 100);

            Assert.AreSame(_dialogue, received);
        }

        [Test]
        public void TryAdvance_MovesToNextLine()
        {
            var system = new DialogueSystem();
            system.Start(_dialogue, 100);

            bool advanced = system.TryAdvance(101);

            Assert.IsTrue(advanced);
            Assert.AreEqual("我这里有个委托。", system.CurrentLine);
            Assert.AreEqual(1, system.CurrentIndex);
        }

        [Test]
        public void TryAdvance_PastLastLine_WithNoOptions_Ends()
        {
            var system = new DialogueSystem();
            bool ended = false;
            system.Ended += d => ended = true;

            system.Start(_dialogue, 100);
            system.TryAdvance(101);
            bool advanced = system.TryAdvance(102);

            Assert.IsTrue(advanced);
            Assert.IsTrue(ended);
            Assert.IsFalse(system.IsRunning);
        }

        [Test]
        public void TryAdvance_PastLastLine_WithOptions_PresentsOptions()
        {
            QuestData quest = ScriptableObject.CreateInstance<QuestData>();
            _dialogue.options = new[]
            {
                new DialogueOption { text = "我接受", questToOffer = quest },
                new DialogueOption { text = "再说吧" }
            };

            var system = new DialogueSystem();
            IReadOnlyList<DialogueOption> presented = null;
            system.OptionsPresented += o => presented = o;

            system.Start(_dialogue, 100);
            system.TryAdvance(101);
            system.TryAdvance(102);

            Assert.IsNotNull(presented);
            Assert.AreEqual(2, presented.Count);
            Assert.IsTrue(system.HasOptions);
            Assert.IsFalse(system.IsRunning);

            Object.DestroyImmediate(quest);
        }

        [Test]
        public void TrySelectOption_WithNextDialogue_JumpsToIt()
        {
            DialogueData next = ScriptableObject.CreateInstance<DialogueData>();
            next.speakerName = "村民";
            next.lines = new[] { "那就多谢了。" };

            _dialogue.options = new[] { new DialogueOption { text = "好", nextDialogue = next } };

            var system = new DialogueSystem();
            system.Start(_dialogue, 100);
            AdvanceToOptions(system);

            bool selected = system.TrySelectOption(0, 103);

            Assert.IsTrue(selected);
            Assert.IsTrue(system.IsRunning);
            Assert.AreEqual("那就多谢了。", system.CurrentLine);
            Assert.AreEqual(0, system.CurrentIndex);

            Object.DestroyImmediate(next);
        }

        [Test]
        public void TrySelectOption_WithoutNextDialogue_Ends()
        {
            _dialogue.options = new[] { new DialogueOption { text = "算了" } };

            var system = new DialogueSystem();
            bool ended = false;
            system.Ended += d => ended = true;

            system.Start(_dialogue, 100);
            AdvanceToOptions(system);

            bool selected = system.TrySelectOption(0, 103);

            Assert.IsTrue(selected);
            Assert.IsTrue(ended);
            Assert.IsFalse(system.IsRunning);
            Assert.IsFalse(system.HasOptions);
        }

        [Test]
        public void TrySelectOption_OutOfRange_ReturnsFalse()
        {
            _dialogue.options = new[] { new DialogueOption { text = "算了" } };

            var system = new DialogueSystem();
            system.Start(_dialogue, 100);
            AdvanceToOptions(system);

            Assert.IsFalse(system.TrySelectOption(5, 103));
            Assert.IsFalse(system.TrySelectOption(-1, 103));

            // 越界选择不应消耗掉选项
            Assert.IsTrue(system.HasOptions);
        }

        [Test]
        public void TrySelectOption_WhenNoOptionsPresented_ReturnsFalse()
        {
            var system = new DialogueSystem();
            system.Start(_dialogue, 100);

            Assert.IsFalse(system.TrySelectOption(0, 101));
            Assert.IsTrue(system.IsRunning);
        }

        [Test]
        public void TryAdvance_OnStartFrame_IsIgnored()
        {
            var system = new DialogueSystem();
            system.Start(_dialogue, 100);

            bool advanced = system.TryAdvance(100);

            Assert.IsFalse(advanced);
            Assert.AreEqual("你好。", system.CurrentLine);
            Assert.AreEqual(0, system.CurrentIndex);
        }

        [Test]
        public void Start_WhileRunning_Restarts()
        {
            var system = new DialogueSystem();
            system.Start(_dialogue, 100);
            system.TryAdvance(101);

            system.Start(_dialogue, 200);

            Assert.AreEqual(0, system.CurrentIndex);
            Assert.AreEqual(200, system.StartFrame);
            Assert.AreEqual("你好。", system.CurrentLine);
        }
    }
}
