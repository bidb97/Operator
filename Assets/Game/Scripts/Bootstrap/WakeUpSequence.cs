using System.Collections;
using Operator.Dialogue;
using UnityEngine;

namespace Operator.Bootstrap
{
    /// <summary>Пробуждение оператора: после паузы играет вступительный диалог (HUD скрывает/показывает сама DialoguePanel).</summary>
    public class WakeUpSequence : MonoBehaviour
    {
        [SerializeField] DialoguePanel dialoguePanel;
        [SerializeField] DialogueSet wakeUpDialogue;
        [SerializeField] float delayBeforeDialogue = 3f;

        void Start()
        {
            StartCoroutine(RunSequence());
        }

        IEnumerator RunSequence()
        {
            yield return new WaitForSeconds(delayBeforeDialogue);

            if (dialoguePanel != null && wakeUpDialogue != null)
                dialoguePanel.PlayDialogue(wakeUpDialogue);
        }
    }
}
