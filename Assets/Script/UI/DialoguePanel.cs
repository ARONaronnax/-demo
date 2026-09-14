using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Demo.Core;
using Demo.Data;
using Demo.Dialogue;

namespace Demo.UI
{
    /// <summary>RPG 对话 View。仅消费事件，并把按钮索引交还 DialogueRunner。</summary>
    public class DialoguePanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text speakerLabel;
        [SerializeField] private TMP_Text lineLabel;
        [SerializeField] private Image portrait;
        [SerializeField] private Sprite fallbackPortrait;
        [SerializeField] private GameObject continueHint;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private DialogueRunner runner;

        public void Bind(GameObject panelObject, TMP_Text speaker, TMP_Text line, Image portraitImage,
            Sprite defaultPortrait, GameObject hint, Button[] options, DialogueRunner dialogueRunner)
        {
            panel = panelObject;
            speakerLabel = speaker;
            lineLabel = line;
            portrait = portraitImage;
            fallbackPortrait = defaultPortrait;
            continueHint = hint;
            optionButtons = options;
            runner = dialogueRunner;
            Hide();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueStartedEvent>(OnStarted);
            EventBus.Subscribe<DialogueLineChangedEvent>(OnLineChanged);
            EventBus.Subscribe<DialogueOptionsPresentedEvent>(OnOptionsPresented);
            EventBus.Subscribe<DialogueEndedEvent>(OnEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueStartedEvent>(OnStarted);
            EventBus.Unsubscribe<DialogueLineChangedEvent>(OnLineChanged);
            EventBus.Unsubscribe<DialogueOptionsPresentedEvent>(OnOptionsPresented);
            EventBus.Unsubscribe<DialogueEndedEvent>(OnEnded);
        }

        public void Advance() { if (runner != null) runner.TryAdvance(); }
        public void SelectOption0() { SelectOption(0); }
        public void SelectOption1() { SelectOption(1); }
        public void SelectOption2() { SelectOption(2); }

        private void SelectOption(int index)
        {
            if (runner != null) runner.TrySelectOption(index);
        }

        private void OnStarted(DialogueStartedEvent e)
        {
            if (panel != null) panel.SetActive(true);
            if (portrait != null) portrait.sprite = e.Data != null && e.Data.portrait != null ? e.Data.portrait : fallbackPortrait;
            SetOptionsVisible(false);
        }

        private void OnLineChanged(DialogueLineChangedEvent e)
        {
            if (speakerLabel != null) speakerLabel.text = e.Speaker;
            if (lineLabel != null) lineLabel.text = e.Line;
            if (continueHint != null) continueHint.SetActive(true);
        }

        private void OnOptionsPresented(DialogueOptionsPresentedEvent e)
        {
            IReadOnlyList<DialogueOption> choices = e.Options;
            for (int i = 0; optionButtons != null && i < optionButtons.Length; i++)
            {
                bool visible = choices != null && i < choices.Count;
                optionButtons[i].gameObject.SetActive(visible);
                TMP_Text label = optionButtons[i].GetComponentInChildren<TMP_Text>();
                if (visible && label != null) label.text = choices[i].text;
            }
            if (continueHint != null) continueHint.SetActive(false);
        }

        private void OnEnded(DialogueEndedEvent e) { Hide(); }

        private void Hide()
        {
            SetOptionsVisible(false);
            if (panel != null) panel.SetActive(false);
        }

        private void SetOptionsVisible(bool visible)
        {
            if (optionButtons == null) return;
            for (int i = 0; i < optionButtons.Length; i++)
                if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(visible);
        }
    }
}
