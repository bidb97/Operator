using Operator.Missions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Operator.Dialogue
{
    /// <summary>Показывает диалог построчно. Тап переключает строку после минимального времени показа.</summary>
    public class DialoguePanel : MonoBehaviour, IPointerClickHandler
    {
        const float MinDisplaySeconds = 2f;
        const float SecondsPerChar = 0.045f;

        [SerializeField] float autoAdvanceMinSeconds = 4f;

        [SerializeField] GameObject panel;
        [SerializeField] TextMeshProUGUI speakerText;
        [SerializeField] TextMeshProUGUI messageText;
        [SerializeField] Image avatarImage;
        [SerializeField] Image progressImage;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip lineAppearClip;
        [SerializeField] SpeakerProfile operatorSpeaker;
        [SerializeField] AudioSource operatorAudioSource;
        [SerializeField] AudioClip operatorLineAppearClip;

        [Tooltip("Скрывается на время диалога, показывается после его завершения.")]
        [SerializeField] GameObject[] hudElementsToHide;

        DialogueSet _dialogue;
        int _lineIndex;
        float _lineTimer;
        bool _isPlaying;

        public bool IsPlaying => _isPlaying;

        void Awake()
        {
            SetVisible(false);
        }

        void Update()
        {
            if (!_isPlaying)
                return;

            _lineTimer += Time.deltaTime;

            if (progressImage != null)
            {
                var scale = progressImage.rectTransform.localScale;
                scale.x = 1f - Mathf.Clamp01(_lineTimer / MinDisplaySeconds);
                progressImage.rectTransform.localScale = scale;
            }

            if (_lineTimer >= AutoAdvanceSeconds(CurrentLine()))
                Advance();
        }

        public void PlayDialogue(DialogueSet dialogueSet)
        {
            if (dialogueSet == null || dialogueSet.Lines == null || dialogueSet.Lines.Length == 0)
                return;

            _dialogue = dialogueSet;
            _lineIndex = 0;
            SetHudElementsActive(false);
            ShowCurrentLine();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isPlaying || _lineTimer < MinDisplaySeconds)
                return;

            Advance();
        }

        void Advance()
        {
            _lineIndex++;

            if (_dialogue == null || _lineIndex >= _dialogue.Lines.Length)
            {
                StopDialogue();
                return;
            }

            ShowCurrentLine();
        }

        void ShowCurrentLine()
        {
            var line = CurrentLine();
            if (line == null)
            {
                StopDialogue();
                return;
            }

            _lineTimer = 0f;
            _isPlaying = true;

            if (speakerText != null)
                speakerText.text = line.Speaker != null ? line.Speaker.Title : string.Empty;

            if (messageText != null)
                messageText.text = line.Text ?? string.Empty;

            if (avatarImage != null)
            {
                var avatar = line.Speaker != null ? line.Speaker.Avatar : null;
                avatarImage.sprite = avatar;
                avatarImage.enabled = avatar != null;
            }

            if (progressImage != null)
            {
                var scale = progressImage.rectTransform.localScale;
                scale.x = 1f;
                progressImage.rectTransform.localScale = scale;
            }

            SetVisible(true);

            if (line.Speaker != null && line.Speaker == operatorSpeaker)
            {
                if (operatorAudioSource != null && operatorLineAppearClip != null)
                    operatorAudioSource.PlayOneShot(operatorLineAppearClip);
            }
            else if (audioSource != null && lineAppearClip != null)
            {
                audioSource.PlayOneShot(lineAppearClip);
            }
        }

        void StopDialogue()
        {
            _isPlaying = false;
            _dialogue = null;
            SetVisible(false);
            SetHudElementsActive(true);
        }

        DialogueLine CurrentLine()
        {
            if (_dialogue == null || _dialogue.Lines == null || _lineIndex < 0 || _lineIndex >= _dialogue.Lines.Length)
                return null;

            return _dialogue.Lines[_lineIndex];
        }

        float AutoAdvanceSeconds(DialogueLine line)
        {
            var length = line?.Text?.Length ?? 0;
            return Mathf.Max(autoAdvanceMinSeconds, length * SecondsPerChar);
        }

        void SetVisible(bool visible)
        {
            if (panel != null)
                panel.SetActive(visible);
        }

        void SetHudElementsActive(bool active)
        {
            if (hudElementsToHide == null)
                return;

            for (var i = 0; i < hudElementsToHide.Length; i++)
            {
                if (hudElementsToHide[i] != null)
                    hudElementsToHide[i].SetActive(active);
            }
        }
    }
}
